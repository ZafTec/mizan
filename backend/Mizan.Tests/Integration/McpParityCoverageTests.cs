extern alias McpServer;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Every API endpoint is reached by an MCP tool, or is listed in <see cref="Excluded"/> with the
/// reason it has none. A new endpoint without either fails here, and so does an exclusion that no
/// longer names an endpoint or that a tool now covers.
///
/// Endpoints come from the API's controllers by reflection. The paths each tool calls come from the
/// MCP server's source, where every tool calls the API through <c>IBackendApiClient</c> with a
/// literal route.
/// </summary>
public class McpParityCoverageTests
{
    /// <summary>"METHOD api/route/*" (lower case, parameters as <c>*</c>) → why no tool reaches it.</summary>
    private static readonly Dictionary<string, string> Excluded = new(StringComparer.Ordinal)
    {
        // Signing in, the account itself and its credentials belong to the person's browser.
        ["POST api/auth/register"] = SignIn,
        ["POST api/auth/login"] = SignIn,
        ["POST api/auth/logout"] = SignIn,
        ["POST api/auth/forgot-password"] = SignIn,
        ["POST api/auth/reset-password"] = SignIn,
        ["POST api/auth/verify-email"] = SignIn,
        ["POST api/auth/resend-verification"] = SignIn,
        ["POST api/auth/change-password"] = SignIn,
        ["GET api/auth/me"] = SignIn,
        ["GET api/auth/sessions"] = SignIn,
        ["DELETE api/auth/sessions/*"] = SignIn,
        ["DELETE api/auth/account"] = "Deleting the account is the person's own act, in the web with its confirmation.",
        ["GET api/auth/external/*"] = SignIn,
        ["GET api/auth/external/callback"] = SignIn,
        ["POST api/auth/impersonation/stop"] = Impersonation,
        ["POST api/admin/users/*/impersonate"] = Impersonation,
        ["POST api/admin/users"] = "Sets a password; administrators create accounts in the console.",
        ["DELETE api/admin/users/*"] = "Permanent account deletion wants a browser and a second look (docs/AI.md#consent).",

        // OAuth and discovery: the protocol a connected app itself speaks.
        ["GET api/oauth/authorize"] = OAuth,
        ["POST api/oauth/token"] = OAuth,
        ["POST api/oauth/register"] = OAuth,
        ["POST api/oauth/revoke"] = OAuth,
        ["POST api/oauth/introspect"] = OAuth,
        ["POST api/oauth/authorization-requests/view"] = OAuth,
        ["POST api/oauth/authorization-requests/decision"] = OAuth,
        ["GET .well-known/oauth-authorization-server/api"] = OAuth,
        ["GET .well-known/openid-configuration/api"] = OAuth,
        ["GET api/.well-known/oauth-authorization-server"] = OAuth,
        ["GET api/.well-known/openid-configuration"] = OAuth,

        // A connected app does not manage connected apps: the person grants and revokes them in the web.
        ["GET api/mcpconnections"] = Connections,
        ["GET api/mcpconnections/scopes"] = Connections,
        ["GET api/mcpconnections/analytics"] = Connections,
        ["PATCH api/mcpconnections/*"] = Connections,
        ["DELETE api/mcpconnections/*"] = Connections,

        // Service-to-service calls: the MCP server, the bot and the prices worker with their own keys.
        ["POST api/mcpconnections/usage"] = Service,
        ["POST api/mcptasks"] = Service,
        ["GET api/mcptasks/*"] = Service,
        ["POST api/mcptasks/*/cancel"] = Service,
        ["POST api/mcptasks/*/complete"] = Service,
        ["POST api/mcptasks/*/fail"] = Service,
        ["POST api/telegram/resolve"] = Service,
        ["GET api/telegram/resolve/*"] = Service,
        ["DELETE api/telegram/resolve/*"] = Service,
        ["POST api/telegram/sessions"] = Service,
        ["POST api/ai/conversation/context"] = "The Telegram adapter's notes on button flows (docs/TELEGRAM.md).",
        ["GET api/internal/prices/sources"] = Service,
        ["POST api/internal/prices/observations"] = Service,
        ["POST api/internal/prices/sources/*/runs"] = Service,
        ["GET api/internal/settings/prices"] = Service,
        ["GET api/internal/settings/telegram-bot"] = Service,
        ["POST api/webhooks/paddle"] = "Paddle's webhook, signed by Paddle.",
        ["GET health"] = "The container health probe.",

        // Linking a chat or a device, and offline sync, belong to that client.
        ["GET api/telegram/link"] = Device,
        ["POST api/telegram/link"] = Device,
        ["DELETE api/telegram/link"] = Device,
        ["GET api/devices"] = Device,
        ["POST api/devices"] = Device,
        ["DELETE api/devices/*"] = Device,
        ["GET api/sync/changes"] = Device,

        // Money moves through Paddle's checkout and portal in a browser.
        ["GET api/subscriptions/plans"] = Billing,
        ["GET api/subscriptions/checkout-settings"] = Billing,
        ["GET api/subscriptions/transactions"] = Billing,
        ["GET api/subscriptions/transactions/*/invoice"] = Billing,
        ["POST api/subscriptions/cancel"] = Billing,
        ["POST api/subscriptions/resume"] = Billing,
        ["POST api/subscriptions/change-plan"] = Billing,
        ["POST api/subscriptions/change-plan/preview"] = Billing,
        ["POST api/subscriptions/portal"] = Billing,
        ["GET api/admin/billing"] = AdminBilling,
        ["POST api/admin/billing/plans"] = AdminBilling,
        ["PUT api/admin/billing/plans/*"] = AdminBilling,
        ["POST api/admin/billing/plans/*/price"] = AdminBilling,
        ["POST api/admin/billing/plans/*/archive"] = AdminBilling,
        ["POST api/admin/billing/plans/import"] = AdminBilling,
        ["POST api/admin/billing/discounts"] = AdminBilling,
        ["POST api/admin/billing/discounts/*/archive"] = AdminBilling,

        // Public pages and signed links, reached without an account.
        ["POST api/contact"] = Public,
        ["GET api/social/share/*"] = Public,
        ["POST api/notifications/opt-out"] = Public,

        // Web forms whose job a tool already does another way.
        ["POST api/meals/preview"] = "The meal form's running total; log_food, log_meal and log_day compute on the server.",
        ["POST api/nutrition/log"] = "The web's quick log; log_food and log_meal reach the same diary rules.",
        ["POST api/recipes"] = "Answers 410: recipes are made from logged meals (promote_to_recipe).",
        ["POST api/ai/chat/image"] = "The web chat's photo upload; upload_image and analyze_food_image do it for an app.",
        ["GET api/ai/onboarding"] = Onboarding,
        ["GET api/ai/onboarding/tools"] = Onboarding,
        ["POST api/ai/onboarding"] = Onboarding,
        ["DELETE api/households/*"] = "Deleting a household removes what other members share; the web shows the preview and asks.",
        ["GET api/households/*/deletion"] = "The preview for that web confirmation.",

        // File downloads and uploads, and checks that spend a provider call, stay in the console.
        ["GET api/admin/usage/export"] = Files,
        ["GET api/auditlogs/export"] = Files,
        ["POST api/uploads/model"] = Files,
        ["PUT api/exercises/*/model"] = Files,
        ["POST api/admin/settings/ai/test"] = "Spends a provider call to check the AI settings; run it from the console.",
        ["GET api/admin/mail/announcements/audience"] = "The count the announcement form shows; admin_send_announcement sends.",
    };

