# Image Info Merge Property Testing Recommendations

## Summary

The current property tests are a useful smoke-test suite, but they are not strong enough to prove that the new `ImageArtifactDetails` implementation is compatible with the old implementation for real `mergeImageInfo` workloads.

The main problem is that the generators create arbitrary JSON-shaped image-info trees rather than coherent merge scenarios. Real merge inputs are not arbitrary trees: they are fragments derived from one manifest, split across build legs, then merged back into one image-info file. The properties should generate that same kind of structured world, then randomly vary the important edge cases inside it.

The recommended approach is:

1. Add one simplified real-world golden regression fixture based on `IMAGE_INFO_MERGE_REPRO_SUMMARY.md`.
2. Replace or supplement the current generic generators with scenario generators that build valid manifest-linked merge worlds.
3. Differentially compare old and new merge behavior at the same boundary used by production: JSON input fragments plus manifest linkage, not just in-memory V2 records.
4. Force coverage of known-risk scenario variants instead of hoping a low-probability random tree happens to hit them.

## Why the current properties are insufficient

The migration is trying to replace old mutable image-info models and helper behavior with newer immutable/stateless services. The highest-value property is therefore:

> For every valid `mergeImageInfo` scenario we care about, the new implementation should produce the same observable output as the old implementation.

The current tests do not exercise enough valid `mergeImageInfo` scenarios.

### The generated data is not production-shaped

`ImageInfoGenerators.ImageArtifactDetails` generates:

- 1-3 repos, each with unique names.
- 1-3 images per repo.
- 1-3 platforms per image.
- Independently generated product versions, Dockerfile paths, tags, digests, and manifest metadata.

This is useful for serialization smoke tests, but it does not model a real merge. In production, image-info fragments come from build legs. A leg typically contains a subset of platforms for the same manifest-defined images. Multiple source files may contain the same repo/image identity with different architectures or OS variants, and the merge must combine those fragments into the same image.

The real repro documented in `IMAGE_INFO_MERGE_REPRO_SUMMARY.md` has six per-leg input files that merge into one output with 4 repos, 8 images, and 24 platforms. That shape is much more important than random small trees.

### The differential merge tests avoid the risky matching path

The most important current differential tests are in `MergeMigrationPropertyTests`. They compare old vs new behavior for:

- Non-overlapping repos, achieved by prefixing generated repo names.
- Merging into an empty target.
- One hand-built same-platform shared-tag replacement case.

These are not enough. Non-overlapping repos mostly test concatenation and sorting. Merging into an empty target mostly tests cloning and serialization. The risky behavior is matching existing repos, images, and platforms across multiple source files and optional initial target content.

The old implementation can compare `ImageData` using manifest-linked object identity (`ManifestImage`) after loading image-info with `ImageInfoHelper.LoadFromContent`. The new `ImageInfoMerger` matches images using product-version equivalence plus the first platform identity. A property suite that does not generate manifest-linked overlapping images can miss compatibility breaks in exactly this area.

### The current generators do not deliberately create collisions or near misses

A good merge property suite needs both matches and near misses:

- Same repo/image with different platforms should merge into one image.
- Same platform identity should replace or union selected fields.
- Same platform identity with different tag-presence state should not match.
- Same major/minor product version should match where old behavior matches.
- Different product version, Dockerfile, architecture, OS type, or OS version should not match.
- Build mode and publish mode should handle replaceable lists differently.

The current generators may occasionally create some of these patterns by accident, but the probability is low and the tests do not assert coverage.

## Recommended test architecture

Use two complementary layers of tests.

## 1. Golden regression fixture

Create one small, deterministic test based on `IMAGE_INFO_MERGE_REPRO_SUMMARY.md`.

This should be a distilled version of the real pipeline shape, not a full copy of the downloaded artifact. It should be small enough to understand at a glance while preserving the important merge behavior.

Suggested shape:

