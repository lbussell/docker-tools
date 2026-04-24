# Plan: Refactor ImageArtifactDetails into Stateless Immutable Data Models

## Progress

**Steps 1-10 COMPLETE** ✅ (10 commits on `imageinfo-refactor` branch)

New infrastructure built and verified with 54 property/metamorphic tests:
- CsCheck generators for all image-info model types
- Baseline property tests for serialization, merge, and identity behavior
- V2 immutable records in `ImageBuilder.Models/Image/` (namespace `V2`)
- `ImageInfoIdentity` — canonical key generation
- `ImageInfoSerializer` — V2 serialization (proven identical to old)
- `ManifestLinkIndex` — key-based manifest linking
- `ImageInfoMerger` — explicit type-safe merge (proven identical to old)
- `ImageInfoQueryService` — digest/query operations (proven identical to old)
- `PlatformDataBuilder` — mutable accumulator for BuildCommand

**Remaining**: Steps 11-13 (Migrate Commands, Update Tests, Remove Old Classes)

## Problem Statement

`ImageArtifactDetails` and its related image-info model classes (`RepoData`, `ImageData`, `PlatformData`, `ManifestData`, `Layer`) currently live in `src/ImageBuilder/Models/Image/` and are **tightly coupled** to:

1. **Newtonsoft.Json** — via `[JsonProperty]`, `[JsonIgnore]`, `[JsonConverter]` attributes and direct `JsonConvert` calls
2. **Manifest ViewModel types** — `[JsonIgnore]` properties (`ManifestImage`, `ManifestRepo`, `ImageInfo`, `PlatformInfo`) create runtime cross-references to ViewModel objects
3. **Behavior** — `IComparable` implementations, `GetIdentifier()`, `HasDifferentTagState()`, `FromPlatformInfo()`, `AllTags` computed property, `FromJson()` static factory with schema migration
4. **Reflection-based merge logic** — `ImageInfoHelper.MergeData()` uses reflection over properties, skipping `[JsonIgnore]`-annotated ones

The goal is to make these models **stateless, immutable records** in the `ImageBuilder.Models` project with **no Newtonsoft.Json dependency**, moving all behavior to services and query/lookup classes in the `ImageBuilder` project.

## Current State

### Model Classes (all in `src/ImageBuilder/Models/Image/`)

| Class | Behavior | Newtonsoft Attributes | ViewModel Cross-Refs |
|---|---|---|---|
| `ImageArtifactDetails` | `FromJson()` static factory, nested `SchemaVersion2LayerConverter` | None on properties | None |
| `RepoData` | `IComparable<RepoData>.CompareTo()` | `[JsonProperty(Required)]` on `Repo` | None |
| `ImageData` | `IComparable<ImageData>.CompareTo()` (requires `ManifestImage`) | `[JsonProperty(NullValueHandling)]` × 2, `[JsonIgnore]` × 2 | `ManifestImage`, `ManifestRepo` |
| `PlatformData` | `IComparable.CompareTo()`, `GetIdentifier()`, `HasDifferentTagState()`, `FromPlatformInfo()`, `AllTags` | `[JsonProperty(Required)]` × 6, `[JsonProperty(NullValueHandling)]` × 1, `[JsonProperty(DefaultValueHandling)]` × 1, `[JsonIgnore]` × 3 | `ImageInfo`, `PlatformInfo` |
| `ManifestData` | None ✅ | None ✅ | None ✅ |
| `Layer` | None ✅ (already a record) | None ✅ | None ✅ |

### Key Behavioral Responsibilities to Extract

1. **Serialization / Deserialization with schema migration**
   - `ImageArtifactDetails.FromJson()` + `SchemaVersion2LayerConverter` (v1→v2 Layer migration)
   - `JsonHelper.SerializeObject()` with `CustomContractResolver` (camelCase, required-property handling, empty-list skipping)
   - Newtonsoft attributes on model properties control serialization behavior

