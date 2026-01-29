# Container Image Signing Design

## Overview

This document describes the design for signing container images using Notary v2 signatures and ESRP (Enterprise Signing and Release Platform) infrastructure.

## Integration Approach

Signing happens immediately after build/push in the `BuildCommand`, but all signing logic is fully encapsulated in services (not in BuildCommand itself). This approach:

- Keeps the build process self-contained and distributed across jobs
- Makes it easy to extract signing to a separate command later if needed
- Allows swapping implementations (real vs no-op) via DI
- Enables testing in isolation

```
BuildCommand
    └── IBulkImageSigningService.SignImagesAsync(imageArtifactDetails, keyCode)
            └── IPayloadSigningService.SignPayloadsAsync(requests, keyCode)
                    └── IEsrpSigningService.SignDirectoryAsync(directory, keyCode)
            └── IOrasSignatureService.PushSignatureAsync(signedPayload)
```

### Signing Inputs

| Input | Source | When Available |
|-------|--------|----------------|
| Image digests | `ImageArtifactDetails` (PlatformData.Digest, ManifestData.Digest) | After push |
| OCI Descriptor (mediaType, size) | ORAS query to registry | After push |
| Signing key code | `SigningConfiguration` | Always (config) |
| Output directory | `BuildConfiguration.ArtifactStagingDirectory` | Always (config) |

## Architecture

### Service Layers

The design uses three service layers with clear separation of concerns:

1. **IBulkImageSigningService** - Orchestrates the full signing workflow: generates payloads, signs them, and pushes signature artifacts to the registry using ORAS.

2. **IPayloadSigningService** - Handles bulk payload signing: writes payloads to disk, invokes ESRP, reads back signed files, and calculates certificate chains.

3. **IEsrpSigningService** - Abstracts the ESRP signing infrastructure. Signs all files in a directory in-place.

### ORAS Abstraction

Registry operations are abstracted with interface segregation to support multiple backends:

```cs
interface IOrasDescriptorService
{
    Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken ct);
}

interface IOrasSignatureService
{
    Task<string> PushSignatureAsync(string imageReference, PayloadSigningResult result, CancellationToken ct);
}

interface IOrasDiscoveryService  // existing functionality
{
    Task<OrasDiscoverData> DiscoverAsync(string digest, string artifactType, CancellationToken ct);
}

interface IOrasAttachService  // existing functionality  
{
    Task AttachAsync(string digest, string artifactType, IDictionary<string, string> annotations, CancellationToken ct);
}
```

### ORAS .NET Library API (OrasProject.Oras 0.4.0)

Key types for our implementation:

**Repository** - Main entry point for registry operations:
```cs
var repo = new Repository(new RepositoryOptions
{
    Reference = Reference.Parse($"{registry}/{repository}"),
    Client = new Client(httpClient, credentialProvider, new Cache(memoryCache)),
});
Descriptor desc = await repo.ResolveAsync(reference, ct);  // Get descriptor
```

**Packer.PackManifestAsync** - Attach referrer artifacts (signatures):
```cs
var options = new PackManifestOptions
{
    ManifestAnnotations = annotations,  // e.g., io.cncf.notary.x509chain.thumbprint#S256
    Subject = subjectDescriptor,        // The image being signed - makes this a referrer
};
var signatureDescriptor = await Packer.PackManifestAsync(
    repo, 
    Packer.ManifestVersion.Version1_1, 
    "application/vnd.cncf.notary.signature",  // artifactType
    options);
```

**Auth.Client** - Authenticated HTTP client:
```cs
var credentialProvider = new SingleRegistryCredentialProvider(registry, credential);
var memoryCache = new MemoryCache(new MemoryCacheOptions());
var authClient = new Client(httpClient, credentialProvider, new Cache(memoryCache));
```

**ICredentialProvider** - Interface for providing credentials:
```cs
interface ICredentialProvider
{
    Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken ct);
}
```

