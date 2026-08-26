namespace Mizan.Application.Interfaces;

/// <summary>
/// Where an uploaded file goes. A closed set, because the folder becomes part
/// of the object key and must never come from the caller.
/// </summary>
public enum StorageFolder
{
    Avatars = 0,
    Recipes = 1,
}

public record StorageUpload(
    StorageFolder Folder,
    string FileName,
    string ContentType,
    Stream Content,
    long Length);

/// <summary>The stored object. Key is ours; Url is what a browser can fetch.</summary>
public record StoredObject(string Key, string Url);

/// <summary>
/// Object storage behind one interface - docs/REFOCUS.md §7. The v2
/// implementation speaks S3, which covers self-hosted MinIO and Cloudflare R2
/// with nothing but configuration between them.
/// </summary>
public interface IStorageService
{
    Task<StoredObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default);

    /// <summary>Idempotent: deleting an absent key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// A fetchable URL for a key. Permanent when a public base URL is
    /// configured, otherwise a time-limited presigned link.
    /// </summary>
    Task<string> GetUrlAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// The key a stored URL refers to, or null when the URL is not ours -
    /// an OAuth avatar on googleusercontent, say. Deleting needs this.
    /// </summary>
    string? TryGetKey(string? url);
}