2. **Deep merge logic** (`ImageInfoHelper.MergeImageArtifactDetails()`)
   - Reflection-based: iterates properties, checks types, skips `[JsonIgnore]`
   - Different behavior for build vs publish (`ImageInfoMergeOptions.IsPublish`)
   - Uses `IComparable<T>` to match source/target items in lists
   - Special handling for string lists (merge vs replace), Layers (always replace), dictionaries

3. **IComparable implementations** (used by merge to match items)
   - `RepoData.CompareTo()` — compares by `Repo` name
   - `ImageData.CompareTo()` — compares by `ManifestImage` reference, then `ProductVersion`, then first platform (**requires ManifestImage to be set**)
   - `PlatformData.CompareTo()` — compares by tag state + identifier string

4. **PlatformData behavior**
   - `GetIdentifier()` — `{Dockerfile}-{Architecture}-{OsType}-{OsVersion}[-{MajorMinorVersion}]` (accesses `ImageInfo` for version)
   - `HasDifferentTagState()` — checks if SimpleTags emptiness differs
   - `FromPlatformInfo()` — factory creating PlatformData from ViewModel types
   - `AllTags` — computed property combining `ImageInfo.SharedTags` + `PlatformInfo.Tags`

5. **Manifest linking** (`ImageInfoHelper.LoadFromContent()`)
   - After deserialization, walks tree and sets `[JsonIgnore]` ViewModel references
   - Uses `ArePlatformsEqual()` to match platforms to manifest definitions
   - `ArePlatformsEqual()` creates a temporary `PlatformData` via `FromPlatformInfo()` and compares identifiers

6. **Extension methods on ImageArtifactDetails** (`ImageInfoHelper`)
   - `GetAllDigests()` — collects all platform + manifest list digests
   - `GetAllImageDigestInfos()` — collects digest+tags+isManifestList tuples
   - `ApplyRegistryOverride()` — rewrites digests with registry prefix
   - `GetMatchingPlatformData()` — finds PlatformData by PlatformInfo reference

### Consumers (Commands)

| Command | Uses |
|---|---|
| `BuildCommand` | Creates `ImageArtifactDetails`, populates platform data, serializes to file |
| `MergeImageInfoCommand` | Loads multiple files, merges, applies commit overrides, removes stale content |
| `CreateManifestListCommand` | Loads, reads `ManifestImage`/`ManifestRepo`, updates manifest digests |
| `CopyAcrImagesCommand` | Loads, reads digests and `ManifestImage.SharedTags` |
| `WaitForMcrImageIngestionCommand` | Loads, reads digests and syndicated tags via `ManifestImage` |
| `GetStaleImagesCommand` | Loads, uses `GetMatchingPlatformData()`, compares layers/digests |
| `GenerateBuildMatrixCommand` | Loads, uses `GetMatchingPlatformData()` for cache checking |
| `TrimUnchangedPlatformsCommand` | Deserializes directly, removes `IsUnchanged` platforms, re-serializes |
| `SignImagesCommand` | Deserializes, applies registry override, gets all digests |
| `VerifySignaturesCommand` | Deserializes, applies registry override, gets all image references |
| `PostPublishNotificationCommand` | Loads, reads tags and digests |
| `IngestKustoImageInfoCommand` | Loads, iterates repos/images/platforms for Kusto ingestion |
| `GenerateEolAnnotationDataForPublishCommand` | Deserializes two files, compares old vs new, gets digests |
| `PublishImageInfoCommand` | Merges into published version |

## Key Design Decisions (Resolved)

### Namespace Coexistence Strategy
**Decision: Temporary `ImageInfoV2` namespace during migration.**

New record types will live in `Microsoft.DotNet.ImageBuilder.Models.ImageInfoV2` (in the `ImageBuilder.Models` project) while old types remain in `Microsoft.DotNet.ImageBuilder.Models.Image` (in the `ImageBuilder` project). After all consumers are migrated and old types deleted, rename the namespace to the canonical `Microsoft.DotNet.ImageBuilder.Models.Image`. This avoids compiler ambiguity and allows incremental migration.

