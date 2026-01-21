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

### Task 1: Migrate Manifest Models to ImageBuilder.Models

**Description**: Move the `Models/Manifest/` classes from ImageBuilder to ImageBuilder.Models, maintaining the existing class structure and Newtonsoft.Json serialization.

**Files to migrate**:
- `Manifest.cs`
- `Repo.cs`
- `Image.cs`
- `Platform.cs`
- `Tag.cs`
- `Readme.cs`
- `Architecture.cs` (enum)
- `OS.cs` (enum)
- `CustomBuildLegGroup.cs`
- `CustomBuildLegDependencyType.cs` (enum)
- `TagDocumentationType.cs` (enum)
- `TagSyndication.cs`

**Acceptance Criteria**:
- [ ] All Manifest model classes exist in `ImageBuilder.Models` project
- [ ] Namespace is `Microsoft.DotNet.ImageBuilder.Models.Manifest`
- [ ] ImageBuilder references ImageBuilder.Models and compiles
- [ ] Existing Newtonsoft.Json attributes preserved for compatibility
- [ ] All ImageBuilder tests pass
- [ ] No changes to serialized JSON output

**Notes**:
- Keep `[Description]` attributes for schema generation
- Keep `[JsonProperty(Required = Required.Always)]` attributes
- Add `<ProjectReference>` from ImageBuilder to ImageBuilder.Models

---

### Task 2: Migrate Image Models to ImageBuilder.Models

**Description**: Move the `Models/Image/` classes from ImageBuilder to ImageBuilder.Models.

**Files to migrate**:
- `ImageArtifactDetails.cs`
- `RepoData.cs`
- `ImageData.cs`
- `PlatformData.cs`
- `ManifestData.cs`
- `Layer` record (currently in PlatformData.cs)

**Acceptance Criteria**:
- [ ] All Image model classes exist in `ImageBuilder.Models` project
- [ ] Namespace is `Microsoft.DotNet.ImageBuilder.Models.Image`
- [ ] ImageBuilder compiles with updated references
- [ ] All ImageBuilder tests pass
- [ ] `SchemaVersion2LayerConverter` removed (schema migration no longer needed)
- [ ] `[JsonIgnore]` properties (`ImageInfo`, `PlatformInfo`, `ManifestImage`, `ManifestRepo`) remain in a separate extension/partial class in ImageBuilder (not in shared models)

**Notes**:
- The `PlatformData` and `ImageData` classes have `[JsonIgnore]` properties that reference ViewModel types. These must stay in ImageBuilder.
- Consider splitting into a core model (in ImageBuilder.Models) and an extension class (in ImageBuilder)
- Remove the `SchemaVersion2LayerConverter` as the schema migration is no longer needed

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

**Test scenarios for each model**:
1. `Default{Model}_Bidirectional` - Default instance serializes to expected JSON and back
2. `FullyPopulated{Model}_Bidirectional` - Fully populated instance serializes correctly
3. `FullyPopulated{Model}_RoundTrip` - Round-trip serialization preserves data
4. `Minimal{Model}_Bidirectional` - Minimal valid instance (only required fields)
5. `Deserialization_{Property}IsRequired_Missing` - Required property missing throws
6. `Deserialization_{Property}IsRequired_Null` - Required property null throws
7. `Serialization_{Property}IsRequired_Null` - Serializing null required property throws

**Acceptance Criteria**:
- [ ] All Image models have serialization tests
- [ ] Tests cover default, fully-populated, and minimal scenarios
- [ ] Tests verify required property validation
- [ ] Tests use `SerializationHelper` utilities
- [ ] All tests pass with current Newtonsoft.Json implementation
- [ ] Tests will serve as regression tests during STJ migration

---

### Task 3: Convert All Models to System.Text.Json

**Description**: Replace Newtonsoft.Json attributes with System.Text.Json attributes in all migrated models (Manifest and Image).

**Prerequisites**:
- Task 2.5 completed (Image model serialization tests exist)
- All serialization tests passing with Newtonsoft.Json

**Attribute Changes**:
- Replace `[JsonProperty(Required = Required.Always)]` with `[JsonRequired]`
- Replace `[JsonConverter(typeof(StringEnumConverter))]` with `[JsonConverter(typeof(JsonStringEnumConverter))]`
- Replace `[JsonProperty(DefaultValueHandling = ...)]` with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`
- Replace `[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]` with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
- Replace `[JsonIgnore]` with `[JsonIgnore]` (same in STJ)
- Remove Newtonsoft.Json package reference from ImageBuilder.Models

**Files to update**:
- All Manifest models: `Manifest.cs`, `Repo.cs`, `Image.cs`, `Platform.cs`, `Tag.cs`, `Readme.cs`, `CustomBuildLegGroup.cs`, `TagSyndication.cs`
- All Image models: `ImageArtifactDetails.cs`, `RepoData.cs`, `ImageData.cs`, `PlatformData.cs`, `ManifestData.cs`

**Test infrastructure updates**:
- Update `SerializationHelper.cs` to use System.Text.Json
- Ensure all existing serialization tests pass with STJ
- Add any STJ-specific edge case tests if behavior differs

**ImageBuilder Changes**:
- Update `ManifestInfo.LoadModel()` to use `JsonSerializer.Deserialize()`
- Update `ImageArtifactDetails.FromJson()` to use `JsonSerializer.Deserialize()`
- Update `JsonHelper` to use System.Text.Json (or create new helper)
- Update all serialization call sites

**Acceptance Criteria**:
- [ ] No Newtonsoft.Json references in ImageBuilder.Models
- [ ] All models use System.Text.Json attributes
- [ ] `SerializationHelper.cs` updated to use System.Text.Json
- [ ] ALL existing serialization tests pass (Manifest + Image models)
- [ ] JSON serialization output matches previous format (camelCase, ignore nulls, etc.)
- [ ] Deserialization handles all existing JSON files correctly

**Notes**:
- The existing serialization tests in `ImageBuilder.Tests/Models/` are the primary regression tests
- May need custom `JsonSerializerOptions` to match existing behavior:
  - `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
  - `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
  - Custom converter for empty collection handling
- The `JsonHelper.SerializeObject()` method in ImageBuilder uses Newtonsoft.Json - needs updating

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

**Task order**:
1. Tasks 1 & 2 can be done in parallel (migration phase)
2. Task 2.5 must follow Task 2 (needs Image models migrated)
3. Task 3 requires Tasks 1 & 2.5 complete (all models + all serialization tests)
4. Tasks 4 → 5 are sequential
5. Tasks 6, 7, 8 can be done in parallel (after Task 5)
6. Task 9 is the final step

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking changes to JSON format | High - pipeline failures | Compare serialized output before/after each change |
| Immutable collections break existing code | Medium | Identify all mutation sites before converting |
| System.Text.Json behavior differences | Medium | Test edge cases (empty arrays, nulls, defaults) |
| ViewModel coupling harder to separate than expected | Low | Keep ViewModel in ImageBuilder, use extension methods |

---

## Testing Strategy

1. **Unit tests**: Ensure each model serializes/deserializes correctly
2. **Integration tests**: Load real manifest.json and image-info.json files
3. **Comparison tests**: Verify JSON output matches between old and new implementations
4. **Pipeline tests**: Run full build/publish pipeline to verify no regressions