**Implementation approach:**
1. Create `OrasCredentialProviderAdapter` that wraps our existing `IRegistryCredentialsProvider`
2. Use `repo.ResolveAsync()` to get the subject descriptor (image being signed)
3. Use `Packer.PackManifestAsync()` with `Subject` set to attach signature as referrer
4. Annotations include `io.cncf.notary.x509chain.thumbprint#S256` with certificate chain

**Implementations:**
- `OrasDotNetDescriptorService` - Uses ORAS .NET library for descriptor resolution
- `OrasDotNetSignatureService` - Uses ORAS .NET library for pushing signatures

Existing `IOrasClient`/`OrasClient` remain unchanged - they continue to serve other parts of the codebase. The new interfaces are independent and used only by the signing services.

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Signing in BuildCommand via injected service** | Keeps build self-contained, distributed across jobs, but logic is encapsulated and easily extractable |
| **Bulk operations over single-item** | ESRP signing service accepts a directory of files and signs them all. This is more efficient than signing one file at a time. |
| **File I/O is synchronous** | Payload files are tiny. Async overhead is unnecessary. Only external service calls are async. |
| **`FileInfo` for signed payloads** | Avoids primitive obsession. ESRP modifies files in-place, so file references are natural. Stream/byte[] would require re-reading. |
| **`SigningKeyCode` as int from config** | Certificate IDs are integers passed directly to DDSignFiles.dll. Config-driven, no enum mapping needed. |
| **Certificate chain calculated internally** | The chain (in `io.cncf.notary.x509chain.thumbprint#S256` format per Notary v2 spec) is computed by `IPayloadSigningService`, not returned by ESRP. |
| **`IPayloadSigningService` as abstraction point** | If we need to support different signing backends in the future, this is the interface to swap implementations. `IEsrpSigningService` is specific to our current infrastructure. |
| **Interface segregation for ORAS** | Different operations have different consumers; easier to mock/test |
| **Two ORAS backends (CLI + .NET library)** | Existing CLI-based `IOrasClient` unchanged; new .NET library services for signing only |
| **Configuration-driven key codes** | prod vs nonprod determined by pipeline config, no runtime logic |
| **`ImageArtifactDetails` as signing input** | Already contains all digests; no additional registry queries for digest list |
| **Dry-run handled at command level** | Signing services don't need dry-run logic; commands will conditionally invoke signing. |
| **Error handling deferred** | Will revisit once we understand failure modes from the signing service in practice. |

## Configuration

### SigningConfiguration (add to PublishConfiguration)

Added as a nested property on the existing `PublishConfiguration` record.

- `ImageSigningKeyCode` - Key code for signing container images
- `ReferrerSigningKeyCode` - Key code for signing referrer artifacts (SBOMs, etc.)

Production vs test key codes are configured in `publish-config-prod.yml` and `publish-config-nonprod.yml` respectively.

### BuildConfiguration (new top-level section)

New configuration section for build/pipeline artifacts.

- `ArtifactStagingDirectory` - Root directory for build artifacts. Signing payloads will be written to a subdirectory. Files are uploaded by the pipeline after signing; no cleanup needed.

## File Naming

Signed payload files should be named using the image's digest or tag for traceability.

## Interfaces

