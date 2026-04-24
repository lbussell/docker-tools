# Plan: Refactor ImageArtifactDetails into Stateless Immutable Data Models

## Goal

Move image-info data (`ImageArtifactDetails`, `RepoData`, `ImageData`, `PlatformData`, `ManifestData`, `Layer`) to `ImageBuilder.Models` as stateless, immutable records with no Newtonsoft.Json attributes and no manifest ViewModel references. Move behavior into services/query classes in `ImageBuilder`, then migrate commands and remove the old mutable model classes.

## Completed Foundation

The initial infrastructure is in place:

- Added CsCheck to `ImageBuilder.Tests`.
- Added initial image-info generators and property/differential tests for serialization, merge, identity, linking, query, serializer migration, merger migration, and `PlatformDataBuilder`.
- Added temporary V2 records in `src/ImageBuilder.Models/Image/` under `Microsoft.DotNet.ImageBuilder.Models.Image.V2`.
- Added service-layer replacements in `ImageBuilder`:
  - `ImageInfoIdentity`
  - `ImageInfoSerializer`
  - `ManifestLinkIndex`
  - `ImageInfoMerger`
  - `ImageInfoQueryService`
  - `PlatformDataBuilder`
- Verified the foundation with the current `ImageBuilder.Tests` suite.

Production commands still use the old mutable image-info classes in `src/ImageBuilder/Models/Image/`. The old classes and namespace remain until command migration is complete.

## Key Decisions

### Testing terminology and scope

Old-vs-new comparisons are primarily **differential/characterization property tests**, not strict metamorphic tests. They are useful when the equivalence boundary is externally observable, such as `(manifest, sourceJson, targetJson, options) => mergedJson`.

Use CsCheck's direct metamorphic APIs only where there are genuinely two different operation paths on the same initial state that should converge, such as valid merge-order or normalization invariants. Do not add broad generated tests that are only hardcoded change detectors; every generator and property should have an explicit scenario, contract, and reason to exist.

### Serializer and schema scope

The image-info serialization contract must preserve:

- camelCase property names
- required fields always serialized
- optional empty lists omitted
- optional null values omitted
- `schemaVersion` serialized as `"2.0"`

Schema v1 layer migration is considered dead code. `SchemaVersion2LayerConverter` and the `CanReadJsonSchemaVersion1` test can be removed as part of final cleanup.

### Namespace and dependency scope

The temporary V2 namespace avoids collisions while old and new models coexist. After command migration, delete the old model classes and rename `Microsoft.DotNet.ImageBuilder.Models.Image.V2` to the canonical `Microsoft.DotNet.ImageBuilder.Models.Image`.

The image-info records should not depend on Newtonsoft.Json. Removing the Newtonsoft.Json package from `ImageBuilder.Models` is out of scope because existing manifest models still use Newtonsoft attributes.

### Identity and manifest linking

New code should use stable keys instead of mutable object references:

- repo key: repo name
- platform key: Dockerfile, architecture, OS type, OS version, and optionally normalized product version
- image identity: product-version equivalence plus representative platform identity

`ManifestLinkIndex` replaces `[JsonIgnore]` ViewModel references. Old merge behavior often requires manifest-linked state, so old-vs-new merge parity tests for overlapping images/platforms should include the manifest in the test boundary.

### Mutation points

Immutable records are the public data model, but some commands still need internal mutation while building results:

- `BuildCommand` accumulates platform digest, creation time, layers, base digest, and unchanged state. Use `PlatformDataBuilder`.
- `CreateManifestListCommand` updates manifest digest, shared tags, syndicated digests, and creation time with immutable record replacement.
- `MergeImageInfoCommand` should use `ImageInfoMerger` instead of mutating target graphs directly.

## Remaining Work

### Step 1: Audit and Redesign Generators and Property Tests

**Goal**: Re-evaluate every generator and generated test before command migration. Keep tests that prove meaningful behavior, refactor tests whose boundary is wrong, and remove tests that only act as brittle change detectors.

**Tasks**:

- Audit each generator and property test. For each one, record whether it is an invariant, round-trip, differential/characterization, model-based, or true metamorphic test.
- Replace misleading "metamorphic" terminology where the test is really differential old-vs-new parity.
- Convert hand-picked version identity cases into generated properties where they prove a general rule. Keep hand-picked cases only when they document named, intentional behavior.
- Add or refactor generator families by purpose:
  - **Production-shaped generators** for broad serialization/query coverage.
  - **Edge-biased generators** for empty repos/images/platforms, null optional old-model fields, empty vs non-empty lists, duplicate values, unsorted values, weird-but-valid tags, identity collisions, and empty platform lists.
  - **Linked manifest/image-info generators** for old behavior requiring `ManifestInfo`, `ManifestImage`, `ManifestRepo`, `ImageInfo`, and `PlatformInfo` references.
  - **Merge scenario generators** that generate `(manifest, sourceJson, targetJson, options)` or equivalent structured inputs targeted at one merge semantic at a time.
