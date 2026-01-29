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
    Task PushSignatureAsync(string imageReference, PayloadSigningResult result, CancellationToken ct);
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

**Implementations:**
- `OrasCliService` - Uses ORAS CLI (fallback, Linux only)
- `OrasDotNetService` - Uses ORAS .NET library (primary, cross-platform)

The ORAS .NET library is unproven, so maintaining a CLI fallback provides a safety net. The existing `IOrasClient` will be refactored to implement the segregated interfaces.

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Signing in BuildCommand via injected service** | Keeps build self-contained, distributed across jobs, but logic is encapsulated and easily extractable |
| **Bulk operations over single-item** | ESRP signing service accepts a directory of files and signs them all. This is more efficient than signing one file at a time. |
| **File I/O is synchronous** | Payload files are tiny. Async overhead is unnecessary. Only external service calls are async. |
| **`FileInfo` for signed payloads** | Avoids primitive obsession. ESRP modifies files in-place, so file references are natural. Stream/byte[] would require re-reading. |
| **`SigningKeyCode` as enum** | Three fixed key codes (Images, Referrers, Test) that map to certificates on the ESRP backend. Enum provides type safety. |
| **Certificate chain calculated internally** | The chain (in `io.cncf.notary.x509chain.thumbprint#S256` format per Notary v2 spec) is computed by `IPayloadSigningService`, not returned by ESRP. |
| **`IPayloadSigningService` as abstraction point** | If we need to support different signing backends in the future, this is the interface to swap implementations. `IEsrpSigningService` is specific to our current infrastructure. |
| **Interface segregation for ORAS** | Different operations have different consumers; easier to mock/test |
| **Two ORAS backends (CLI + .NET library)** | .NET library is unproven; CLI fallback provides safety net |
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

enum SigningKeyCode
{
    Images,
    Referrers,
    Test
}

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
        SigningKeyCode keyCode,
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
        SigningKeyCode keyCode,
        CancellationToken cancellationToken = default);
}

interface IEsrpSigningService
{
    // Signs all files in directory in-place using ESRP infrastructure.
    Task SignDirectoryAsync(
        DirectoryInfo directory,
        SigningKeyCode keyCode,
        CancellationToken cancellationToken = default);
}
```

## Configuration Records

```cs
// Add Signing property to existing PublishConfiguration
public sealed record SigningConfiguration
{
    public SigningKeyCode ImageSigningKeyCode { get; set; }
    public SigningKeyCode ReferrerSigningKeyCode { get; set; }
}

// New top-level configuration section
public sealed record BuildConfiguration
{
    public static string ConfigurationKey => nameof(BuildConfiguration);
    public DirectoryInfo? ArtifactStagingDirectory { get; set; }
}
```

## Implementation Plan

### Phase 1: Configuration & Models
- [ ] Add `SigningKeyCode` enum (Images, Referrers, Test)
- [ ] Add signing request/result records to Models project
- [ ] Add `SigningConfiguration` record with `ImageSigningKeyCode`, `ReferrerSigningKeyCode`
- [ ] Add `Signing` property to `PublishConfiguration`
- [ ] Add `BuildConfiguration` record with `ArtifactStagingDirectory`
- [ ] Add `BuildConfiguration` to DI via Options pattern

### Phase 2: ORAS Abstraction
- [ ] Define segregated ORAS interfaces (`IOrasDescriptorService`, `IOrasSignatureService`)
- [ ] Refactor existing `OrasClient` to implement the interfaces
- [ ] Add `OrasDotNetService` implementation using ORAS .NET library
- [ ] Register implementations in DI (configurable backend selection)

### Phase 3: Signing Services
- [ ] Implement `IEsrpSigningService` (calls ESRP to sign directory)
- [ ] Implement `IPayloadSigningService` (writes payloads, calls ESRP, calculates cert chain)
- [ ] Implement `IBulkImageSigningService` (orchestrates payload signing + ORAS push)
- [ ] Register services in DI

### Phase 4: BuildCommand Integration
- [ ] Inject `IBulkImageSigningService` into `BuildCommand`
- [ ] Call signing service after successful push (respect dry-run)
- [ ] Pass `ImageArtifactDetails` and key code from configuration

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
- `src/ImageBuilder/Signing/SigningKeyCode.cs`
- `src/ImageBuilder/Signing/ImageSigningRequest.cs`
- `src/ImageBuilder/Signing/PayloadSigningResult.cs`
- `src/ImageBuilder/Signing/ImageSigningResult.cs`
- `src/ImageBuilder/Signing/IBulkImageSigningService.cs`
- `src/ImageBuilder/Signing/IPayloadSigningService.cs`
- `src/ImageBuilder/Signing/IEsrpSigningService.cs`
- `src/ImageBuilder/Signing/BulkImageSigningService.cs`
- `src/ImageBuilder/Signing/PayloadSigningService.cs`
- `src/ImageBuilder/Signing/EsrpSigningService.cs`
- `src/ImageBuilder/Configuration/SigningConfiguration.cs`
- `src/ImageBuilder/Configuration/BuildConfiguration.cs`
- `src/ImageBuilder/Oras/IOrasDescriptorService.cs`
- `src/ImageBuilder/Oras/IOrasSignatureService.cs`
- `src/ImageBuilder/Oras/OrasDotNetService.cs`
- `src/ImageBuilder.Tests/Signing/*.cs`

### Modified Files
- `src/ImageBuilder/Configuration/PublishConfiguration.cs` - Add `Signing` property
- `src/ImageBuilder/Configuration/ConfigurationExtensions.cs` - Register `BuildConfiguration`
- `src/ImageBuilder/Commands/BuildCommand.cs` - Inject and call signing service
- `src/ImageBuilder/ImageBuilder.cs` - Register signing services in DI
- `src/ImageBuilder/OrasClient.cs` - Refactor to implement segregated interfaces
- `eng/docker-tools/templates/variables/publish-config-prod.yml`
- `eng/docker-tools/templates/variables/publish-config-nonprod.yml`

## Open Questions

1. **ORAS .NET library maturity** - Need to evaluate if it supports all required operations (manifest fetch, signature push)
2. **ESRP integration details** - Exact command/API for directory signing
3. **Certificate chain calculation** - Reference implementation exists, needs to be integrated
