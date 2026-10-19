using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Mizan.Application.Interfaces;
using Mizan.Application.OAuth;

namespace Mizan.Infrastructure.Identity;

/// <summary>
/// Fetches a client metadata document. The URL comes from an unauthenticated
/// request, so this is a server-side request forgery risk and it is guarded in
/// three places: only https on port 443, the connection goes to an address we
/// checked ourselves (so DNS cannot change its answer between check and
/// connect), and the response is small, JSON, and not redirected.
/// </summary>
public sealed class OAuthClientMetadataFetcher : IOAuthClientMetadataFetcher
{
    public const string HttpClientName = "oauth-client-metadata";
    private const int MaxBytes = 32 * 1024;

    private readonly IHttpClientFactory _factory;

    public OAuthClientMetadataFetcher(IHttpClientFactory factory) => _factory = factory;

    public async Task<OAuthClientMetadata> FetchAsync(string clientIdUrl, CancellationToken cancellationToken)
    {
        var uri = ValidateUrl(clientIdUrl);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        string body;
        try
        {
            var http = _factory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (!response.IsSuccessStatusCode)
                throw new OAuthException("invalid_client", "The client metadata document could not be loaded.");
            if (response.Content.Headers.ContentType?.MediaType is not "application/json" and not "application/jrd+json")
                throw new OAuthException("invalid_client", "The client metadata document is not JSON.");
            if (response.Content.Headers.ContentLength > MaxBytes)
                throw new OAuthException("invalid_client", "The client metadata document is too large.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                    throw new OAuthException("invalid_client", "The client metadata document is too large.");
            }

            body = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (OAuthException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            throw new OAuthException("invalid_client", "The client metadata document could not be loaded.");
        }

        return Parse(clientIdUrl, body);
    }

    /// <summary>https, no credentials, no fragment, default port, a real host name, and a path.</summary>
    public static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length != 0
            || uri.Fragment.Length != 0
            || !uri.IsDefaultPort
            || uri.AbsolutePath.Length <= 1
            || uri.HostNameType != UriHostNameType.Dns
            || !uri.Host.Contains('.'))
        {
            throw new OAuthException("invalid_client", "client_id is not an acceptable metadata document URL.");
        }

        return uri;
    }

    public static OAuthClientMetadata Parse(string clientIdUrl, string json)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            throw new OAuthException("invalid_client", "The client metadata document is not valid JSON.");
        }

        if (root.ValueKind != JsonValueKind.Object || Str(root, "client_id") != clientIdUrl)
            throw new OAuthException("invalid_client", "The document's client_id must equal the URL it was served from.");

        // A document may only describe a public client. A secret or key would be
        // something we cannot honor, and ignoring it would give a false sense of identity.
        if (Str(root, "token_endpoint_auth_method") is { } method && method != "none")
            throw new OAuthException("invalid_client", "Only public clients are supported.");
        if (root.TryGetProperty("client_secret", out _))
            throw new OAuthException("invalid_client", "A metadata document must not contain a client_secret.");

        var redirects = new List<string>();
        if (root.TryGetProperty("redirect_uris", out var uris) && uris.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in uris.EnumerateArray().Take(10))
            {
                var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (!RedirectUriRules.TryValidate(value, out _)) continue;
                redirects.Add(value!);
            }
        }

        if (redirects.Count == 0)
            throw new OAuthException("invalid_client", "The document lists no usable redirect_uris.");

        var name = Str(root, "client_name")?.Trim();
        if (string.IsNullOrEmpty(name)) name = new Uri(clientIdUrl).Host;
        if (name.Length > 100) name = name[..100];

        return new OAuthClientMetadata(clientIdUrl, name, HttpsOrNull(Str(root, "logo_uri")), HttpsOrNull(Str(root, "client_uri")), redirects);
    }

    /// <summary>True only for addresses on the public internet.</summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return false; // unique local fc00::/7
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) return false; // documentation
            return true;
        }

        var b = address.GetAddressBytes();
        return !(b[0] == 10
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] is >= 64 and <= 127) // carrier-grade NAT
            || b[0] == 0
            || b[0] >= 224
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)
            || (b[0] == 198 && b[1] is 18 or 19)
            || (b[0] == 192 && b[1] == 0 && b[2] == 2)
            || (b[0] == 198 && b[1] == 51 && b[2] == 100)
            || (b[0] == 203 && b[1] == 0 && b[2] == 113));
    }

    /// <summary>
    /// Connects to a resolved address only after checking it. Because the check
    /// and the connection use the same address, a name that resolves to a public
    /// address first and a private one second cannot get through.
    /// </summary>
    public static async ValueTask<Stream> GuardedConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var allowed = addresses.Where(IsPublicAddress).ToArray();
        if (allowed.Length == 0 || allowed.Length != addresses.Length)
            throw new HttpRequestException("The host resolves to an address that is not allowed.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? HttpsOrNull(string? value) =>
        value is { Length: <= 512 } && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? value
            : null;
}
