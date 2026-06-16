# Reference: testing ImageBuilder on other repos

## Deriving and verifying the Linux-only `--path` set

`imageBuilder.pathArgs` is appended to the `generateBuildMatrix`, `build`, and
`publish` ImageBuilder invocations. Matrix generation hard-codes `--os-type '*'`,
and `--os-type` cannot be supplied twice (System.CommandLine errors with
*"expects a single argument but 2 were provided"*). `--path` accepts repeatable
glob patterns (`*`, `?` only — no negation), matched against each platform's
Dockerfile path, so include every Linux distro family instead of excluding
Windows.

Derive the families from the consumer repo's `manifest.json`:

```powershell
# Distinct OS-version directory segments across all Dockerfile paths
Select-String -Path manifest.json -Pattern '"dockerfile":\s*"([^"]*)"' -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
  ForEach-Object { ($_ -split '/')[-2] } | Sort-Object -Unique
```

Verify your include set matches exactly the non-Windows platforms (everything
not under `nanoserver*` / `windowsservercore*`):

```powershell
$paths = Select-String -Path manifest.json -Pattern '"dockerfile":\s*"([^"]*)"' -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$inc = 'alpine|azurelinux|bookworm|jammy|noble|resolute|ubuntu|aspire-dashboard'
"matched:    " + ($paths | Where-Object { $_ -match $inc }).Count
"not matched (should be Windows only):"
$paths | Where-Object { $_ -notmatch $inc }
```

If anything other than `nanoserver`/`windowsservercore` shows up under
"not matched", add the missing family to both the include set above and the
`pathArgs` value in the pipeline yaml.

You can also dry-run the filter locally against a built ImageBuilder:

```powershell
dotnet build src/ImageBuilder/Microsoft.DotNet.ImageBuilder.csproj
$dll = "artifacts/bin/Microsoft.DotNet.ImageBuilder/Debug/net9.0/Microsoft.DotNet.ImageBuilder.dll"
dotnet $dll generateBuildMatrix --manifest manifest.json --type platformDependencyGraph `
  --os-type '*' --architecture '*' `
  --path '*alpine*' --path '*azurelinux*' --path '*bookworm*' --path '*jammy*' `
  --path '*noble*' --path '*resolute*' --path '*ubuntu*' --path '*aspire-dashboard*'
# grep the output for windows/nanoserver/servercore -> expect zero matches
```

## Gotchas

- **`--os-type linux` fails** in matrix generation (duplicate option, since
  matrix generation already passes `--os-type '*'`). Use `--path` includes
  instead (see above).
- **ACR throttling / HTTP 429 during Publish** — the dev registry
  (`dotnetdockerdev`, Standard SKU) rate-limits the `Copy Images` step on large
  no-cache runs: *"Your request was throttled because it exceeded the limits of
  your registry SKU and tier."* Build/Test can pass while Publish fails for this
  reason alone. Mitigations: temporarily bump the dev ACR to Premium, or re-run
  just the Publish stage. This is unrelated to ImageBuilder code.
- **No Windows ImageBuilder on macOS/arm64** — only Linux amd64/arm64 are built
  and pushed, matching existing dev tags. Always apply the Linux-only filter
  (step 4) so the consumer pipeline doesn't try to use a Windows ImageBuilder.
- **Remember to disable anonymous pull** on the dev ACR once the run completes.

## Useful lookups

```powershell
# Find the consumer pipeline's id
az pipelines list --org https://dev.azure.com/dnceng --project internal `
  --query "[?contains(name,'dotnet-docker-nightly')].{id:id,name:name}" -o table

# Confirm a queued run's branch + template parameters
az devops invoke --org https://dev.azure.com/dnceng --area pipelines --resource runs `
  --route-parameters project=internal pipelineId=<id> runId=<run> `
  --api-version 7.1 --http-method GET --query "templateParameters" -o json
```