```cs
using Microsoft.DotNet.ImageBuilder.Models.Notary;

namespace Microsoft.DotNet.ImageBuilder.Signing;

sealed record ImageSigningRequest(
    // Full tag/reference to manifest or manifest list.
    string ImageName,
    // Signing payload object.
    Payload Payload);

sealed record PayloadSigningResult(
    // Full tag/reference to manifest or manifest list.
    string ImageName,
    // Signed payload file is stored on disk.
    FileInfo SignedPayload,
    // Certificate chain (io.cncf.notary.x509chain.thumbprint#S256 format).
    string CertificateChain);

sealed record ImageSigningResult(
    // Full tag/reference to manifest or manifest list.
    string ImageName,
    // Digest of the signature artifact that has been created and stored in the registry.
    string SignatureDigest);

interface ISigningPayloadGenerator
{
    // Generates the signing payload for the given reference.
    ImageSigningRequest GeneratePayload(string reference);
}

interface IBulkImageSigningService
{
    // Signs multiple images in bulk.
    // 1. Sign payloads using IPayloadSigningService
    // 2. Use ORAS to push signature artifacts to registry
    // 3. Return ImageSigningResult with signature digests
    Task<IReadOnlyList<ImageSigningResult>> SignImagesAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default);
}

interface IPayloadSigningService
{
    // Signs payloads in bulk.
    // 1. Write all payloads to a subdirectory of ArtifactStagingDirectory (sync)
    // 2. Call IEsrpSigningService.SignDirectoryAsync
    // 3. Read back signed files, calculate certificate chains
    // 4. Return results
    Task<IReadOnlyList<PayloadSigningResult>> SignPayloadsAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default);
}

interface IEsrpSigningService
{
    // Signs all files in directory in-place using DDSignFiles.dll via MicroBuild plugin.
    // Generates sign list JSON, invokes DDSignFiles.dll, cleans up temp files.
    Task SignDirectoryAsync(
        DirectoryInfo directory,
        int signingKeyCode,
        CancellationToken cancellationToken = default);
}
```

## Configuration Records

```cs
// Add Signing property to existing PublishConfiguration
public sealed record SigningConfiguration
{
    // Certificate ID used by DDSignFiles.dll for signing container images
    public int ImageSigningKeyCode { get; set; }
    // Certificate ID used by DDSignFiles.dll for signing referrer artifacts (SBOMs, etc.)
    public int ReferrerSigningKeyCode { get; set; }
}

// New top-level configuration section
public sealed record BuildConfiguration
{
    public static string ConfigurationKey => nameof(BuildConfiguration);
    // Path to root directory for build artifacts (string for config binding)
    public string? ArtifactStagingDirectory { get; set; }
}
```

## Implementation Plan

### Phase 1: Configuration & Models ✅
- [x] Add signing request/result records to Signing folder
- [x] Add `SigningConfiguration` record with `ImageSigningKeyCode` (int), `ReferrerSigningKeyCode` (int)
- [x] Add `Signing` property to `PublishConfiguration`
- [x] Add `BuildConfiguration` record with `ArtifactStagingDirectory`
- [x] Add `BuildConfiguration` to DI via Options pattern

### Phase 2: ORAS Abstraction ✅
- [x] Add `OrasProject.Oras` NuGet package (version 0.4.0)
- [x] Define new interfaces (`IOrasDescriptorService`, `IOrasSignatureService`) - separate from existing `IOrasClient`
- [x] Create `OrasCredentialProviderAdapter` implementing `ICredentialProvider` (wraps `IRegistryCredentialsProvider`)
- [x] Implement `OrasDotNetDescriptorService` using `Repository.ResolveAsync()`
- [x] Implement `OrasDotNetSignatureService` using `Packer.PackManifestAsync()`
- [x] Register new implementations in DI (existing `IOrasClient` unchanged)

### Phase 3: Signing Services
- [ ] Implement `IEsrpSigningService` (calls ESRP to sign directory)
- [ ] Port certificate chain calculation from Python to .NET (CBOR parsing, SHA256 thumbprints)
- [ ] Implement `IPayloadSigningService` (writes payloads, calls ESRP, calculates cert chain)
- [ ] Implement `IBulkImageSigningService` (orchestrates payload signing + ORAS push)
- [ ] Register services in DI

### Phase 4: BuildCommand Integration ✅
- [x] Inject `IBulkImageSigningService` into `BuildCommand`
- [x] Call signing service after successful push (respect dry-run)
- [ ] Generate `ImageSigningRequest` from built platforms (TODO: complete implementation)

### Phase 5: Pipeline Configuration
- [ ] Add signing key codes to `publish-config-prod.yml`
- [ ] Add signing key codes to `publish-config-nonprod.yml`
- [ ] Add `ArtifactStagingDirectory` to build configuration

### Phase 6: Testing & Cleanup
- [ ] Unit tests for signing services
- [ ] Unit tests for ORAS .NET implementation
- [ ] Integration test with mock ESRP
- [ ] Delete unused `GenerateSigningPayloadsCommand`