    private const string SignIn = "Sign-in and account credentials belong to the person's browser.";
    private const string Impersonation = "Impersonation is a browser session for support, not something an app should start.";
    private const string OAuth = "OAuth and discovery, the protocol a connected app speaks to connect.";
    private const string Connections = "A connected app does not manage connected apps; the person does that in the web.";
    private const string Service = "A service-to-service call with its own key, not a person's feature.";
    private const string Device = "Linking a chat or device, and its offline sync, belongs to that client.";
    private const string Billing = "Payments go through Paddle's checkout and portal in a browser; get_my_subscription reads the plan.";
    private const string AdminBilling = "Changes live prices in Paddle; administrators do it in the console with Paddle's preview.";
    private const string Public = "A public page or signed link, reached without an account.";
    private const string Onboarding = "The web's first-run conversation; an app uses ask_ai and the logging tools.";
    private const string Files = "A file download or upload for the console.";

    [Fact]
    public void EveryEndpoint_HasAnMcpTool_OrAStatedReason()
    {
        var endpoints = ApiEndpoints().Select(e => e.Key).ToHashSet();
        var calls = ToolCalls();

        var uncovered = endpoints.Where(e => !Covered(e, calls)).Order().ToList();
        uncovered.Except(Excluded.Keys).Should().BeEmpty(
            "each endpoint needs an MCP tool or an entry in Excluded with the reason it has none");

        Excluded.Keys.Except(endpoints).Should().BeEmpty("an exclusion must name an endpoint that exists");
        Excluded.Keys.Where(k => Covered(k, calls)).Should().BeEmpty("a tool covers these now; drop the exclusion");
        Excluded.Values.Should().OnlyContain(reason => !string.IsNullOrWhiteSpace(reason));
    }


