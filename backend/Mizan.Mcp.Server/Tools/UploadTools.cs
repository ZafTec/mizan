using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Images, in and out.
///
/// This server runs server-side, so a file path would mean nothing here -
/// callers hand over base64. That puts a practical ceiling well below the
/// API's own limit, since the encoded string has to fit in a tool call, so
/// both tools say so rather than letting a 6 MB photo fail somewhere less
/// obvious.
/// </summary>
[McpServerToolType]
public sealed class UploadTools
{
    /// <summary>
    /// Base64 inflates by a third, and a tool argument this large is already
    /// pushing it. Rejected here with a message that says what to do, rather
    /// than by the API after the round trip.
    /// </summary>
    private const int MaxBytes = 4 * 1024 * 1024;

    private readonly IBackendApiClient _api;

    public UploadTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "upload_image")]
    [Description(
        "Stores an image and returns its key and URL, for use as a recipe photo "
        + "or avatar. imageBase64 is the raw file bytes, base64-encoded, up to "
        + "about 4 MB decoded. folder is recipes or avatars.")]
    public Task<string> UploadImage(
        string imageBase64,
        string fileName = "upload.jpg",
        [Description("recipes or avatars")] string folder = "recipes",
        CancellationToken ct = default)
    {
        var bytes = Decode(imageBase64);
        var contentType = DetectOrThrow(bytes);

        return _api.PostFileAsync(
            $"/api/Uploads/image?folder={Folder(folder)}",
            "file",
            bytes,
            fileName,
            contentType,
            ct);
    }

    [McpServerTool(Name = "analyze_food_image")]
    [Description(
        "Estimates what is in a photo of a meal and its macros. Pro only, and "
        + "it costs against your assistant allowance. Returns an estimate to "
        + "confirm - it does not log anything. imageBase64 is the raw file "
        + "bytes, base64-encoded, up to about 4 MB decoded.")]
    public Task<string> AnalyzeFoodImage(
        string imageBase64,
        string fileName = "meal.jpg",
        CancellationToken ct = default)
    {
        var bytes = Decode(imageBase64);
        var contentType = DetectOrThrow(bytes);

        if (contentType == "image/gif")
        {
            throw new ArgumentException("Food analysis takes a JPEG, PNG or WebP.");
        }

        return _api.PostFileAsync("/api/Nutrition/ai/analyze-image", "image", bytes, fileName, contentType, ct);
    }

    private static byte[] Decode(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new ArgumentException("imageBase64 is empty.");
        }

        // A data URI is what a browser or another tool usually hands over, so
        // accept it rather than making the caller strip the prefix.
        var payload = base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? base64[(base64.IndexOf(',') + 1)..]
            : base64;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload.Trim());
        }
        catch (FormatException)
        {
            throw new ArgumentException("imageBase64 is not valid base64.");
        }

        if (bytes.Length == 0) throw new ArgumentException("imageBase64 decoded to nothing.");

        if (bytes.Length > MaxBytes)
        {
            throw new ArgumentException(
                $"That image is {bytes.Length / 1024 / 1024} MB decoded; the limit here is 4 MB. Resize it first.");
        }

        return bytes;
    }

    /// <summary>
    /// The bytes decide, not a caller-supplied content type - the same rule the
    /// upload endpoint applies (docs/REFOCUS.md §7). Checking here as well
    /// means a wrong format is a usable message instead of a 400.
    /// </summary>
    private static string DetectOrThrow(byte[] bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
        [(byte)'G', (byte)'I', (byte)'F', ..] => "image/gif",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        _ => throw new ArgumentException("That is not a JPEG, PNG, WebP or GIF image."),
    };

    private static string Folder(string folder) => folder.ToLowerInvariant() switch
    {
        "avatars" or "avatar" => "Avatars",
        "recipes" or "recipe" => "Recipes",
        _ => throw new ArgumentException($"Unknown folder '{folder}'. Expected recipes or avatars."),
    };
}