- 2 repos, for example `dotnet/nightly/runtime-deps` and `dotnet/nightly/runtime`.
- 2 product versions, for example `8.0.26` and `9.0.15`.
- 3 architecture legs: `amd64`, `arm32`, `arm64`.
- One source image-info file per architecture/version leg, or a similarly small set that still forces multiple files to contribute platforms to the same repo/image.
- Each per-leg file contains one platform per repo/image.
- The expected merged output contains one image per repo/product-version with all architecture platforms combined.

This fixture should catch the production-shaped failure class even if the random property generators are later weakened accidentally.

The assertion should compare old and new output through the same JSON boundary:

1. Serialize each source fragment to JSON.
2. Load old model fragments through `ImageInfoHelper.LoadFromContent(..., manifestInfo, skipManifestValidation: false)`.
3. Load new V2 fragments through `ImageInfoSerializer.Deserialize`.
4. Merge all fragments in a deterministic order.
5. Serialize both outputs.
6. Compare the JSON exactly, or compare normalized JSON if the test intentionally includes fields that are added after `mergeImageInfo`.

For tests that target only `mergeImageInfo`, do not compare against post-`createManifestList` output unless `manifest.digest` and `manifest.created` are normalized away. The repro summary notes that those fields are added later by `createManifestList`.

## 2. Property-generated merge scenarios

The main property suite should generate valid merge scenarios rather than arbitrary image-info trees.

Think of the generator as creating a coherent "world":

```csharp
private sealed record MergeWorld(
    Manifest Manifest,
    IReadOnlyList<RepoSpec> Repos,
    IReadOnlyList<BuildLegSpec> BuildLegs,
    ImageInfoMergeOptions Options,
    V2.ImageArtifactDetails? InitialTarget);

private sealed record RepoSpec(
    string RepoName,
    IReadOnlyList<ImageSpec> Images);

private sealed record ImageSpec(
    string ProductVersion,
    IReadOnlyList<string> SharedTags,
    IReadOnlyList<string> SyndicatedDigests,
    IReadOnlyList<PlatformSpec> Platforms);

private sealed record PlatformSpec(
    string Dockerfile,
    string Architecture,
    string OsType,
    string OsVersion,
    IReadOnlyList<string> SimpleTags,
    string Digest,
    string? BaseImageDigest,
    IReadOnlyList<V2.Layer> Layers,
    string CommitUrl);

private sealed record BuildLegSpec(
    string Name,
    IReadOnlySet<PlatformSpec> IncludedPlatforms);
```

The exact types can differ, but the key idea is that the manifest, old-model inputs, and new-model inputs all derive from the same generated source of truth.

### Generation flow

Use a staged generator:

1. Generate a small manifest-shaped world:
   - repo names;
   - product versions;
   - platform identities;
   - Dockerfile paths;
   - tags;
   - OS/architecture metadata.
2. Derive a `Manifest` from that world using existing manifest test helpers where possible.
3. Split platforms into source fragments that represent build legs.
4. Optionally generate an initial target image-info file.
5. Apply a scenario mutation such as digest replacement, tag movement, stale target content, or publish-mode replacement.
6. Run old and new merge paths and compare output.

This preserves realistic relationships that arbitrary generators currently break. For example, product version should agree with Dockerfile path and tags; platform architecture should agree with the build leg; image-info Dockerfile paths should match manifest platform paths.

### Differential property shape

The core property should look conceptually like this:

```csharp
[Fact]
public void Merge_GeneratedScenario_MatchesOldBehavior()
{
    ImageInfoMergeScenarioGenerators.MergeScenario.Sample(scenario =>
    {
        string oldJson = MergeWithOldImplementation(scenario);
        string newJson = MergeWithNewImplementation(scenario);

        Normalize(newJson).ShouldBe(Normalize(oldJson));
    }, iter: 500);
}
```

`MergeWithOldImplementation` should:

1. Create or load the generated manifest into `ManifestInfo`.
2. Deserialize each source JSON using `ImageInfoHelper.LoadFromContent`.
3. Use `ImageInfoHelper.MergeImageArtifactDetails`.
4. Serialize with `JsonHelper.SerializeObject`.

`MergeWithNewImplementation` should:

