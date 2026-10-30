using System.Net;
using System.Net.Http.Headers;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Media;
using Mizan.Infrastructure.Identity;

namespace Mizan.Infrastructure.Media;

/// <summary>
/// Downloads an image from an address a client gave us. The address is caller-controlled, so this is a
/// server-side request forgery risk, guarded the way client metadata documents are: https on the default port
/// only, a host given as an address must be public, the connection goes only to addresses checked after DNS
/// resolution (<see cref="OAuthClientMetadataFetcher.GuardedConnectAsync"/>), redirects are not followed, and the
/// download stops at <see cref="MaxBytes"/> or <see cref="Timeout"/>. The bytes, not the Content-Type header,
/// decide whether it is an image.
/// </summary>
public sealed class RemoteImageFetcher : IRemoteImageFetcher
{
    public const string HttpClientName = "remote-image";
    public const int MaxBytes = 4 * 1024 * 1024;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory _factory;

    public RemoteImageFetcher(IHttpClientFactory factory) => _factory = factory;

    public async Task<FetchedImage> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var uri = ValidateUrl(url);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        byte[] bytes;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            using var response = await _factory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if ((int)response.StatusCode is >= 300 and < 400)
                throw new DomainValidationException("That image address redirects elsewhere. Pass the address it ends at.");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new DomainValidationException($"The image could not be downloaded: the server answered {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength > MaxBytes)
                throw TooLarge();

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes) throw TooLarge();
            }

            bytes = buffer.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DomainValidationException($"The image took longer than {Timeout.TotalSeconds:0} seconds to download.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new DomainValidationException("The image could not be downloaded from that address. It must be a public https address.");
        }

        var contentType = ImageFormat.Detect(bytes.AsSpan(0, Math.Min(ImageFormat.HeaderBytes, bytes.Length)))
            ?? throw new DomainValidationException("That address does not serve a JPEG, PNG, WebP or GIF image.");
        return new FetchedImage(bytes, contentType);
    }

    /// <summary>
    /// Refuses, before any connection, what the address alone shows to be unsafe. A name that resolves to a
    /// private address is refused at connect time instead.
    /// </summary>
    public static Uri ValidateUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new DomainValidationException("imageUrl must be an absolute https address.");
        if (!uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            throw new DomainValidationException("imageUrl must use the default https port and carry no credentials.");
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && !OAuthClientMetadataFetcher.IsPublicAddress(address)))
            throw new DomainValidationException("imageUrl must be a public address, not a private, loopback or link-local one.");
        return uri;
    }

    private static DomainValidationException TooLarge() =>
        new($"That image is larger than {MaxBytes / 1024 / 1024} MB.");
}
