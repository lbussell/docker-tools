# Handoff: ORAS OAuth token cache is defeated during image copy

## Summary

During the `copyAcrImages` (Publish stage "Copy Images") step, ImageBuilder
issues **one OAuth2 token request per tag** instead of roughly one per
repository. In a representative run this was **1,706 token fetches for only 7
distinct repos**, all bursting within ~2 seconds. That burst is the sole source
of the `429 (Too Many Requests)` throttling observed on the registry's
`oauth2/token` endpoint, and it also produces 1,706 unnecessary `401` challenges.

This document describes **one issue** for a fresh agent to investigate and fix:
the OAuth token cache provides essentially zero cross-operation benefit because
of how ImageBuilder instantiates the ORAS auth `Client`.

## Impact

- ~1,706 OAuth `oauth2/token` requests where ~7 would suffice (one per repo
  scope), plus ~1,706 redundant `401` manifest challenges.
- All token requests fire in a ~2s burst (peak ≈ 2,151 requests / 60s window)
  → **445 `429` throttle responses** on `oauth2/token`. They were absorbed by
  retries in the sampled run, but this is load- and tier-dependent and is the
  same class of throttling that has caused Publish-stage failures.
- This is **separate from** (but compounded by) the unbounded `Task.WhenAll`
  fan-out in `CopyAcrImagesCommand`. Even fully serialized, each operation would
  still incur one `401` + one fresh token fetch.

## Root cause

The ORAS `OrasProject.Oras` (v0.5.0) auth token cache is keyed by
**`host + scopes`**. The scope for a repository is only learned *from the `401`
challenge response* and is tracked in the `Client`'s **per-instance
`ScopeManager`** (`ScopeManager { get; set; } = new();`).

ImageBuilder constructs a **new `Client` (and therefore a new empty
`ScopeManager`) for every operation**, in
`OrasDotNetService.CreateRepository()` (one call per `GetReferrersAsync`, i.e.
per tag):

```csharp
// src/ImageBuilder/Oras/OrasDotNetService.cs (~line 236-237)
HttpClient httpClient = _httpClientFactory.CreateClient(nameof(OrasDotNetService));
Client authClient = new(httpClient, _credentialProvider, _orasCache);
```

Because each operation starts with an empty `ScopeManager`,
`Client.SendCoreAsync` computes an **empty scopes string**, so the cache lookup
key never matches the cached token (which is stored under the real scope, e.g.
`repository:dotnet/nightly/runtime-deps:pull`). Every operation therefore:

1. looks up the cache with the wrong (empty-scope) key → miss,
2. sends an unauthenticated request → `401`,
3. parses the challenge, fetches a fresh token, caches it, retries → `200`.

The shared `_orasCache` (a singleton `IMemoryCache` wrapped in ORAS `Cache`) only
helps **within a single `Client`'s lifetime** — which is exactly why the manifest
`HEAD` (401 → token) and the immediately-following referrers `GET` on the *same*
client do reuse the token (observed: referrers `GET` returns `200` with no `401`).

### Evidence (sampled run)

- 7 distinct repos, 1,706 manifest `HEAD`s (each `401`→`200`), 1,706 token
  fetches, 1,706 referrers `GET`s (`200` only). 1:1 token-per-HEAD ratio.
- All `oauth2/token` requests within a 4.5s span (1,709 within the first 2s).
- `oauth2/token`: 1,706 × `200`, 445 × `429`.

## Relevant code

- `src/ImageBuilder/Oras/OrasDotNetService.cs` — `CreateRepository()` creates a
  new ORAS `Client` per call; `_orasCache` is the shared token cache;
  `GetReferrersImplAsync()` / `GetDescriptorAsync()` are the per-operation entry
  points.
- `src/ImageBuilder/Commands/CopyAcrImagesCommand.cs` — `ExecuteAsync()` fans out
  all tag imports via `Task.WhenAll` (unbounded), compressing the token fetches
  into one burst.
- `src/ImageBuilder/CopyImageService.cs` — `ImportImageAsync()` calls
  `GetReferrersAsync` (the manifest HEAD + referrers lookups) before each ARM
  import.
- ORAS library reference (v0.5.0), for understanding only:
  - `Registry/Remote/Auth/Client.cs` — `SendCoreAsync` (cache-key logic),
    per-instance `ScopeManager`.
  - `Registry/Remote/Auth/Cache.cs` — `host + scopes` keyed token cache.

## Suggested fix directions (for investigation)

1. **Reuse a single long-lived ORAS `Client` / shared `ScopeManager`** across
   operations so learned scopes persist and the cached token is found. This
   should collapse ~1,706 token fetches toward ~7 (one per repo) and remove the
   redundant 401s.
2. **Pre-seed the repo scope** into the `ScopeManager` before the first request
   per repo, so the first attempt carries the token (no 401).
3. Consider interaction with ORAS scope accumulation (scopes are unioned per host
   via `GetScopesStringForHost`); validate that a shared `ScopeManager` does not
   produce an ever-growing scope token that ACR rejects.

These are independent of, and complementary to, bounding the `Task.WhenAll`
parallelism in `CopyAcrImagesCommand` (which separately caps the burst and the
ARM `ImportImage` 429s). The token-cache fix targets the OAuth/401 traffic
specifically.

## Verification approach

Re-run the Publish-only path (or a local `copyAcrImages` invocation) and confirm
from the "Copy Images" log that `oauth2/token` request count drops to roughly the
number of distinct repos, `401` challenges drop correspondingly, and
`oauth2/token` `429`s are eliminated. Use the same log-parsing approach that
produced the evidence above (count `Sending HTTP request` lines per endpoint and
status responses from the `OrasDotNetService` HTTP client logs).

## Constraints / notes

- Follow `.github/instructions/csharp.instructions.md` for any C# changes.
- ImageBuilder is published as a container image and bootstrapped into pipelines;
  see `AGENTS.md` for the two-step change process and local `dotnet run` /
  `dotnet test` workflow.
- No code changes have been made for this issue yet; this is investigation +
  fix work.

## Suggested skills

- `dotnet-inspect` — to inspect the `OrasProject.Oras` package API surface
  (`Client`, `ScopeManager`, `Cache`) when designing the fix.
- `tdd` — add/adjust unit tests in `src/ImageBuilder.Tests` around
  `OrasDotNetService` token/scope reuse.
- `testing-imagebuilder-on-other-repos` — validate the fix end-to-end against a
  consuming repo's Publish stage via the dev registry and unofficial pipeline.
- `creating-pull-requests` — open the PR once the fix is ready.
