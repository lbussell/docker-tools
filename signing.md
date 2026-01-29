# Container Image Signing Design

## Overview

This document describes the design for signing container images using Notary v2 signatures and ESRP (Enterprise Signing and Release Platform) infrastructure.

## Architecture

### Service Layers

The design uses three service layers with clear separation of concerns:

1. **IBulkImageSigningService** - Orchestrates the full signing workflow: generates payloads, signs them, and pushes signature artifacts to the registry using ORAS.

2. **IPayloadSigningService** - Handles bulk payload signing: writes payloads to disk, invokes ESRP, reads back signed files, and calculates certificate chains.

3. **IEsrpSigningService** - Abstracts the ESRP signing infrastructure. Signs all files in a directory in-place.

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Bulk operations over single-item** | ESRP signing service accepts a directory of files and signs them all. This is more efficient than signing one file at a time. |
| **File I/O is synchronous** | Payload files are tiny. Async overhead is unnecessary. Only external service calls are async. |
| **`FileInfo` for signed payloads** | Avoids primitive obsession. ESRP modifies files in-place, so file references are natural. Stream/byte[] would require re-reading. |
| **`SigningKeyCode` as enum** | Three fixed key codes (Images, Referrers, Test) that map to certificates on the ESRP backend. Enum provides type safety. |
| **Certificate chain calculated internally** | The chain (in `io.cncf.notary.x509chain.thumbprint#S256` format per Notary v2 spec) is computed by `IPayloadSigningService`, not returned by ESRP. |
| **`IPayloadSigningService` as abstraction point** | If we need to support different signing backends in the future, this is the interface to swap implementations. `IEsrpSigningService` is specific to our current infrastructure. |
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
