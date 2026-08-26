namespace Mizan.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// S3 endpoint. MinIO: https://minio.example. Cloudflare R2:
    /// https://{account-id}.r2.cloudflarestorage.com. Empty disables uploads.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>R2 wants "auto"; MinIO ignores it but the SDK insists on one.</summary>
    public string Region { get; set; } = "auto";

    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "mizan";

    /// <summary>
    /// Both MinIO and R2 address buckets by path rather than by subdomain.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Origin serving the bucket publicly - an R2 custom domain or r2.dev
    /// host, or whatever fronts a public MinIO bucket. When empty, reads fall
    /// back to presigned URLs, which work but expire and defeat CDN caching.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public int PresignedUrlMinutes { get; set; } = 60;

    public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;
}
