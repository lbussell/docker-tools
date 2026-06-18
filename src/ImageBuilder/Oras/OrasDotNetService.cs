// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrasProject.Oras;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// ORAS .NET library implementation for resolving OCI descriptors and pushing Notary v2 signatures.
/// </summary>
public class OrasDotNetService(
    IRegistryCredentialsProvider credentialsProvider,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IFileSystem fileSystem,
    ILogger<OrasDotNetService> logger,
    IRegistryCredentialsHost? credentialsHost = null)
        : IOrasService
{
    /// <summary>
    /// Media type for COSE signature envelopes per the Notary v2 spec.
    /// </summary>
    private const string CoseMediaType = "application/cose";

    /// <summary>
    /// Annotation key for the certificate chain thumbprints per the Notary v2 spec.
    /// </summary>
    private const string CertificateChainAnnotation = "io.cncf.notary.x509chain.thumbprint#S256";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly Cache _orasCache = new(cache);
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<OrasDotNetService> _logger = logger;
    private readonly OrasCredentialProviderAdapter _credentialProvider = new(credentialsProvider, credentialsHost);

    /// <summary>
    /// Per-repository ORAS <see cref="Client"/> instances, keyed by "registry/repository". Reusing one
    /// client per repository keeps its <see cref="ScopeManager"/> alive across operations. ORAS seeds the
    /// scope on every request (e.g. ManifestStore/Repository call SetActionsForRepository before sending),
    /// but it stores that scope in the client's ScopeManager - not in the shared token cache. The token
    /// cache is keyed by host plus scope, so unless the ScopeManager (and therefore the scope) survives,
    /// each operation looks up an empty-scope key, misses the cache, and triggers a fresh 401 challenge and
    /// token fetch. A separate client per repository keeps each repository's scope (and token) isolated,
    /// avoiding an ever-growing unioned scope per host.
    /// See the ORAS cache-key logic (v0.5.0):
    /// https://github.com/oras-project/oras-dotnet/blob/84309e748527589539e3a2736df260d1c3e9a09b/src/OrasProject.Oras/Registry/Remote/Auth/Client.cs#L548-L550
    /// </summary>
    private readonly ConcurrentDictionary<string, Client> _clients = new();

    /// <inheritdoc/>
    public async Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        _logger.LogDebug("Fetching descriptor for reference: {Reference}", reference);

        long startTime = Stopwatch.GetTimestamp();
        Repository repository = CreateRepository(reference);
        Descriptor descriptor = await repository.ResolveAsync(reference, cancellationToken);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);
        _logger.LogDebug(
            "Resolved descriptor: mediaType={MediaType}, digest={Digest}, size={Size} in {Elapsed}",
            descriptor.MediaType, descriptor.Digest, descriptor.Size, elapsed);

        return descriptor;
    }

    /// <inheritdoc/>
    public async Task<ManifestQueryResult> GetManifestAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        _logger.LogDebug("Fetching manifest for reference: {Reference}", reference);

        long startTime = Stopwatch.GetTimestamp();
        Repository repository = CreateRepository(reference);
        (Descriptor descriptor, Stream content) = await repository.FetchAsync(reference, cancellationToken);

        string json;
        await using (content)
        using (StreamReader reader = new(content))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        JsonObject manifest = (JsonObject)(JsonNode.Parse(json)
            ?? throw new InvalidOperationException($"Invalid JSON manifest for '{reference}': {json}"));

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);
        _logger.LogDebug(
            "Fetched manifest: mediaType={MediaType}, digest={Digest}, size={Size} in {Elapsed}",
            descriptor.MediaType, descriptor.Digest, descriptor.Size, elapsed);

        return new ManifestQueryResult(descriptor.Digest, manifest);
    }

    /// <inheritdoc/>
    public async Task<string> PushSignatureAsync(
        Descriptor subjectDescriptor,
        PayloadSigningResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectDescriptor);
        ArgumentNullException.ThrowIfNull(result);

        Repository repository = CreateRepository(result.ImageName);

        byte[] payloadBytes = await _fileSystem.ReadAllBytesAsync(result.SignedPayloadFilePath, cancellationToken);
        Descriptor signatureLayerDescriptor = Descriptor.Create(payloadBytes, CoseMediaType);

        using MemoryStream payloadStream = new(payloadBytes);
        await repository.PushAsync(signatureLayerDescriptor, payloadStream, cancellationToken);

        Dictionary<string, string> annotations = new()
        {
            [CertificateChainAnnotation] = result.CertificateChain
        };

        PackManifestOptions options = new()
        {
            ManifestAnnotations = annotations,
            Subject = subjectDescriptor,
            Layers = [signatureLayerDescriptor]
        };

        _logger.LogDebug("Pushing signature for {ImageName}", result.ImageName);

        long startTime = Stopwatch.GetTimestamp();
        Descriptor signatureDescriptor =
            await Packer.PackManifestAsync(
                pusher: repository,
                version: Packer.ManifestVersion.Version1_1,
                artifactType: OciArtifactType.NotarySignatureV2,
                options: options,
                cancellationToken);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);
        _logger.LogDebug("Signature pushed: {Digest} in {Elapsed}", signatureDescriptor.Digest, elapsed);

        return signatureDescriptor.Digest;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReferrerInfo>> GetReferrersAsync(
        string reference,
        bool isDryRun = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        IReadOnlyList<ReferrerInfo> referrers = isDryRun ? []
            : await GetReferrersImplAsync(reference, cancellationToken);

        _logger.LogInformation(
            "{Reference} has {Count} referrer(s) (DryRun={DryRun})",
            reference, referrers.Count, isDryRun);

        return referrers;
    }

    private async Task<IReadOnlyList<ReferrerInfo>> GetReferrersImplAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        Repository repository = CreateRepository(reference);
        Descriptor subjectDescriptor = await repository.ResolveAsync(reference, cancellationToken);

        List<ReferrerInfo> referrers = [];
        Reference parsedRef = Reference.Parse(reference);

        await foreach (Descriptor referrer in repository.FetchReferrersAsync(subjectDescriptor, cancellationToken))
        {
            string referrerReference = $"{parsedRef.Registry}/{parsedRef.Repository}@{referrer.Digest}";
            referrers.Add(new ReferrerInfo(referrerReference, referrer.ArtifactType)
            {
                Annotations = referrer.Annotations as IReadOnlyDictionary<string, string>
                    ?? referrer.Annotations?.AsReadOnly()
            });
            _logger.LogDebug("Found referrer: {Referrer} (artifactType={ArtifactType})", referrerReference, referrer.ArtifactType);
        }

        return referrers;
    }

    /// <inheritdoc/>
    public async Task<string> AttachArtifactAsync(
        string reference,
        string artifactType,
        IDictionary<string, string> annotations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactType);
        ArgumentNullException.ThrowIfNull(annotations);

        long startTime = Stopwatch.GetTimestamp();
        Repository repository = CreateRepository(reference);
        Descriptor subjectDescriptor = await repository.ResolveAsync(reference, cancellationToken);

        PackManifestOptions options = new()
        {
            ManifestAnnotations = annotations,
            Subject = subjectDescriptor
        };

        Descriptor artifactDescriptor =
            await Packer.PackManifestAsync(
                pusher: repository,
                version: Packer.ManifestVersion.Version1_1,
                artifactType: artifactType,
                options: options,
                cancellationToken);

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTime);
        _logger.LogDebug(
            "Attached artifact to {Reference}: digest={Digest}, artifactType={ArtifactType}, annotations={Annotations}, elapsed={Elapsed}",
            reference, artifactDescriptor.Digest, artifactType, annotations, elapsed);

        return artifactDescriptor.Digest;
    }

    /// <summary>
    /// Creates an ORAS repository client for the given reference, backed by a per-repository
    /// <see cref="Client"/> so that cached OAuth tokens are reused across operations on the same repository.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo:tag").</param>
    private Repository CreateRepository(string reference)
    {
        _logger.LogTrace("Creating ORAS repository for: {Reference}", reference);
        Reference parsedRef = Reference.Parse(reference);
        _logger.LogTrace(
            "Parsed reference: Registry={Registry}, Repository={Repository}, Reference={ContentReference}",
            parsedRef.Registry, parsedRef.Repository, parsedRef.ContentReference);

        RepositoryOptions repositoryOptions = new()
        {
            Reference = parsedRef,
            Client = GetOrCreateClient(parsedRef)
        };

        Repository repository = new(repositoryOptions);
        return repository;
    }

    /// <summary>
    /// Gets (or creates) the ORAS <see cref="Client"/> for the repository identified by
    /// <paramref name="parsedRef"/>, reusing it across operations so its <see cref="ScopeManager"/>
    /// (and the host-plus-scope keyed OAuth token cache) is reused rather than rebuilt per
    /// operation. ORAS seeds the scope on each request but holds it in the client's scope manager,
    /// so the client must outlive a single operation for the cached token to be found. A separate
    /// client per repository keeps each repository's scope (and token) isolated, avoiding an
    /// ever-growing unioned scope across repositories.
    /// </summary>
    /// <param name="parsedRef">The parsed reference identifying the registry and repository.</param>
    internal Client GetOrCreateClient(Reference parsedRef)
    {
        string repositoryKey = $"{parsedRef.Registry}/{parsedRef.Repository}";
        return _clients.GetOrAdd(repositoryKey, _ =>
            new Client(
                _httpClientFactory.CreateClient(nameof(OrasDotNetService)),
                _credentialProvider,
                _orasCache));
    }
}
