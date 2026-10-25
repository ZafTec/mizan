using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Mizan.Mcp.Server;

/// <summary>
/// Trusts X-Forwarded-For and X-Forwarded-Proto from the loopback address by default,
/// or from the one proxy named in ReverseProxy:KnownProxy. Any other caller cannot
/// rewrite the client address or scheme the server records.
/// </summary>
public static class ReverseProxyHeaders
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        var knownProxy = configuration["ReverseProxy:KnownProxy"];
        // With no override, keep the framework's loopback-only trust defaults.
        if (string.IsNullOrWhiteSpace(knownProxy)) return;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        if (IPAddress.TryParse(knownProxy, out var address))
        {
            options.KnownProxies.Add(address);
            return;
        }

        // Not a literal IP: resolve it as a hostname (e.g. a Docker service name).
        IPAddress[] resolved;
        try
        {
            resolved = Dns.GetHostAddresses(knownProxy);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"ReverseProxy:KnownProxy '{knownProxy}' is not a valid IP address and could not be resolved as a hostname.", ex);
        }

        if (resolved.Length == 0)
        {
            throw new InvalidOperationException($"ReverseProxy:KnownProxy '{knownProxy}' did not resolve to any address.");
        }

        foreach (var ip in resolved)
        {
            options.KnownProxies.Add(ip);
        }
    }
}
