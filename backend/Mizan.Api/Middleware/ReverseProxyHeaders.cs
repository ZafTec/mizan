using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Mizan.Api.Middleware;

public static class ReverseProxyHeaders
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        var knownProxy = configuration["ReverseProxy:KnownProxy"];
        // With no override, keep the framework's loopback-only trust defaults.
        if (string.IsNullOrWhiteSpace(knownProxy)) return;
        if (!IPAddress.TryParse(knownProxy, out var address))
        {
            throw new InvalidOperationException("ReverseProxy:KnownProxy must be a single proxy IP address.");
        }

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(address);
    }
}
