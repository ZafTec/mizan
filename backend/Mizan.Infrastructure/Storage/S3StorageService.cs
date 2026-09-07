using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Storage;

/// <summary>
/// One implementation for every S3-compatible backend we care about. MinIO and
/// Cloudflare R2 differ only in ServiceUrl, Region and whether a public base
/// URL serves permanent object links.
/// </summary>
public class S3StorageService : IStorageService, IDisposable
{
    private readonly StorageOptions _options;
    private readonly IAmazonS3 _client;
    private readonly ILogger<S3StorageService> _logger;
    private readonly Uri? _publicBase;

    /// <summary>
    /// R2 rejects the streaming payload signature, so it is normally turned
    /// off - but the SDK refuses to skip it over plain HTTP, since an unsigned
    /// body on an unencrypted connection is tamperable in transit. Production
    /// endpoints are HTTPS and keep the R2-friendly behaviour; a local MinIO on
    /// http:// signs the payload instead of failing every upload.
    /// </summary>
    private readonly bool _disablePayloadSigning;

    public S3StorageService(IOptions<StorageOptions> options, ILogger<S3StorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ServiceUrl))
        {
            throw new InvalidOperationException(
                "Storage:ServiceUrl is not configured. Set it, or the app will refuse uploads at startup rather than at the first upload.");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = _options.ForcePathStyle,
            AuthenticationRegion = _options.Region,
            // R2 rejects the flexible checksum headers the v4 SDK adds by
            // default, and older MinIO builds do too. Sending them only when a
            // request actually requires one is what makes a single client work
            // against all three.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
            config);

        _publicBase = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? null
            : new Uri(_options.PublicBaseUrl.TrimEnd('/') + "/");

        _disablePayloadSigning =
            Uri.TryCreate(_options.ServiceUrl, UriKind.Absolute, out var endpoint)
            && endpoint.Scheme == Uri.UriSchemeHttps;
    }

    public async Task<StoredObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default)
    {
        if (upload.Length <= 0)
        {
            throw new DomainValidationException("The file is empty.");
        }

        if (upload.Length > _options.MaxUploadBytes)
        {
            var mb = _options.MaxUploadBytes / (1024 * 1024);
            throw new DomainValidationException($"The file must be {mb} MB or smaller.");
        }

        var key = StorageKey.Build(upload.Folder, upload.FileName);

        try
        {
            await _client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _options.Bucket,
                    Key = key,
                    InputStream = upload.Content,
                    ContentType = upload.ContentType,
                    // Immutable by construction: every upload gets a fresh key,
                    // so anything holding an old URL keeps seeing the old image.
                    Headers = { CacheControl = "public, max-age=31536000, immutable" },
                    DisablePayloadSigning = _disablePayloadSigning,
                },
                cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Upload to {Bucket}/{Key} failed", _options.Bucket, key);
            throw new DomainException("The file could not be stored. Try again.", ex);
        }

        return new StoredObject(key, await GetUrlAsync(key, cancellationToken));
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            await _client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = _options.Bucket, Key = key },
                cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            // A missing object is the state we wanted; anything else is worth
            // knowing about but never worth failing the caller's operation.
            _logger.LogWarning(ex, "Delete of {Bucket}/{Key} failed", _options.Bucket, key);
        }
    }

    public Task<string> GetUrlAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_publicBase is not null)
        {
            return Task.FromResult(new Uri(_publicBase, key).ToString());
        }

        return _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.PresignedUrlMinutes),
        });
    }

    public string? TryGetKey(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return null;
        if (parsed.Scheme is not ("http" or "https") || parsed.UserInfo.Length != 0) return null;

        var publicKey = TryGetKeyFromBase(parsed, _publicBase);
        if (publicKey is not null) return publicKey;

        if (!Uri.TryCreate(_options.ServiceUrl.TrimEnd('/') + "/", UriKind.Absolute, out var serviceBase))
            return null;

        Uri bucketBase;
        if (_options.ForcePathStyle)
        {
            bucketBase = new Uri(serviceBase, Uri.EscapeDataString(_options.Bucket) + "/");
        }
        else
        {
            if (serviceBase.HostNameType != UriHostNameType.Dns) return null;
            bucketBase = new UriBuilder(serviceBase) { Host = _options.Bucket + "." + serviceBase.Host }.Uri;
        }

        return TryGetKeyFromBase(parsed, bucketBase);
    }

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

    public void Dispose() => _client.Dispose();
}