    /// <summary>
    /// A tool's scope matches the API's own check: a tool that reaches an administrators' endpoint needs the admin
    /// scope, and one that reaches a coaches' endpoint needs a trainer scope (or ai:use, which bills the coach).
    /// </summary>
    [Fact]
    public void EachToolsScope_MatchesTheEndpointsItCalls()
    {
        var endpoints = ApiEndpoints();
        var wrong = new List<string>();
        foreach (var (tool, calls) in ToolCallsByTool())
        {
            if (!McpServer::Mizan.Mcp.Server.Authorization.McpToolScopes.TryGet(tool, out var scope))
            {
                wrong.Add($"{tool}: no scope");
                continue;
            }
            var policies = endpoints
                .Where(e => calls.Any(c => Covered(e.Key, [c])))
                .Select(e => e.Policy)
                .ToList();
            if (policies.Any(p => p.Contains("RequireAdmin")) && scope != Mizan.Contracts.Mcp.McpScopes.Admin)
                wrong.Add($"{tool}: calls an admin endpoint under {scope}");
            if (policies.Any(p => p.Contains("RequireTrainer")) && !scope.StartsWith("trainer:") && scope != Mizan.Contracts.Mcp.McpScopes.AiUse)
                wrong.Add($"{tool}: calls a coach endpoint under {scope}");
        }
        wrong.Should().BeEmpty();
    }
    private static bool Covered(string endpoint, IReadOnlyList<(string Method, string Path)> calls)
    {
        var method = endpoint[..endpoint.IndexOf(' ')];
        var path = endpoint[(endpoint.IndexOf(' ') + 1)..];
        return calls.Any(c => (c.Method == method || c.Method == "?") && SamePath(c.Path, path));
    }

    /// <summary>
    /// The tool's route against the endpoint's, segment by segment. An interpolated segment in the tool (<c>*</c>)
    /// matches anything; an endpoint parameter matches only an interpolated segment, so a literal never stands in for a parameter.
    /// </summary>
    private static bool SamePath(string a, string b)
    {
        var x = a.Split('/');
        var y = b.Split('/');
        return x.Length == y.Length && x.Zip(y).All(p => p.First == p.Second || p.First == "*");
    }

    internal sealed record Endpoint(string Key, string Controller, string Policy);

