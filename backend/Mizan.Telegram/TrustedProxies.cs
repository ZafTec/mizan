using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Mizan.Telegram;

/// <summary>
/// Which callers may say whose request they forward. The Mini App session records the
/// browser's address, and the request reaches the bot through the reverse proxy or the
/// Next.js route handler, so the bot reads <c>X-Forwarded-For</c> from those hops only
/// (<see cref="TelegramBotOptions.TrustedProxies"/>). Nothing configured trusts loopback
/// alone, the framework's default, so a header from anywhere else is ignored.
/// </summary>
public static class TrustedProxies
{
    public static void Configure(ForwardedHeadersOptions options, string? trustedProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

        var entries = (trustedProxies ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0) return;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var entry in entries)
        {
            if (IPAddress.TryParse(entry, out var address)) options.KnownProxies.Add(address);
            else if (System.Net.IPNetwork.TryParse(entry, out var network)) options.KnownIPNetworks.Add(network);
            else throw new InvalidOperationException($"TelegramBot:TrustedProxies has '{entry}', which is neither an IP address nor a CIDR range.");
        }

        // Walk the header from the right past every trusted hop (the proxy, then Next.js)
        // and stop at the first address that is not one: that is the browser.
        options.ForwardLimit = null;
    }
}