1. Deserialize each source JSON using `ImageInfoSerializer.Deserialize`.
2. Use the new merge path.
3. If the new path needs manifest linkage to match old production semantics, build and use `ManifestLinkIndex` or the equivalent new service that production will use.
4. Serialize with `ImageInfoSerializer.Serialize`.

The important point is that both implementations should receive equivalent JSON and manifest context. Do not compare old in-memory objects that were manifest-linked against new in-memory objects that were not.

## Scenario variants to generate

The generator should choose among explicit scenario kinds. Each kind should force the relevant shape, not rely on chance.

### Multi-leg platform aggregation

Purpose: verify that fragments for the same repo/image from different build legs become one image with multiple platforms.

Generate:

- Same repo.
- Same manifest image/product version.
- Multiple source files.
- Each source file contains a different platform for the same image.

Expected behavior:

- Old and new outputs match.
- The merged output has one image for that repo/product-version.
- That image has all platforms.

This is the most important property because it mirrors the real repro.

### Same-platform replacement

Purpose: verify field replacement for matching platforms.

Generate:

- Target contains a platform.
- Source contains the same platform identity.
- Source has different digest, base image digest, created timestamp, commit URL, layers, and optionally `isUnchanged`.

Expected behavior:

- Source scalar fields replace target scalar fields.
- Layers are replaced, not unioned.
- Layer order is preserved.
- Tags follow build-mode or publish-mode rules.

### Build-mode tag union

Purpose: verify non-publish merge semantics.

Generate:

- Matching manifest/platform.
- Target simple tags and source simple tags overlap but are not identical.
- Same for manifest shared tags and syndicated digests.
- `ImageInfoMergeOptions.IsPublish = false`.

Expected behavior:

- String lists are unioned.
- Duplicates are removed.
- Output is sorted according to existing behavior.

### Publish-mode tag replacement

Purpose: verify publish merge semantics.

Generate:

- Same setup as build-mode tag union.
- `ImageInfoMergeOptions.IsPublish = true`.

Expected behavior:

- Replaceable lists come from the source, not the union:
  - platform `SimpleTags`;
  - manifest `SharedTags`;
  - manifest `SyndicatedDigests`.
- Source lists are sorted where existing behavior sorts them.

### Tag-state near miss

Purpose: verify a subtle platform identity rule.

Generate:

- Same Dockerfile, architecture, OS type, OS version, and product version.
- One side has no simple tags.
- The other side has at least one simple tag.

Expected behavior:

- The platforms do not match.
- The merge preserves both entries as distinct platforms or images according to old behavior.

This should be tested because both old and new comparison logic treat tag presence as part of platform identity.

### Product-version equivalence

Purpose: verify version matching compatibility.

Generate cases for:

- Same exact product version.
- Same major/minor but different patch, such as `8.0.26` vs `8.0.420`.
- Different major/minor, such as `8.0.26` vs `9.0.15`.
- Preview/suffix versions, such as `10.0.0-preview.1`.
- Null or omitted product version if that is valid for the relevant image-info shape.

Expected behavior:

- Same exact versions match.
- Same major/minor versions should match only where the old implementation also matches in the manifest-linked scenario.
- Different major/minor versions should not match.
- Preview/suffix behavior should be clarified. The new identity code strips suffixes before comparison, while the old helper parses raw version strings in at least one path. If this is an intentional behavior change, document it and test it as such. If compatibility is required, add a differential property that exposes and resolves the difference.

### Manifest null/non-null replacement

Purpose: verify `ManifestData` merge behavior.

Generate:

- Source manifest null, target manifest non-null.
- Source manifest non-null, target manifest null.
- Both non-null with different digest, created, shared tags, and syndicated digests.

Expected behavior:

- Match old behavior exactly:
  - source null over target non-null clears the target manifest;
  - source non-null over target null copies source manifest;
  - both non-null merge recursively.

### Stale target content in publish mode

Purpose: verify publish scenarios where old content is removed because it is no longer in the manifest.

Generate:

- Initial target contains extra repo/image/platform content.
- Manifest omits that stale content.
- Publish mode is enabled.

Expected behavior:

- Stale content is removed before merge, matching `MergeImageInfoCommand` behavior.
- The failsafe preventing complete deletion is respected.

This may belong in command-level property tests rather than pure `ImageInfoMerger` tests, because stale removal currently lives in `MergeImageInfoCommand`.

### Source order invariance where valid

Purpose: catch unstable ordering and accidental statefulness.

Generate:

- A set of source fragments that should be commutative, such as disjoint platforms for the same image or non-overlapping repos.
- Multiple permutations of source order.

Expected behavior:

- Outputs are identical after canonical serialization.

Do not assert order invariance for scenarios where old behavior is intentionally order-sensitive.

## Generator design details

### Prefer small but dense cases

Good property tests do not need huge data. Prefer small scenarios with high edge density:

- 1-4 repos.
- 1-3 images per repo.
- 1-4 platforms per image.
- 1-4 source fragments.

Then use scenario variants to make sure those small cases include the relevant overlap.

### Generate identities separately from payload fields

Separate platform identity fields from payload fields:

Identity fields:

- repo name;
- product version / manifest image identity;
- Dockerfile;
- architecture;
- OS type;
- OS version;
- tag-presence state.

Payload fields:

- digest;
- base image digest;
- created timestamp;
- commit URL;
- layers;
- simple tag values;
- manifest digest;
- manifest shared tags;
- syndicated digests.

Most merge bugs happen when identity fields say "these should match" but payload fields differ. The generator should make that easy to express.

### Use weighted edge generation

Use weighted choices so edge cases are common:

- 30% exact platform match with changed payload.
- 20% same image with disjoint platforms.
- 15% tag-state mismatch.
- 15% same major/minor product-version variation.
- 10% null/empty optional fields.
- 10% stale publish target content.

The exact weights are not critical. What matters is that important cases are frequent and deliberate.

### Add coverage checks or variant-specific facts

CsCheck does not have to mirror QuickCheck's `classify` API exactly, but the test suite should still guarantee coverage. The simplest approach is to expose separate generators or separate tests per scenario kind:

```csharp
[Fact]
public void Merge_MultiLegPlatformAggregation_MatchesOldBehavior()
{
    ImageInfoMergeScenarioGenerators.MultiLegPlatformAggregation.Sample(AssertOldNewMatch, iter: 500);
}

[Fact]
public void Merge_TagStateNearMiss_MatchesOldBehavior()
{
    ImageInfoMergeScenarioGenerators.TagStateNearMiss.Sample(AssertOldNewMatch, iter: 500);
}
```

This is usually clearer than one mega-generator where failures are harder to interpret.

### Keep shrinking useful

Property-test failures should shrink to readable cases. To help with that:

- Keep the generated world small.
- Use simple names like `repo-a`, `repo-b`, `8.0.1`, `9.0.1`.
- Avoid globally unique random strings unless uniqueness is the behavior under test.
- Prefer deterministic digest/tag templates based on scenario fields.
- Minimize unrelated optional fields in each scenario variant.

## Suggested helper APIs

Add a dedicated generator/helper file rather than growing `ImageInfoGenerators` indefinitely.

Suggested file:

```text
src/ImageBuilder.Tests/Generators/ImageInfoMergeScenarioGenerators.cs
```

Suggested public generators:

```csharp
public static class ImageInfoMergeScenarioGenerators
{
    public static Gen<ImageInfoMergeScenario> MultiLegPlatformAggregation { get; }
    public static Gen<ImageInfoMergeScenario> SamePlatformReplacement { get; }
    public static Gen<ImageInfoMergeScenario> BuildModeTagUnion { get; }
    public static Gen<ImageInfoMergeScenario> PublishModeTagReplacement { get; }
    public static Gen<ImageInfoMergeScenario> TagStateNearMiss { get; }
    public static Gen<ImageInfoMergeScenario> ProductVersionEquivalence { get; }
    public static Gen<ImageInfoMergeScenario> ManifestNullReplacement { get; }
}
```