### Identity Model (Key-Based, Not Object-Based)
**Decision: Stable identity keys for linking, not object references.**

Instead of keying lookups by record object identity (which breaks with `with` expressions creating new instances), define stable keys:
- `RepoKey` = repo name (string)
- `PlatformKey` = `{Dockerfile}-{Architecture}-{OsType}-{OsVersion}-{NormalizedProductVersion}-{TagState}` (the same components as `GetIdentifier()`)
- `ImageKey` = derived from product version + representative platform key

The `ManifestLinkIndex` and merger both use these keys. This is the **heart of the migration** — getting identity right enables everything else.

### BuildCommand Accumulation Strategy
**Decision: Mutable builder/accumulator within ImageBuilder, materialized to immutable records.**

`BuildCommand` mutates platform data across multiple passes (digest after push, layers after inspection, etc.). Rather than chaining nested `with` expressions, use a mutable `PlatformDataBuilder` internally that materializes to an immutable `PlatformData` record once all data is collected. Only ImageBuilder uses the builder; all external consumers see immutable records.

### Newtonsoft.Json Removal Scope
**Decision: Split into image-info records (immediate) and manifest models (deferred).**

New image-info records have zero serialization attributes. Removing `Newtonsoft.Json` from `ImageBuilder.Models.csproj` is **deferred** until the existing Manifest models (`Image.cs`, `Platform.cs`, etc.) are also migrated — that's a separate effort. The image-info records simply don't use any Newtonsoft types, even though the project-level dependency may remain temporarily.

### Schema v1 Layer Migration Is Dead Code
The `SchemaVersion2LayerConverter` (converts string-based layers to `Layer` records) and the `CanReadJsonSchemaVersion1` test can be **removed** as part of this refactor. All real-world image-info.json files have been fully migrated to schema v2. This simplifies the new serializer — no backward-compatibility converter needed.

### Serialization Contract
The following serialization behaviors are **contractual** (must be preserved):
- camelCase property names
- Required fields (`Repo`, `Dockerfile`, `Digest`, etc.) are always serialized even when default/empty
- Optional/empty lists are omitted from output
- `NullValueHandling.Ignore` for optional nullable properties
- `SchemaVersion` always outputs `"2.0"`

### Metamorphic Test Scope
**Decision: Focus metamorphic coverage at the service layer, not per-command.**

Put most CsCheck coverage on `serializer`, `merger`, `linker`, and `query` services. Command tests focus on wiring/adaptation (existing example-based tests suffice for command logic). This avoids scope explosion.

## Target Architecture

### Phase 1: Establish Property Testing Infrastructure

Add CsCheck to the test project and create generators for all image-info model types **including linked-state scenarios** (ManifestInfo + ImageArtifactDetails pairs). Write baseline metamorphic tests that capture current serialization, merge, and comparison behavior before making any changes.

### Phase 2: Design Identity Model and Create New Records

Define stable identity keys, then create new record types in `ImageBuilder.Models` (under temporary `ImageInfoV2` namespace) that are:
- Immutable (`init` properties, `IReadOnlyList`)
- Free of serialization attributes
- Free of ViewModel cross-references
- Free of behavior methods

### Phase 3: Create Services to Replace Extracted Behavior

Create service classes in `ImageBuilder` that operate on the new models:
- **ImageInfoSerializer** — handles JSON serialization/deserialization with schema migration
- **ImageInfoIdentity** — canonical key generation, platform matching
- **ImageInfoMerger** — explicit (non-reflection) merge logic using identity keys
- **ManifestLinkIndex** — key-based lookup mapping image-info ↔ manifest
- **ImageInfoQueryService** — `GetAllDigests()`, `GetMatchingPlatform()`, `ApplyRegistryOverride()`, etc.
- **PlatformDataBuilder** — mutable accumulator for BuildCommand

### Phase 4: Migrate Commands to Use New Models + Services