- Fix `ImageInfoMerger.CompareFirstPlatforms` for empty-platform images using red-green TDD before relying on the merger for command migration.

**Merge scenarios to bias toward**:

- Empty source and target lists.
- Images with empty platform lists.
- Non-overlapping repos, images, and platforms.
- Overlapping repo + image + platform where scalar source values replace target values.
- Source `ManifestData` null clearing target manifest data.
- Source manifest data replacing or merging target manifest data.
- Build-mode string list union/sort vs publish-mode replace/sort for replaceable lists.
- Empty source string lists leaving target lists unchanged in build mode.
- Layer replacement preserving source layer order.
- Platform tag-state mismatch preventing a platform match even when structural identity matches.
- Product-version major/minor equivalence controlling image/platform matching.
- First-platform-based image identity, including empty-platform images and platform order changes.
- Shared-tag moves between images, especially in publish mode.

**Files**:

- `src/ImageBuilder.Tests/Generators/ImageInfoGenerators.cs`
- `src/ImageBuilder.Tests/PropertyTests/*.cs`
- `src/ImageBuilder/Services/ImageInfoMerger.cs`

### Step 2: Migrate Commands

**Goal**: Update production commands to use the new records and services after the generator/property audit is complete.

**Migration order**:

1. `TrimUnchangedPlatformsCommand` - deserialize, filter, serialize.
2. `SignImagesCommand` - deserialize, apply registry override, get digests.
3. `VerifySignaturesCommand` - deserialize, apply registry override, get image references.
4. `GenerateEolAnnotationDataForPublishCommand` - deserialize, compare old/new, get digests.
5. `PullImagesCommand` - load and iterate.
6. `IngestKustoImageInfoCommand` - load and iterate for Kusto ingestion.
7. `PostPublishNotificationCommand` - load and read tags/digests.
8. `GetStaleImagesCommand` - load and match platforms via `ManifestLinkIndex`.
9. `GenerateBuildMatrixCommand` - load and match platforms for cache checking.
10. `CopyAcrImagesCommand` - load and use `ManifestLinkIndex` for shared tags.
11. `WaitForMcrImageIngestionCommand` - load and use `ManifestLinkIndex`.
12. `CreateManifestListCommand` - load, use `ManifestLinkIndex`, and produce updated immutable records.
13. `MergeImageInfoCommand` - use `ImageInfoMerger` for merge behavior.
14. `BuildCommand` - use `PlatformDataBuilder` to create immutable image-info output.

Existing command tests should cover command wiring. Service-level property/differential tests should cover core serializer, merge, linking, query, and identity behavior.

### Step 3: Update Test Infrastructure

**Goal**: Move test helpers and existing unit tests onto the new record types once production commands no longer depend on the old mutable classes.

**Tasks**:

- Update image-info test helpers to create V2/new record types directly.
- Consolidate repeated old-to-V2 conversion helpers or remove them if JSON-boundary tests replace them.
- Update serialization helpers to work with new records.
- Update existing unit tests to reference new types.
- Ensure the full test suite passes after each major test-infrastructure change.

### Step 4: Remove Old Classes and Finalize Namespace

**Goal**: Delete old mutable image-info classes and make the new records the canonical model.

**Tasks**:

- Remove old classes from `src/ImageBuilder/Models/Image/`.
- Remove `[JsonIgnore]` ViewModel properties and behavior methods from the image-info model surface.
- Remove schema v1 migration code and obsolete tests.
- Rename `Microsoft.DotNet.ImageBuilder.Models.Image.V2` to `Microsoft.DotNet.ImageBuilder.Models.Image`.
- Clean up unused Newtonsoft.Json imports tied to image-info models.
- Verify the full test suite passes.

### Future: Remove Newtonsoft.Json from ImageBuilder.Models

This is out of scope for the image-info refactor. Existing manifest models in `ImageBuilder.Models/Manifest/*.cs` still use Newtonsoft.Json attributes, so removing the package requires a separate manifest-model serialization migration.

## Risks and Considerations

- Old `PlatformData.CompareTo` is not a lawful total order; it returns `1` for tag-state mismatch and behaves more like a match/no-match predicate in merge code.
- Old `ImageData.CompareTo` can throw when `ManifestImage` is not linked, so old-vs-new overlapping merge parity needs linked manifest state.
- The new `ImageInfoMerger.CompareFirstPlatforms` currently needs an empty-platform-image fix before further migration depends on it.
- Immutable data records are safe as outputs, but commands that accumulate data over time need explicit builders or carefully scoped record replacement.
- Keep generated tests scenario-driven. More generated tests are not automatically better if they do not exercise meaningful contracts.
