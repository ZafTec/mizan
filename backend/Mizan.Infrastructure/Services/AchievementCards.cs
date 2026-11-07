using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Application.Notifications;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Storage;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// The picture of an achievement's notification. Its own badge, in the large size, when it has one.
/// Otherwise a card drawn once by <see cref="AchievementCardImage"/>, uploaded to storage and
/// remembered in <see cref="Achievement.CardImageUrl"/>. The card is only drawn when storage has a
/// public https address (<c>Storage.PublicBaseUrl</c>), because a presigned link expires and Telegram
/// fetches the picture itself. Any failure leaves the notification without a picture.
/// </summary>
public sealed class AchievementCards : IAchievementCards
{
    private readonly IMizanDbContext _context;
    private readonly IStorageService _storage;
    private readonly IRuntimeOptions<StorageOptions> _options;
    private readonly IAppUrls _urls;
    private readonly ILogger<AchievementCards> _logger;

    public AchievementCards(
        IMizanDbContext context,
        IStorageService storage,
        IRuntimeOptions<StorageOptions> options,
        IAppUrls urls,
        ILogger<AchievementCards> logger)
    {
        _context = context;
        _storage = storage;
        _options = options;
        _urls = urls;
        _logger = logger;
    }

    public async Task<string?> ImageUrlAsync(Achievement achievement, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(achievement.IconUrl)) return _urls.Asset(NotificationMessages.LargeBadge(achievement.IconUrl));
        if (!string.IsNullOrWhiteSpace(achievement.CardImageUrl)) return achievement.CardImageUrl;

        var options = await _options.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(options.ServiceUrl)
            || !Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicBase)
            || publicBase.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        try
        {
            var png = AchievementCardImage.Render(achievement.Name);
            using var content = new MemoryStream(png);
            var stored = await _storage.UploadAsync(
                new StorageUpload(StorageFolder.Achievements, $"card-{achievement.Id:N}.png", "image/png", content, png.Length),
                cancellationToken);

            // Remembered at once, outside the caller's unit of work: the upload has happened either way.
            await _context.Achievements
                .Where(a => a.Id == achievement.Id && a.CardImageUrl == null)
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.CardImageUrl, stored.Url), cancellationToken);
            achievement.CardImageUrl = stored.Url;
            return stored.Url;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Drawing the card of achievement {AchievementId} failed", achievement.Id);
            return null;
        }
    }
}