Update each command to use the new records and services. Example-based tests suffice for command wiring.

### Phase 5: Cleanup

Remove old mutable classes, rename `ImageInfoV2` → canonical namespace.

---

## Implementation Steps (Ordered)

### Step 1: Add CsCheck and Create Generators

**Goal**: Establish property testing infrastructure with generators for all model types, **including linked-state scenarios**.

**Tasks**:
- Add `CsCheck` NuGet package to `ImageBuilder.Tests` project
- Create `Gen` (generator) helpers for:
  - `Layer` (random digest string + size)
  - `ManifestData` (optional digest, shared tags, syndicated digests, created date)
  - `PlatformData` (valid dockerfile path, digest, osType, osVersion, architecture, tags, layers, etc.)
  - `ImageData` (product version, optional manifest, list of platforms)
  - `RepoData` (repo name, list of images)
  - `ImageArtifactDetails` (list of repos)
- Create **linked-state generators** that produce `(ManifestInfo, ImageArtifactDetails)` pairs where the image-info has been through `LoadFromContent` (manifest linking is done)
- Generators for edge cases: empty lists, single-item collections, repos with removed platforms, publish vs build merge scenarios, shared-tag moves between images
- Generators should produce realistic data (valid SHA digests, plausible paths, consistent dockerfile/arch/os combinations)

**Files**:
- `src/ImageBuilder.Tests/Microsoft.DotNet.ImageBuilder.Tests.csproj` (add CsCheck reference)
- `src/ImageBuilder.Tests/Generators/ImageInfoGenerators.cs` (new)

### Step 2: Write Baseline Metamorphic Tests for Serialization

**Goal**: Lock down current serialization round-trip behavior before any changes.

**Metamorphic Properties**:
- **Round-trip**: For any `ImageArtifactDetails`, `FromJson(SerializeObject(x))` produces semantically identical output
- **Deterministic output**: Serializing the same object twice produces identical JSON
- **Deserialization validation**: Deserialization rejects JSON missing required properties

**Files**:
- `src/ImageBuilder.Tests/PropertyTests/SerializationPropertyTests.cs` (new)

### Step 3: Write Baseline Metamorphic Tests for Merge Logic

**Goal**: Lock down current merge behavior.

**Metamorphic Properties**:
- **Identity merge**: Merging `x` into empty target yields `x` equivalent
- **Idempotency**: Merging `x` into `x` yields `x`
- **Commutativity of non-overlapping**: Merging non-overlapping repos from `a` and `b` is order-independent
- **Tag merge vs replace**: Build mode merges tags (union); publish mode replaces tags
- **Layer replace**: Layers are always replaced, never merged
- **Sort invariant**: Output repos/images/platforms are always sorted

**Note**: Merge tests require linked-state data (ManifestImage must be set for ImageData.CompareTo). Use the linked-state generators from Step 1.

**Files**:
- `src/ImageBuilder.Tests/PropertyTests/MergePropertyTests.cs` (new)

### Step 4: Write Baseline Tests for Identity and Platform Matching

**Goal**: Lock down `GetIdentifier`, `HasDifferentTagState`, and platform matching — the semantics actually used by merge/linking, not abstract ordering laws.

**Properties to test** (adjusted for actual behavior — `CompareTo` is a match/no-match check, not a total order):
- **GetIdentifier determinism**: Same inputs always produce same identifier
- **GetIdentifier component sensitivity**: Changing any component (dockerfile, arch, os, osVersion) changes the identifier
- **HasDifferentTagState symmetry**: `a.HasDifferentTagState(b) == b.HasDifferentTagState(a)`
- **Platform match/no-match**: Two platforms from the same manifest entry compare as 0; different entries compare as non-zero
- **ArePlatformsEqual consistency**: Matches are consistent with GetIdentifier + product version normalization
- **Deterministic sort output**: Sorting a list of platforms by CompareTo produces deterministic ordering

**Files**:
- `src/ImageBuilder.Tests/PropertyTests/IdentityPropertyTests.cs` (new)

