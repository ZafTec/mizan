using System.Globalization;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Storage;

/// <summary>
/// Pure. Object keys are built here and nowhere else, so nothing the caller
/// supplies can steer a write outside its folder.
/// </summary>
public static class StorageKey
{
    private static readonly IReadOnlyDictionary<StorageFolder, string> Prefixes =
        new Dictionary<StorageFolder, string>
        {
            [StorageFolder.Avatars] = "avatars",
            [StorageFolder.Recipes] = "recipes",
            [StorageFolder.Meals] = "meals",
        };

    public static string Build(StorageFolder folder, string fileName, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        // The slashes are quoted and the culture is fixed: in a format string
        // "/" means "the current culture's date separator", so an unquoted
        // yyyy/MM silently becomes 2026-03 on a host whose locale says so, and
        // the key layout would then depend on where the server happens to run.
        var month = now.ToString("yyyy'/'MM", CultureInfo.InvariantCulture);

        return $"{Prefixes[folder]}/{month}/{Guid.CreateVersion7():N}{Extension(fileName)}";
    }

    /// <summary>True for a key this application could have written.</summary>
    public static bool IsOurs(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return Prefixes.Values.Any(prefix => key.StartsWith(prefix + "/", StringComparison.Ordinal));
    }

    /// <summary>
    /// The extension is cosmetic - content type is what the store records - so
    /// anything unfamiliar is dropped rather than trusted into the key.
    /// </summary>
    private static string Extension(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" ? extension : ".bin";
    }
}