    internal static IReadOnlyList<Endpoint> ApiEndpoints()
    {
        var controllers = typeof(Mizan.Api.Controllers.MealsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);
        var result = new List<Endpoint>();
        foreach (var controller in controllers)
        {
            var name = controller.Name.EndsWith("Controller") ? controller.Name[..^"Controller".Length] : controller.Name;
            var prefixes = controller.GetCustomAttributes<RouteAttribute>(true).Select(r => r.Template.Replace("[controller]", name)).DefaultIfEmpty("").ToList();
            var classPolicy = Policy(controller.GetCustomAttributes<AuthorizeAttribute>(true), controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any());
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var policy = action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ? "anonymous"
                    : Policy(action.GetCustomAttributes<AuthorizeAttribute>(true), false) is { Length: > 0 } own && own != "anonymous" ? own : classPolicy;
                foreach (var verb in action.GetCustomAttributes<HttpMethodAttribute>(true))
                    foreach (var method in verb.HttpMethods)
                        foreach (var prefix in prefixes)
                        {
                            var template = verb.Template ?? "";
                            var route = template.StartsWith('/') || template.StartsWith("~/")
                                ? template.TrimStart('~', '/')
                                : string.Join('/', new[] { prefix, template }.Where(s => s.Length > 0));
                            result.Add(new Endpoint($"{method.ToUpperInvariant()} {Normalize(route)}", name, policy));
                        }
            }
        }
        return result.DistinctBy(e => e.Key).ToList();
    }

    private static string Policy(IEnumerable<AuthorizeAttribute> attributes, bool anonymous)
    {
        if (anonymous) return "anonymous";
        var list = attributes.ToList();
        if (list.Count == 0) return "";
        return string.Join("+", list.Select(a => a.Policy ?? (a.Roles is { } r ? "role:" + r : "authenticated")));
    }

    /// <summary>Lower case, no leading slash or query, each <c>{parameter}</c> as <c>*</c>.</summary>
    internal static string Normalize(string route)
    {
        route = route.Split('?')[0].Trim('/');
        route = Regex.Replace(route, @"\{[^}]*\}", "*");
        return route.ToLowerInvariant();
    }

    internal static IReadOnlyList<(string Method, string Path)> ToolCalls() =>
        ToolCallsByTool().SelectMany(kv => kv.Value).Distinct().ToList();

    /// <summary>The routes each MCP tool calls, read from the MCP server's tool sources.</summary>
    internal static Dictionary<string, List<(string Method, string Path)>> ToolCallsByTool()
    {
        var result = new Dictionary<string, List<(string, string)>>();
        foreach (var file in Directory.GetFiles(Path.Combine(BackendRoot(), "Mizan.Mcp.Server"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var source = File.ReadAllText(file);
            foreach (Match constant in Regex.Matches(source, @"const string (\w+) = ""(/api[^""]*)"""))
                source = source.Replace("{" + constant.Groups[1].Value + "}", constant.Groups[2].Value)
                    .Replace("(" + constant.Groups[1].Value + ",", "(\"" + constant.Groups[2].Value + "\",");
            // One-line braces are interpolation holes (or harmless initialisers): {ParseId(id, "id")} becomes {x}.
            for (var before = ""; before != source;)
            {
                before = source;
                source = Regex.Replace(source, @"\{[^{}\n]*\}", "{x}");
            }
            var parts = source.Split("[McpServerTool(Name = \"");
            foreach (var part in parts.Skip(1))
            {
                var name = part[..part.IndexOf('"')];
                var calls = new List<(string, string)>();
                foreach (Match call in Regex.Matches(part, @"_api\.(Get|Post|Put|Patch|Delete)\w*Async\(\s*\$?""([^""]+)"""))
                    calls.Add((call.Groups[1].Value.ToUpperInvariant(), ToolPath(call.Groups[2].Value)));
                // A route built before the call (a ternary, a variable) takes the verb of the call it precedes or sits in.
                foreach (Match literal in Regex.Matches(part, @"""(/api/[^""]+)"""))
                {
                    var enclosing = Regex.Matches(part[..literal.Index], @"_api\.(Get|Post|Put|Patch|Delete)\w*Async").LastOrDefault();
                    var verb = enclosing is not null && !part[enclosing.Index..literal.Index].Contains(';')
                        ? enclosing
                        : Regex.Match(part[literal.Index..], @"_api\.(Get|Post|Put|Patch|Delete)\w*Async");
                    calls.Add((verb.Success ? verb.Groups[1].Value.ToUpperInvariant() : "?", ToolPath(literal.Groups[1].Value)));
                }
                result[name] = calls.Distinct().ToList();
            }
        }
        return result;
    }

    /// <summary>An interpolated route: an interpolation inside a segment is a parameter, one glued on the end is a query.</summary>
    private static string ToolPath(string route)
    {
        route = Regex.Replace(route, @"(?<!/)\{x\}", "");
        return Normalize(route);
    }

    private static string BackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Mizan.Mcp.Server")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The backend folder was not found above the test output.");
    }
}