### Step 5: Design Identity Model and Create New Records

**Goal**: Define stable identity keys and the target immutable data model.

**Part A — Identity Keys**:
Define canonical key types/functions:
```csharp
// Key generation functions (static, pure)
public static class ImageInfoIdentity
{
    public static string GetPlatformKey(string dockerfile, string architecture,
        string osType, string osVersion, string? productVersion = null) => ...;
    public static string GetRepoKey(string repoName) => repoName;
    public static bool HasDifferentTagState(IReadOnlyList<string> tagsA, IReadOnlyList<string> tagsB) => ...;
    public static bool AreProductVersionsEquivalent(string? v1, string? v2) => ...;
}
```

**Part B — Record types** (in `ImageBuilder.Models/Image/`, namespace `Microsoft.DotNet.ImageBuilder.Models.ImageInfoV2`):
```csharp
public record ImageArtifactDetails { ... }
public record RepoData { ... }
public record ImageData { ... }
public record PlatformData { ... }
public record ManifestData { ... }
public record Layer(string Digest, long Size);
```
(Full definitions as in the record types section below, with `init` properties and `IReadOnlyList`)

**Metamorphic test**: For any old PlatformData, `ImageInfoIdentity.GetPlatformKey(...)` produces the same string as the old `GetIdentifier()`.

**Files**:
- `src/ImageBuilder/Services/ImageInfoIdentity.cs` (new)
- `src/ImageBuilder.Models/Image/ImageArtifactDetails.cs` (new)
- `src/ImageBuilder.Models/Image/RepoData.cs` (new)
- `src/ImageBuilder.Models/Image/ImageData.cs` (new)
- `src/ImageBuilder.Models/Image/PlatformData.cs` (new)
- `src/ImageBuilder.Models/Image/ManifestData.cs` (new)
- `src/ImageBuilder.Models/Image/Layer.cs` (new)
- `src/ImageBuilder.Tests/PropertyTests/IdentityMigrationPropertyTests.cs` (new)

### Step 6: Create ImageInfoSerializer Service

**Goal**: Centralize serialization/deserialization with schema migration.

**Responsibilities**:
- Deserialize JSON to new `ImageArtifactDetails` records
- Serialize new `ImageArtifactDetails` to JSON preserving current formatting rules
- Can use Newtonsoft.Json internally (dependency stays in ImageBuilder, not Models)
- **No schema v1 migration needed** — `SchemaVersion2LayerConverter` is dead code and will be removed

**Metamorphic test**: For any `ImageArtifactDetails` generated by CsCheck, `NewSerializer.Deserialize(OldSerializer.Serialize(x))` produces equivalent output, and vice versa.

**Files**:
- `src/ImageBuilder/Services/ImageInfoSerializer.cs` (new)
- `src/ImageBuilder.Tests/PropertyTests/SerializerMigrationPropertyTests.cs` (new)

### Step 7: Create ManifestLinkIndex (Key-Based)

**Goal**: Replace mutable `[JsonIgnore]` ViewModel references with a key-based lookup structure that survives `with`-based record updates.

**Design**: Uses identity keys from Step 5, not object references.

```csharp
public class ManifestLinkIndex
{
    // Build from image-info + manifest
    public static ManifestLinkIndex Create(ImageArtifactDetails details, ManifestInfo manifest);

    // Key-based lookups
    public ImageInfo? GetManifestImage(string platformKey);
    public RepoInfo? GetManifestRepo(string repoKey);
    public PlatformInfo? GetPlatformInfo(string platformKey);
}
```

**Metamorphic test**: For any image-info loaded with manifest, key-based lookups return the same objects as the old `[JsonIgnore]` properties.

**Files**:
- `src/ImageBuilder/Services/ManifestLinkIndex.cs` (new)
- `src/ImageBuilder.Tests/PropertyTests/ManifestLinkingPropertyTests.cs` (new)

