using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Media;

namespace Mizan.Infrastructure.Storage;

/// <summary>
/// One implementation for every S3-compatible backend we care about. MinIO and
/// Cloudflare R2 differ only in ServiceUrl, Region and whether a public base
/// URL serves permanent object links. The settings are runtime settings, so the
/// client is built at use time and rebuilt when an administrator changes them;
/// without an endpoint every upload is refused (docs/ADMIN.md#runtime-settings).
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly IRuntimeOptions<StorageOptions> _settings;
    private readonly ILogger<S3StorageService> _logger;
    private Bucket? _bucket;

    public S3StorageService(IRuntimeOptions<StorageOptions> settings, ILogger<S3StorageService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<StoredObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default)
    {
        if (upload.Length <= 0)
        {
            throw new DomainValidationException("The file is empty.");
        }

        var bucket = await RequireAsync(cancellationToken);
        var limit = upload.ContentType == ModelFormat.ContentType ? bucket.Options.MaxModelBytes : bucket.Options.MaxUploadBytes;
        if (upload.Length > limit)
        {
            var mb = limit / (1024 * 1024);
            throw new DomainValidationException($"The file must be {mb} MB or smaller.");
        }

        var key = StorageKey.Build(upload.Folder, upload.FileName);

        try
        {
            await bucket.Client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = bucket.Options.Bucket,
                    Key = key,
                    InputStream = upload.Content,
                    ContentType = upload.ContentType,
                    // Immutable by construction: every upload gets a fresh key,
                    // so anything holding an old URL keeps seeing the old image.
                    Headers = { CacheControl = "public, max-age=31536000, immutable" },
                    DisablePayloadSigning = bucket.DisablePayloadSigning,
                },
                cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Upload to {Bucket}/{Key} failed", bucket.Options.Bucket, key);
            throw new DomainException("The file could not be stored. Try again.", ex);
        }

        return new StoredObject(key, await bucket.GetUrlAsync(key));
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (await CurrentAsync(cancellationToken) is not { } bucket) return;

        try
        {
            await bucket.Client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = bucket.Options.Bucket, Key = key },
                cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            // A missing object is the state we wanted; anything else is worth
            // knowing about but never worth failing the caller's operation.
            _logger.LogWarning(ex, "Delete of {Bucket}/{Key} failed", bucket.Options.Bucket, key);
        }
    }

    public async Task<string> GetUrlAsync(string key, CancellationToken cancellationToken = default) =>
        await (await RequireAsync(cancellationToken)).GetUrlAsync(key);

    public async Task<string?> TryGetKeyAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return null;
        if (parsed.Scheme is not ("http" or "https") || parsed.UserInfo.Length != 0) return null;
        if (await CurrentAsync(cancellationToken) is not { } bucket) return null;

        var publicKey = TryGetKeyFromBase(parsed, bucket.PublicBase);
        if (publicKey is not null) return publicKey;

        var options = bucket.Options;
        if (!Uri.TryCreate(options.ServiceUrl.TrimEnd('/') + "/", UriKind.Absolute, out var serviceBase))
            return null;

        Uri bucketBase;
        if (options.ForcePathStyle)
        {
            bucketBase = new Uri(serviceBase, Uri.EscapeDataString(options.Bucket) + "/");
        }
        else
        {
            if (serviceBase.HostNameType != UriHostNameType.Dns) return null;
            bucketBase = new UriBuilder(serviceBase) { Host = options.Bucket + "." + serviceBase.Host }.Uri;
        }

        return TryGetKeyFromBase(parsed, bucketBase);
    }

    /// <summary>The bucket for the current settings, or null when no endpoint is set.</summary>
    private async ValueTask<Bucket?> CurrentAsync(CancellationToken cancellationToken)
    {
        var options = await _settings.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(options.ServiceUrl)) return null;

        // The options instance changes only with the settings snapshot, so one comparison is enough.
        // A replaced client is left to the collector rather than disposed, because a request that
        // started a moment ago may still be using it.
        var bucket = Volatile.Read(ref _bucket);
        if (bucket is not null && ReferenceEquals(bucket.Options, options)) return bucket;

        bucket = new Bucket(options);
        Volatile.Write(ref _bucket, bucket);
        return bucket;
    }

    private async ValueTask<Bucket> RequireAsync(CancellationToken cancellationToken) =>
        await CurrentAsync(cancellationToken)
        ?? throw new DomainException("Image uploads are not configured on this server.");

    private static string? TryGetKeyFromBase(Uri url, Uri? objectBase)
    {
        // A matching folder on somebody else's host must never authorize a
        // deletion in our bucket. Public URLs and signed service URLs each
        // have their own origin and complete path prefix.
        if (objectBase is null
            || !string.Equals(url.Scheme, objectBase.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(url.IdnHost, objectBase.IdnHost, StringComparison.OrdinalIgnoreCase)
            || url.Port != objectBase.Port)
            return null;

        var prefix = objectBase.AbsolutePath.TrimEnd('/') + "/";
        if (!url.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return null;

        var key = Uri.UnescapeDataString(url.AbsolutePath[prefix.Length..]);
        if (key.Contains('\\') || key.Any(char.IsControl)
            || key.Split('/').Any(segment => segment is "." or ".."))
            return null;

        // Shared legacy-media objects deliberately remain outside this set.
        return StorageKey.IsOurs(key) ? key : null;
    }

    /// <summary>One client for one set of settings.</summary>
    private sealed class Bucket
    {
        public Bucket(StorageOptions options)
        {
            Options = options;

            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region,
                // R2 rejects the flexible checksum headers the v4 SDK adds by
                // default, and older MinIO builds do too. Sending them only when a
                // request actually requires one is what makes a single client work
                // against all three.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };

            Client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), config);

            PublicBase = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
                ? null
                : new Uri(options.PublicBaseUrl.TrimEnd('/') + "/");

            // R2 rejects the streaming payload signature, so it is normally turned
            // off - but the SDK refuses to skip it over plain HTTP, since an unsigned
            // body on an unencrypted connection is tamperable in transit. Production
            // endpoints are HTTPS and keep the R2-friendly behaviour; a local MinIO on
            // http:// signs the payload instead of failing every upload.
            DisablePayloadSigning =
                Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var endpoint)
                && endpoint.Scheme == Uri.UriSchemeHttps;
        }

        public StorageOptions Options { get; }

        public IAmazonS3 Client { get; }

        public Uri? PublicBase { get; }

        public bool DisablePayloadSigning { get; }

        public Task<string> GetUrlAsync(string key)
        {
            if (PublicBase is not null)
            {
                return Task.FromResult(new Uri(PublicBase, key).ToString());
            }

            return Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = Options.Bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(Options.PresignedUrlMinutes),
            });
        }
    }
}
