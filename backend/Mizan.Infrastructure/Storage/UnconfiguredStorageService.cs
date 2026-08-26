using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Storage;

/// <summary>
/// Stands in when no object store is configured, so the API still starts for
/// a developer with no MinIO running and for the test suite. Uploads fail with
/// a message that says what is actually wrong instead of a connection error.
/// </summary>
public class UnconfiguredStorageService : IStorageService
{
    public Task<StoredObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default) =>
        throw new DomainException("Image uploads are not configured on this server.");

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string> GetUrlAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(key);

    public string? TryGetKey(string? url) => null;
}
