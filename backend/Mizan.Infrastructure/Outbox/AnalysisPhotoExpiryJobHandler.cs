using System.Text.Json;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Removes an analysis photo when its retention ends (<see cref="AnalysisPhotos.RetentionDays"/>).
/// Deleting is idempotent, so a photo the person already deleted, or a retry, is harmless.
/// </summary>
public sealed class AnalysisPhotoExpiryJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IStorageService _storage;

    public AnalysisPhotoExpiryJobHandler(IStorageService storage) => _storage = storage;

    public string Type => OutboxJobTypes.AnalysisPhotoExpiry;

    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        AnalysisPhotoExpiry job;
        try { job = JsonSerializer.Deserialize<AnalysisPhotoExpiry>(payload, Json) ?? throw new OutboxPermanentException("The queued photo removal was empty."); }
        catch (JsonException ex) { throw new OutboxPermanentException("The queued photo removal could not be read.", ex); }

        await _storage.DeleteAsync(job.Key, cancellationToken);
    }
}
