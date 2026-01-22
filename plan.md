# ImageBuilder.Models Migration Plan

Your job is to work on the problem below.
Commit your changes when you finish a task or when the project successfully builds after a significant changes.
Mark tasks as completed as you go.
If a task is already marked complete, validate that the implementation is correct.

## Problem Statement

The ImageBuilder project contains model classes (`Models/Manifest/`, `Models/Image/`) that could be shared with other repositories via a NuGet package. Currently:

- Models use Newtonsoft.Json for serialization
- Models are mutable classes without nullable annotations
- Models are tightly coupled to ImageBuilder
- No way for external consumers to read/edit manifest.json or image-info.json files

**Goal**: Enable other repos (e.g., FilePusher, docker-dotnet unit tests) to easily read, query, and edit Manifest and ImageArtifactDetails JSON files with the same results as ImageBuilder.

## Current Progress (2026-01-22)

### Completed
- **Task 1: Manifest Models Migration** - COMPLETE ✅
  - All 12 Manifest model files moved to `ImageBuilder.Models/Manifest/`
  - Project reference added from ImageBuilder to ImageBuilder.Models
  - Newtonsoft.Json package added to ImageBuilder.Models
  - All 369 tests pass

- **Task 2: Image Models Migration** - COMPLETE ✅
  - All 6 Image model files moved to `ImageBuilder.Models/Image/`:
    - `ImageArtifactDetails.cs`, `ImageData.cs`, `PlatformData.cs`
    - `RepoData.cs`, `ManifestData.cs`, `Layer.cs`
  - Removed `[JsonIgnore]` ViewModel properties from models
  - Created `ImageArtifactContext` for tracking ViewModel associations
  - Created `PlatformDataFactory` for creating PlatformData from ViewModels
  - Renamed `ImageArtifactDetails.cs` to `ImageArtifactDetailsHelper.cs` in ImageBuilder (keeps FromJson with Layer converter)
  - Updated all commands to use context-based lookups
  - Context-aware merge in `MergeImageArtifactDetails`
  - All 370 tests pass

- **Task 2a: Migrate BuildCommand** - COMPLETE ✅
- **Task 2b: Migrate ImageCacheService** - COMPLETE ✅  
- **Task 2c: Update ImageData.CompareTo** - COMPLETE ✅

### Key Architectural Changes

**ImageArtifactContext Pattern**: Decouples data models from ViewModels by providing lookup dictionaries:
- `SetPlatformContext(PlatformData, PlatformInfo, ImageInfo)` - Register associations
- `SetImageContext(ImageData, ImageInfo, RepoInfo)` - Register image associations
- `GetPlatformInfo(PlatformData)` - Lookup PlatformInfo
- `GetImageInfo(ImageData)` - Lookup ImageInfo
- `GetImageInfoForPlatform(PlatformData)` - Lookup ImageInfo for a platform
- `GetRepoInfo(ImageData)` - Lookup RepoInfo

**Context-Aware Merge**: The `MergeImageArtifactDetails` method now has an overload that accepts contexts, enabling proper image matching during merge operations without relying on ViewModel properties.

**PlatformData.GetIdentifier()**: Changed signature from `GetIdentifier(bool excludeProductVersion)` to `GetIdentifier(string? productVersion)` since the model no longer has access to ImageInfo.

## Scope

### In Scope
- `Models/Manifest/` - Manifest, Repo, Image, Platform, Tag, etc.
- `Models/Image/` - ImageArtifactDetails, RepoData, ImageData, PlatformData, etc.
- Thin service interfaces for loading/saving JSON files

### Out of Scope
- `ViewModel/` classes - These remain in ImageBuilder (CLI/service coupling)
- `Models/McrStatus/`, `Models/Subscription/`, etc. - Not needed by external consumers
- Query utilities - Can be added in future phases based on consumer needs

## Success Criteria

1. Models are migrated to the ImageBuilder.Models project
2. Models use System.Text.Json instead of Newtonsoft.Json
3. Models use nullable annotations
4. Models are converted to immutable Records
5. Models use immutable collections (IReadOnlyList, IReadOnlyDictionary)
6. External projects can load/save Manifest and ImageArtifactDetails JSON files
7. No breaking changes to build/publish pipeline behavior
8. ImageBuilder continues to work correctly