### Step 8: Create Explicit (Non-Reflection) Merge Logic

**Goal**: Replace the reflection-based `MergeData()` with explicit, type-safe merge logic using identity keys.

**Design**: The new merger operates on immutable records and returns new instances.

```csharp
public class ImageInfoMerger
{
    public ImageArtifactDetails Merge(
        ImageArtifactDetails source,
        ImageArtifactDetails target,
        ImageInfoMergeOptions options);
}
```

Uses `ImageInfoIdentity` keys to match source/target items (instead of `IComparable`). Does not require manifest linking.

**Metamorphic test**: For any two `ImageArtifactDetails` objects and merge options, `NewMerger.Merge(a, b, opts)` serializes identically to the result of old merge logic.

**Files**:
- `src/ImageBuilder/Services/ImageInfoMerger.cs` (new)
- `src/ImageBuilder.Tests/PropertyTests/MergeMigrationPropertyTests.cs` (new)

### Step 9: Create ImageInfoQueryService

**Goal**: Centralize query/lookup operations currently scattered as extension methods.

**Methods**:
- `GetAllDigests(ImageArtifactDetails)` → `List<string>`
- `GetAllImageDigestInfos(ImageArtifactDetails)` → `List<ImageDigestInfo>`
- `ApplyRegistryOverride(ImageArtifactDetails, RegistryOptions)` → `ImageArtifactDetails` (returns new instance)
- `GetMatchingPlatformData(string platformKey, string repoKey, ImageArtifactDetails)` → `(PlatformData, ImageData)?`

**Metamorphic test**: For any ImageArtifactDetails, new query methods return same results as old extension methods.

**Files**:
- `src/ImageBuilder/Services/ImageInfoQueryService.cs` (new)
- `src/ImageBuilder.Tests/PropertyTests/QueryMigrationPropertyTests.cs` (new)

### Step 10: Create PlatformDataBuilder

**Goal**: Provide a mutable accumulator for `BuildCommand` that materializes to immutable records.

**Design**:
```csharp
public class PlatformDataBuilder
{
    // Set fields incrementally (as BuildCommand does today)
    public PlatformDataBuilder SetDigest(string digest);
    public PlatformDataBuilder SetCreated(DateTime created);
    public PlatformDataBuilder SetLayers(IReadOnlyList<Layer> layers);
    public PlatformDataBuilder SetBaseImageDigest(string? digest);
    public PlatformDataBuilder SetIsUnchanged(bool isUnchanged);

    // Materialize to immutable record
    public PlatformData Build();
}
```

**Files**:
- `src/ImageBuilder/Services/PlatformDataBuilder.cs` (new)

### Step 11: Migrate Commands (One by One)

**Goal**: Update each command to use new records + services. Order by dependency (least coupled first).

**Migration order**:
1. `TrimUnchangedPlatformsCommand` — simplest, just deserializes/filters/reserializes
2. `SignImagesCommand` — deserialize + registry override + get digests
3. `VerifySignaturesCommand` — similar to SignImages
4. `GenerateEolAnnotationDataForPublishCommand` — deserialize + compare + get digests
5. `PullImagesCommand` — load + iterate
6. `IngestKustoImageInfoCommand` — load + iterate
7. `PostPublishNotificationCommand` — load + read tags/digests
8. `GetStaleImagesCommand` — load + platform matching via ManifestLinkIndex
9. `GenerateBuildMatrixCommand` — load + platform matching
10. `CopyAcrImagesCommand` — load + ManifestLinkIndex for shared tags
11. `WaitForMcrImageIngestionCommand` — load + ManifestLinkIndex
12. `CreateManifestListCommand` — load + ManifestLinkIndex + produce updated records
13. `MergeImageInfoCommand` — uses merge logic extensively
14. `BuildCommand` — uses PlatformDataBuilder to create ImageArtifactDetails from scratch

**Testing**: Existing example-based command tests are sufficient. Metamorphic coverage is at the service layer.

### Step 12: Update Test Infrastructure

