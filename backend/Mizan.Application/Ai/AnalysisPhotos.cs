using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

/// <summary>The queued removal of one analysis photo, which also says whose it is.</summary>
public sealed record AnalysisPhotoExpiry(string Key, Guid UserId);

/// <summary>
/// Photos sent for a food analysis (web, Telegram, MCP) or with an assistant chat turn. Each is stored with a removal scheduled
/// in the outbox <see cref="RetentionDays"/> days later, so a photo nothing else keeps does not
/// stay for good; the person can delete it sooner (<see cref="DeleteAnalysisPhotoCommand"/>). The
/// scheduled job is also the record of whose photo it is (docs/AI.md#logging-a-meal-from-words-or-a-photo).
/// </summary>
public sealed class AnalysisPhotos
{
    public const int RetentionDays = 30;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IStorageService _storage;
    private readonly IOutbox _outbox;
    private readonly IMizanDbContext _context;
    private readonly ILogger<AnalysisPhotos> _logger;

    public AnalysisPhotos(IStorageService storage, IOutbox outbox, IMizanDbContext context, ILogger<AnalysisPhotos> logger)
    {
        _storage = storage;
        _outbox = outbox;
        _context = context;
        _logger = logger;
    }

    public static string DedupeKey(string key) => $"analysis-photo:{key}";

    /// <summary>
    /// Stores the photo and schedules its removal; null when storage fails, which costs the picture, not the
    /// analysis or the chat turn. <paramref name="name"/> starts the stored file's name.
    /// </summary>
    public async Task<string?> StoreAsync(
        Guid userId, byte[] bytes, string contentType, string extension, CancellationToken cancellationToken, string name = "meal")
    {
        try
        {
            await using var stream = new MemoryStream(bytes);
            var stored = await _storage.UploadAsync(
                new StorageUpload(StorageFolder.Meals, $"{name}{extension}", contentType, stream, bytes.Length), cancellationToken);
            await _outbox.ScheduleAsync(OutboxJobTypes.AnalysisPhotoExpiry, new AnalysisPhotoExpiry(stored.Key, userId),
                DateTime.UtcNow.AddDays(RetentionDays), DedupeKey(stored.Key), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return stored.Url;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not store an analysed meal photo");
            return null;
        }
    }

    /// <summary>The scheduled removal of the photo at <paramref name="imageUrl"/> when it is the person's, else null.</summary>
    internal async Task<(OutboxJob Job, string Key)?> FindAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken)
    {
        var key = await _storage.TryGetKeyAsync(imageUrl, cancellationToken);
        if (key is null) return null;
        var job = await _context.OutboxJobs
            .Where(j => j.DedupeKey == DedupeKey(key) && j.Type == OutboxJobTypes.AnalysisPhotoExpiry && j.Status == OutboxJobStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null) return null;
        var expiry = JsonSerializer.Deserialize<AnalysisPhotoExpiry>(job.Payload, Json);
        return expiry?.UserId == userId ? (job, key) : null;
    }
}

/// <summary>
/// Deletes a photo the person sent for a food analysis or a chat turn, before its scheduled removal. Someone
/// else's photo, or one already removed, is not found.
/// </summary>
public sealed record DeleteAnalysisPhotoCommand(string ImageUrl) : IRequest;

public sealed class DeleteAnalysisPhotoCommandHandler : IRequestHandler<DeleteAnalysisPhotoCommand>
{
    private readonly AnalysisPhotos _photos;
    private readonly IStorageService _storage;
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteAnalysisPhotoCommandHandler(AnalysisPhotos photos, IStorageService storage, IMizanDbContext context, ICurrentUserService currentUser)
    {
        _photos = photos;
        _storage = storage;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteAnalysisPhotoCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var found = await _photos.FindAsync(userId, request.ImageUrl, cancellationToken)
            ?? throw new EntityNotFoundException("That photo was not found.");

        await _storage.DeleteAsync(found.Key, cancellationToken);
        // Done now, so the scheduled removal does not run.
        found.Job.Status = OutboxJobStatus.Succeeded;
        found.Job.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