---

## Tasks

### Task 1: Migrate Manifest Models to ImageBuilder.Models ✅ COMPLETE

**Description**: Move the `Models/Manifest/` classes from ImageBuilder to ImageBuilder.Models, maintaining the existing class structure and Newtonsoft.Json serialization.

**Files migrated**:
- `Manifest.cs`, `Repo.cs`, `Image.cs`, `Platform.cs`, `Tag.cs`, `Readme.cs`
- `Architecture.cs` (enum), `OS.cs` (enum)
- `CustomBuildLegGroup.cs`, `CustomBuildLegDependencyType.cs` (enum)
- `TagDocumentationType.cs` (enum), `TagSyndication.cs`

**Acceptance Criteria**:
- [x] All Manifest model classes exist in `ImageBuilder.Models` project
- [x] Namespace is `Microsoft.DotNet.ImageBuilder.Models.Manifest`
- [x] ImageBuilder references ImageBuilder.Models and compiles
- [x] Existing Newtonsoft.Json attributes preserved for compatibility
- [x] All ImageBuilder tests pass
- [x] No changes to serialized JSON output

**Notes**:
- Nullable disabled in ImageBuilder.Models for now (Task 4 will enable)
- Fixed license header in TagSyndication.cs
- Run `dotnet format src/ImageBuilder.Models` for formatting (don't run on full ImageBuilder)

---

### Task 2: Migrate Image Models to ImageBuilder.Models ✅ COMPLETE

**Description**: Move the `Models/Image/` classes from ImageBuilder to ImageBuilder.Models.

**Files migrated**:
- `ImageArtifactDetails.cs` - Data only, no FromJson method
- `ImageData.cs` - No ManifestImage/ManifestRepo properties
- `PlatformData.cs` - No ImageInfo/PlatformInfo/AllTags properties
- `RepoData.cs`, `ManifestData.cs`, `Layer.cs` - Unchanged

**Supporting changes in ImageBuilder**:
- `ImageArtifactDetailsHelper.cs` - Contains FromJson with custom Layer converter
- `PlatformDataFactory.cs` - Factory methods for creating PlatformData from ViewModels
- `ImageArtifactContext.cs` - Tracks ViewModel associations for data models
- Context-aware `MergeImageArtifactDetails` overload in ImageInfoHelper

**Acceptance Criteria**:
- [x] All Image model classes exist in `ImageBuilder.Models` project
- [x] Namespace is `Microsoft.DotNet.ImageBuilder.Models.Image`
- [x] ImageBuilder compiles with updated references
- [x] All ImageBuilder tests pass (370 tests)
- [x] `[JsonIgnore]` properties removed from models (ViewModel refs moved to context)

---

### Task 2a: Migrate BuildCommand to use ImageArtifactContext ✅ COMPLETE

**Description**: Refactor `BuildCommand` to use the context pattern when creating and tracking `PlatformData` objects during builds.

**Implementation**:
- Added `_imageArtifactContext` field - always created alongside `_imageArtifactDetails`
- `CreatePlatformData()` uses `PlatformDataFactory.FromPlatformInfo()` and registers in context
- Added `GetPlatformInfo()` and `GetImageInfo()` helper methods for context lookups
- All usages of ViewModel properties updated to use context

**Acceptance Criteria**:
- [x] BuildCommand uses ImageArtifactContext for all ViewModel lookups
- [x] New platforms are registered in context when created
- [x] All build-related tests pass

---

### Task 2b: Migrate ImageCacheService to use ImageArtifactContext ✅ COMPLETE

**Description**: Refactor `ImageCacheService` to receive context instead of relying on `PlatformData.PlatformInfo`.

**Implementation**:
- Added `ImageArtifactContext?` parameters to `CheckForCachedImageAsync` (for both current and source)
- Uses context-only lookups for PlatformInfo (no fallback to properties)
- Updated BuildCommand to pass `_imageArtifactContext` and source context
- Updated GenerateBuildMatrixCommand to pass contexts
- Updated test mocks

**Acceptance Criteria**:
- [x] ImageCacheService uses context for PlatformInfo lookups
- [x] All cache-related tests pass

---

### Task 2c: Update ImageData.CompareTo to not require ManifestImage ✅ COMPLETE

**Description**: The `CompareTo` method throws if `ManifestImage` is null. This blocks merge operations with context-based loading.

**Implementation**:
- Removed `InvalidOperationException` when ManifestImage is null
- Made comparison null-safe: checks if both have same ManifestImage reference before comparing
- Falls back to ProductVersion and first Platform comparison when ManifestImage is not set

**Acceptance Criteria**:
- [x] `ImageData.CompareTo` works without ManifestImage
- [x] Merge operations continue to work correctly
- [x] All merge-related tests pass

---

### Task 2.5: Add Serialization Tests for Image Models

**Description**: Create comprehensive serialization tests for Image models following the pattern established for Manifest models.

**Reference**: See existing tests in `ImageBuilder.Tests/Models/` for the pattern:
- `ManifestSerializationTests.cs`, `PlatformSerializationTests.cs`, etc.
- `SerializationHelper.cs` for test utilities

**New test files to create**:
- `ImageArtifactDetailsSerializationTests.cs`
- `RepoDataSerializationTests.cs`
- `ImageDataSerializationTests.cs`
- `PlatformDataSerializationTests.cs`
- `ManifestDataSerializationTests.cs`
- `LayerSerializationTests.cs`

**Acceptance Criteria**:
- [ ] All Image models have serialization tests
- [ ] Tests cover default, fully-populated, and minimal scenarios
- [ ] Tests verify required property validation
- [ ] Tests use `SerializationHelper` utilities
- [ ] All tests pass with current Newtonsoft.Json implementation

---

### Task 3: Convert All Models to System.Text.Json

**Status**: BLOCKED - Requires significant work due to behavioral differences

**Description**: Replace Newtonsoft.Json attributes with System.Text.Json attributes in all migrated models (Manifest and Image).

**Prerequisites**:
- Task 2 completed (all models in ImageBuilder.Models)
- Task 2.5 completed (Image model serialization tests exist)
- All serialization tests passing with Newtonsoft.Json

**Investigation Findings (2026-01-22)**:

The STJ migration was attempted but has significant behavioral differences that require substantial work:

1. **`[JsonRequired]` behavior differs**: STJ's `[JsonRequired]` only checks if a property is present in JSON, not if it's null. Newtonsoft's `Required = Required.Always` throws for both missing AND null values. The 8 `_Null` tests would need to be removed or completely rewritten.

2. **Default value handling differs**: STJ's `DefaultIgnoreCondition.WhenWritingDefault` uses the type's numeric default (0 for enums), not the `[DefaultValue]` attribute. This requires a custom `TypeInfoResolver` modifier to match Newtonsoft behavior.

3. **Comprehensive migration required**: Both ImageBuilder.Models AND ImageBuilder's JsonHelper must be migrated simultaneously. Having STJ attributes on models while using Newtonsoft for serialization causes a mismatch.

4. **Custom serialization logic needed**: A custom `TypeInfoResolver` modifier is needed to:
   - Respect `[DefaultValue]` attributes for enums
   - Skip empty collections for non-required properties
   - Handle default DateTime values

**Files to update**:
- All Manifest models: `Manifest.cs`, `Repo.cs`, `Image.cs`, `Platform.cs`, `Tag.cs`, `Readme.cs`, `CustomBuildLegGroup.cs`, `TagSyndication.cs`
- All Image models: `ImageArtifactDetails.cs`, `RepoData.cs`, `ImageData.cs`, `PlatformData.cs`, `ManifestData.cs`

**Test infrastructure updates**:
- Update `SerializationHelper.cs` to use System.Text.Json with custom `JsonSerializerOptions`
- Remove/update 8-11 tests that rely on Newtonsoft-specific null handling behavior
- May need to adjust expected JSON outputs for minor formatting differences

**ImageBuilder Changes**:
- Update `ManifestInfo.LoadModel()` to use `JsonSerializer.Deserialize()`
- Update `ImageArtifactDetails.FromJson()` to use `JsonSerializer.Deserialize()`
- Update `JsonHelper` to use System.Text.Json
- Update all serialization call sites

**Acceptance Criteria**:
- [ ] No Newtonsoft.Json references in ImageBuilder.Models
- [ ] All models use System.Text.Json attributes
- [ ] `SerializationHelper.cs` updated to use System.Text.Json
- [ ] ALL existing serialization tests pass (Manifest + Image models) - except `_Null` tests which need updating
- [ ] JSON serialization output matches previous format (camelCase, ignore nulls, etc.)
- [ ] Deserialization handles all existing JSON files correctly

**Recommended Approach**:
1. Create a `JsonSerializerOptionsFactory` in ImageBuilder.Models with properly configured options
2. Implement custom `TypeInfoResolver` modifier for default value handling
3. Migrate ImageBuilder's JsonHelper to use the new options
4. Update tests to reflect STJ behavior for null handling
5. Run comprehensive integration tests with actual manifest.json files

---

### Task 4: Add Nullable Annotations to All Models

**Description**: Enable nullable reference types and add appropriate annotations to all models.

**Changes**:
- Ensure `<Nullable>enable</Nullable>` in ImageBuilder.Models.csproj (already present)
- Mark optional properties as nullable (e.g., `string?`)
- Mark required properties as non-nullable
- Fix any nullability warnings
- Remove `#nullable enable/disable` pragmas (use project-wide setting)

**Files to update**:
- All Manifest models
- All Image models

**Acceptance Criteria**:
- [ ] All model properties have correct nullability annotations
- [ ] No nullable warnings in ImageBuilder.Models
- [ ] No new nullable warnings in ImageBuilder (or warnings addressed)
- [ ] All tests pass

**Manifest Property Analysis**:
| Class | Property | Nullable? | Reason |
|-------|----------|-----------|--------|
| Manifest | Includes | Yes | Optional array |
| Manifest | Readme | Yes | Optional |
| Manifest | Registry | Yes | Optional (can be overridden) |
| Manifest | Repos | No | Always required |
| Manifest | Variables | No | Has default value |
| Repo | Id | Yes | Optional identifier |
| Repo | Images | No | Required |
| Repo | Name | No | Required |
| Repo | McrTagsMetadataTemplate | Yes | Optional |
| Repo | Readmes | No | Has default value |
| Platform | Dockerfile | No | Required |
| Platform | Tags | No | Required |
| Platform | DockerfileTemplate | Yes | Optional |
| Platform | Variant | Yes | Optional |
| Tag | DocumentationGroup | Yes | Optional |
| Tag | Syndication | Yes | Optional |

**Image Property Analysis**:
| Class | Property | Nullable? | Reason |
|-------|----------|-----------|--------|
| ImageArtifactDetails | Repos | No | Always present |
| RepoData | Repo | No | Required |
| RepoData | Images | No | Always present |
| ImageData | ProductVersion | Yes | Optional |
| ImageData | Manifest | Yes | Optional |
| ImageData | Platforms | No | Always present |
| PlatformData | Dockerfile | No | Required |
| PlatformData | Digest | No | Required |
| PlatformData | BaseImageDigest | Yes | Optional |
| PlatformData | SimpleTags | No | Has default |
| PlatformData | Layers | No | Has default |
| ManifestData | Digest | No | Required |
| ManifestData | SharedTags | No | Has default |
| ManifestData | SyndicatedDigests | No | Has default |

---

### Task 5: Convert All Models to Records with Immutable Collections

**Description**: Convert all model classes to `record` types and change collections to truly immutable types (`ImmutableList<T>`, `ImmutableDictionary<K,V>`).

**Collection Changes**:
- Replace `T[]` with `ImmutableList<T>`
- Replace `List<T>` with `ImmutableList<T>`
- Replace `IDictionary<K,V>` with `ImmutableDictionary<K,V>`
- Add `using System.Collections.Immutable;`

**Record Changes**:
- Convert classes to `record` types
- Use `init` accessors for deserialization support
- Add `[JsonConstructor]` if needed
- Remove empty constructors (records have synthesized constructors)

**Example transformation**:
```csharp
// Before
public class Manifest
{
    public string[] Includes { get; set; }
    public Repo[] Repos { get; set; } = Array.Empty<Repo>();
    public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
}

// After
public record Manifest
{
    public ImmutableList<string>? Includes { get; init; }
    public required ImmutableList<Repo> Repos { get; init; }
    public ImmutableDictionary<string, string> Variables { get; init; } = ImmutableDictionary<string, string>.Empty;
}
```

**Files to update**:
- All Manifest models
- All Image models

**ImageBuilder Changes Required**:
- `ManifestInfo.LoadModel()` - merges included manifests; needs to build immutable collections
- Any code that adds/removes items from model collections
- Use `with` expressions to create modified copies where needed

**Handling IComparable**:
- `PlatformData`, `ImageData`, `RepoData` implement `IComparable<T>`
- Records can implement interfaces - keep the comparison logic
- Note: `PlatformData.CompareTo` depends on `ImageInfo` (ViewModel) - this comparison logic may need to stay in ImageBuilder as an extension or comparer class

**Acceptance Criteria**:
- [ ] All models are records
- [ ] All collections use `ImmutableList<T>` or `ImmutableDictionary<K,V>`
- [ ] JSON deserialization works correctly (STJ can deserialize to immutable collections)
- [ ] All ImageBuilder tests pass
- [ ] ImageBuilder code updated to work with immutable models
- [ ] `IComparable<T>` implementations still work for sorting

**Notes**:
- System.Text.Json supports deserializing to immutable collections natively
- May need `[JsonConstructor]` attribute on records with complex initialization
- Consider using `ImmutableList<T>.Builder` for efficient construction during deserialization

---

### Task 6: Create Service Interfaces for JSON Loading/Saving

**Description**: Add thin service interfaces and implementations for loading and saving Manifest and ImageArtifactDetails JSON files.

**New files**:
```
ImageBuilder.Models/
  Services/
    IManifestLoader.cs
    IImageArtifactDetailsLoader.cs
    ManifestLoader.cs
    ImageArtifactDetailsLoader.cs
    JsonOptions.cs  (shared serialization options)
```

**Interface definitions**:
```csharp
public interface IManifestLoader
{
    Manifest Load(string filePath);
    Manifest LoadFromJson(string json);
    void Save(Manifest manifest, string filePath);
    string ToJson(Manifest manifest);
}

public interface IImageArtifactDetailsLoader
{
    ImageArtifactDetails Load(string filePath);
    ImageArtifactDetails LoadFromJson(string json);
    void Save(ImageArtifactDetails details, string filePath);
    string ToJson(ImageArtifactDetails details);
}
```

**Acceptance Criteria**:
- [ ] `IManifestLoader` interface defined
- [ ] `IImageArtifactDetailsLoader` interface defined
- [ ] Default implementations provided
- [ ] JSON options match existing format (camelCase, ignore nulls, etc.)
- [ ] ImageBuilder uses the new loaders (or its own implementation)
- [ ] All tests pass
- [ ] External consumer can load a manifest.json file and get correct data

---

### Task 7: Add Integration Tests for External Consumption

**Description**: Create tests that verify an external consumer can successfully use the ImageBuilder.Models package.

**New test project or tests**:
- Test loading actual manifest.json from dotnet/docker repo
- Test loading image-info.json files
- Test round-trip serialization (load -> save -> load)
- Test that ImageBuilder and external consumer get same results

**Acceptance Criteria**:
- [ ] Test project references only ImageBuilder.Models (not ImageBuilder)
- [ ] Tests successfully load sample manifest.json
- [ ] Tests successfully load sample image-info.json
- [ ] Round-trip tests pass (no data loss)
- [ ] Serialized output matches expected format

---

### Task 8: Update ImageBuilder to Use New Models

**Description**: Update ImageBuilder to work with the migrated, immutable models.

**Areas requiring changes**:
- `ManifestInfo.LoadModel()` - merges included manifests (needs immutable approach)
- `JsonHelper` - may need System.Text.Json support
- Any code that mutates model properties

**Acceptance Criteria**:
- [ ] ImageBuilder compiles with no errors
- [ ] All ImageBuilder tests pass
- [ ] Build pipelines continue to work
- [ ] No regression in serialized output

---

### Task 9: Update NuGet Package Configuration

**Description**: Ensure ImageBuilder.Models is properly configured for NuGet publishing.

**Changes**:
- Verify package metadata (description, authors, license, etc.)
- Add README to package
- Configure source link / symbol packages
- Add package version

**Acceptance Criteria**:
- [ ] Package can be built with `dotnet pack`
- [ ] Package includes all public APIs
- [ ] Package metadata is complete
- [ ] Package can be installed in a test project

---

## Task Dependencies

**Updated Task order** (reflecting completed work):
1. ✅ Task 1: Migrate Manifest Models - COMPLETE
2. ✅ Task 2a: Migrate BuildCommand to use context - COMPLETE
3. ✅ Task 2b: Migrate ImageCacheService to use context - COMPLETE
4. ✅ Task 2c: Update ImageData.CompareTo - COMPLETE
5. ✅ Task 2: Migrate Image Models - COMPLETE
6. Task 2.5: Add Serialization Tests for Image Models
7. Task 3: Convert to System.Text.Json (requires Task 2.5)
8. Tasks 4 → 5 are sequential
9. Tasks 6, 7, 8 can be done in parallel (after Task 5)
10. Task 9 is the final step

**Next Steps**: Task 2.5 (serialization tests) or Task 3 (System.Text.Json conversion).

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking changes to JSON format | High - pipeline failures | Compare serialized output before/after each change |
| Immutable collections break existing code | Medium | Identify all mutation sites before converting |
| System.Text.Json behavior differences | Medium | Test edge cases (empty arrays, nulls, defaults) |
| ViewModel coupling harder to separate than expected | **Resolved** | Created `ImageArtifactContext` pattern and context-aware merge |
| BuildCommand complexity | **Resolved** | Context is always created and maintained alongside ImageArtifactDetails |

---

## Testing Strategy

1. **Unit tests**: Ensure each model serializes/deserializes correctly
2. **Integration tests**: Load real manifest.json and image-info.json files
3. **Comparison tests**: Verify JSON output matches between old and new implementations
4. **Pipeline tests**: Run full build/publish pipeline to verify no regressions

---

## Notes on Implementation

### ImageArtifactContext Pattern
Created `ImageArtifactContext` class that provides lookup dictionaries to track associations between data models and view models without storing references directly on the data objects.

**Key methods**:
- `SetPlatformContext(PlatformData, PlatformInfo, ImageInfo)` - Register associations
- `SetImageContext(ImageData, ImageInfo, RepoInfo)` - Register image associations
- `GetPlatformInfo(PlatformData)` - Lookup PlatformInfo for a given PlatformData
- `GetImageInfo(ImageData)` - Lookup ImageInfo for a given ImageData
- `GetImageInfoForPlatform(PlatformData)` - Lookup ImageInfo for a platform
- `GetRepoInfo(ImageData)` - Lookup RepoInfo for a given ImageData
- `GetAllTags(PlatformData)` - Get combined shared + platform tags

**New loader methods**:
- `ImageInfoHelper.LoadFromFileWithContext()` - Returns context with lookups populated
- `ImageInfoHelper.LoadFromContentWithContext()` - Same, from string content

### Context-Aware Merge
The `MergeImageArtifactDetails` method has an overload accepting `ImageArtifactContext` objects for both source and target. This enables:
- Matching images by their manifest `ImageInfo` reference (same manifest used for all loads)
- Copying context mappings when repos/images are added to target
- Proper platform merging across files without ViewModel properties on models

### PlatformDataFactory
Created `PlatformDataFactory.FromPlatformInfo(PlatformInfo, ImageInfo)` to replace the static method that was on `PlatformData`. This keeps the model clean while providing the same functionality.

### Formatting
Run `dotnet format src/ImageBuilder.Models` for formatting errors. Do NOT run `dotnet format` on the entire ImageBuilder project as it causes too many changes.