**Goal**: Update test helpers and existing tests to use new record types.

**Tasks**:
- Update `ImageInfoHelper` test helper to create new record types
- Update `SerializationHelper` to work with new records
- Update all existing unit tests to reference new types
- Ensure all existing tests still pass

### Step 13: Remove Old Classes and Rename Namespace

**Goal**: Delete the old mutable classes and finalize namespaces.

**Tasks**:
- Remove old classes from `src/ImageBuilder/Models/Image/`
- Remove `[JsonIgnore]` ViewModel properties (replaced by `ManifestLinkIndex`)
- Rename `Microsoft.DotNet.ImageBuilder.Models.ImageInfoV2` → `Microsoft.DotNet.ImageBuilder.Models.Image`
- Clean up unused Newtonsoft.Json imports
- Verify all tests pass

### Step 14 (Future): Remove Newtonsoft.Json from ImageBuilder.Models

**Goal**: Deferred — separate effort to migrate Manifest models away from Newtonsoft.Json.

This step is **out of scope** for the image-info refactor. It requires migrating the Manifest model serialization in `ImageBuilder.Models/Manifest/*.cs` which is a distinct concern. The Newtonsoft.Json PackageReference will remain in `ImageBuilder.Models.csproj` until that work is done.

---

## Key Risks and Considerations

### IComparable Is Not a Total Order
`PlatformData.CompareTo` returns `1` when tag states differ — it's a match/no-match check, not a proper total ordering. Property tests must test the semantics actually used (matching, deterministic sort output) rather than generic ordering laws like transitivity.

### Merge Requires Linked State (Currently)
`ImageData.CompareTo()` throws `InvalidOperationException` if `ManifestImage` is null. The current merge only works after manifest linking. The new explicit merger avoids this by using identity keys instead of `IComparable`, enabling merge without manifest linking.

### Immutability and Mutation Points
Several commands mutate image-info objects after creation:
- `BuildCommand` sets `Digest`, `Created`, `Layers`, `BaseImageDigest`, `IsUnchanged` on `PlatformData` across multiple passes
- `CreateManifestListCommand` sets `ManifestData.Digest`, `SharedTags`, `SyndicatedDigests`, `Created`
- `MergeImageInfoCommand` mutates the target during merge

The `PlatformDataBuilder` (Step 10) addresses BuildCommand. Other commands will use `with` expressions on immutable records.

### Newtonsoft.Json in Manifest Models (Out of Scope)
The existing Manifest models in `ImageBuilder.Models/Manifest/` use Newtonsoft.Json attributes (`[JsonProperty]`, `[JsonConverter(typeof(StringEnumConverter))]`). Migrating those is a separate effort. The Newtonsoft.Json PackageReference stays in `ImageBuilder.Models.csproj` for now — only the new image-info records are attribute-free.

### Test Coverage Gap
Current tests are all example-based. Property tests will dramatically increase coverage of edge cases (empty lists, null values, single-item collections, very large structures, etc.).

## Dependencies Between Steps

```
Step 1 (CsCheck + Generators)
├── Step 2 (Serialization baseline tests)
├── Step 3 (Merge baseline tests)  [needs linked-state generators]
└── Step 4 (Identity baseline tests)
    │
Step 5 (Identity model + New records) ← independent of baseline tests
    │
Step 6 (ImageInfoSerializer) ← depends on Step 5, Step 2
Step 7 (ManifestLinkIndex) ← depends on Step 5
Step 8 (Explicit Merger) ← depends on Step 5, Step 7, Step 3
Step 9 (QueryService) ← depends on Step 5, Step 7
Step 10 (PlatformDataBuilder) ← depends on Step 5
    │
Step 11 (Migrate Commands) ← depends on Steps 6-10
Step 12 (Update Tests) ← depends on Step 11
Step 13 (Remove Old Classes + Rename NS) ← depends on Steps 11-12
Step 14 (Remove Newtonsoft from Models) ← FUTURE, out of scope
```