## Files to Create/Modify

### New Files
- `src/ImageBuilder/Signing/ImageSigningRequest.cs` ✅
- `src/ImageBuilder/Signing/PayloadSigningResult.cs` ✅
- `src/ImageBuilder/Signing/ImageSigningResult.cs` ✅
- `src/ImageBuilder/Signing/IBulkImageSigningService.cs`
- `src/ImageBuilder/Signing/IPayloadSigningService.cs`
- `src/ImageBuilder/Signing/IEsrpSigningService.cs`
- `src/ImageBuilder/Signing/BulkImageSigningService.cs`
- `src/ImageBuilder/Signing/PayloadSigningService.cs`
- `src/ImageBuilder/Signing/EsrpSigningService.cs`
- `src/ImageBuilder/Signing/CertificateChainCalculator.cs`
- `src/ImageBuilder/Configuration/SigningConfiguration.cs` ✅
- `src/ImageBuilder/Configuration/BuildConfiguration.cs` ✅
- `src/ImageBuilder/Oras/IOrasDescriptorService.cs`
- `src/ImageBuilder/Oras/IOrasSignatureService.cs`
- `src/ImageBuilder/Oras/OrasCredentialProviderAdapter.cs`
- `src/ImageBuilder/Oras/OrasDotNetDescriptorService.cs`
- `src/ImageBuilder/Oras/OrasDotNetSignatureService.cs`
- `src/ImageBuilder.Tests/Signing/*.cs`

### Modified Files
- `src/ImageBuilder/Configuration/PublishConfiguration.cs` - Add `Signing` property ✅
- `src/ImageBuilder/Configuration/ConfigurationExtensions.cs` - Register `BuildConfiguration` ✅
- `src/ImageBuilder/Commands/BuildCommand.cs` - Inject and call signing service
- `src/ImageBuilder/ImageBuilder.cs` - Register signing services in DI (partially done ✅)
- `src/ImageBuilder/Microsoft.DotNet.ImageBuilder.csproj` - Add OrasProject.Oras package reference
- `eng/docker-tools/templates/variables/publish-config-prod.yml`
- `eng/docker-tools/templates/variables/publish-config-nonprod.yml`

**Note:** Existing `IOrasClient` and `OrasClient` are NOT modified. New ORAS interfaces are independent and used only by the signing services.

## Open Questions

1. ~~**ORAS .NET library maturity**~~ - Inspected API; `Repository.ResolveAsync()` and `Manifests.PushAsync()` provide what we need

## ESRP Integration (DDSignFiles.dll)

ESRP signing uses `DDSignFiles.dll` from the MicroBuild signing plugin.

**Invocation:**
```
dotnet --roll-forward major <MBSIGN_APPFOLDER>/DDSignFiles.dll -- /filelist:<path> /signType:<real|test>
```

**Key details:**
- `MBSIGN_APPFOLDER` environment variable points to the MicroBuild plugin install location
- Sign list is a JSON file listing files to sign and certificate IDs
- `signType: test` only works on Windows
- Files are signed in-place

**Implementation notes from reference (`microbuild-signing.md`):**
- Generate sign list JSON with file paths and certificate IDs
- Write to temp file, clean up after signing
- Handle exit code for error reporting
- Consider dry-run behavior (test signing vs skip on non-Windows)

## Certificate Chain Calculation

Reference implementation in `generate-cert-chain-thumbprint.py`. Needs to be ported to .NET.

**Algorithm:**
1. Read signed payload file as COSE_Sign1 envelope (CBOR tag 18)
2. Extract x5chain (key 33) from unprotected header
3. For each certificate in chain, compute SHA256 hash
4. Return JSON array of hex-encoded thumbprints

**Dependencies needed:**
- CBOR parsing library (e.g., `System.Formats.Cbor` or `PeterO.Cbor`)

**Output format:** `["thumbprint1", "thumbprint2", ...]` used in `io.cncf.notary.x509chain.thumbprint#S256` annotation.