Suggested scenario record:

```csharp
public sealed record ImageInfoMergeScenario(
    IReadOnlyList<string> SourceJsons,
    string? InitialTargetJson,
    ManifestInfo ManifestInfo,
    ImageInfoMergeOptions Options,
    bool SkipManifestValidation,
    string Description);
```

Keep `SourceJsons` in the scenario so both old and new implementations consume the same serialized contract. This avoids false confidence from comparing two different in-memory construction paths.

Suggested assertion helper:

```csharp
private static void AssertOldNewMatch(ImageInfoMergeScenario scenario)
{
    string oldJson = MergeOldJson(scenario);
    string newJson = MergeNewJson(scenario);

    NormalizeMergeOutput(newJson).ShouldBe(NormalizeMergeOutput(oldJson));
}
```

`NormalizeMergeOutput` should be as small as possible. For pure `mergeImageInfo` tests, exact JSON comparison should usually be possible. Only normalize fields such as manifest `digest` and `created` when intentionally comparing against post-`createManifestList` artifacts.

## Implementation sequence

Implement in this order:

1. **Add the golden repro regression test.**
   - Keep it deterministic.
   - Use a tiny manifest and source fragments.
   - Verify old and new output match.
   - Verify the merged output has the expected repo/image/platform counts.

2. **Extract old/new merge assertion helpers.**
   - Reuse the existing `MergeOldJson` and `MergeNewJson` shape from `MergeMigrationPropertyTests`.
   - Make sure both paths consume JSON and manifest context.

3. **Add the scenario record and one generator.**
   - Start with `MultiLegPlatformAggregation`.
   - This gives the highest confidence because it models the real repro.

4. **Add replacement and tag-mode generators.**
   - `SamePlatformReplacement`.
   - `BuildModeTagUnion`.
   - `PublishModeTagReplacement`.

5. **Add near-miss identity generators.**
   - `TagStateNearMiss`.
   - `ProductVersionEquivalence`.
   - Different Dockerfile/architecture/OS cases if not already covered.

6. **Add null/empty/stale content generators.**
   - Manifest null/non-null replacement.
   - Empty source.
   - Empty target.
   - Publish stale target content if command-level property testing is in scope.

7. **Retire or narrow weak properties.**
   - Keep serialization round-trip tests.
   - Keep simple idempotence tests if useful.
   - Do not treat arbitrary `ImageArtifactDetails` generation as sufficient merge compatibility coverage.

## Example property: multi-leg aggregation

The first high-value property should force this shape:

```text
Manifest:
  repo-a
    image 8.0.1
      src/runtime-deps/8.0/bookworm-slim/amd64/Dockerfile
      src/runtime-deps/8.0/bookworm-slim/arm32v7/Dockerfile
      src/runtime-deps/8.0/bookworm-slim/arm64v8/Dockerfile

Source file 1:
  repo-a / image 8.0.1 / amd64 platform

Source file 2:
  repo-a / image 8.0.1 / arm32 platform

Source file 3:
  repo-a / image 8.0.1 / arm64 platform

Expected merged output:
  repo-a / image 8.0.1 / all three platforms
```

Then randomize:

- repo name;
- version;
- OS version;
- architecture subset;
- source file order;
- digest/layer values;
- tag values;
- whether a second repo or second product version is also present.

This property should fail if the new implementation accidentally emits three separate image entries instead of one image with three platforms.

## What success looks like

After this refactor, the suite should make these statements credible:

- New merge output matches old merge output for production-shaped multi-file merges.
- Matching and non-matching identity rules are characterized by explicit properties.
- Build vs publish mode list behavior is covered by generated overlapping cases.
- Optional/null/empty fields are covered where they affect merge semantics.
- A real-world simplified fixture protects against regression of the observed pipeline shape.
- Failures shrink to small, understandable cases that identify the scenario kind.

The goal is not to generate every possible JSON document. The goal is to generate many small, valid, manifest-linked merge worlds that stress the identity and replacement rules most likely to break service compatibility.
