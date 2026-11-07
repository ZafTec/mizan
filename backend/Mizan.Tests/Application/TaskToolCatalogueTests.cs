using FluentAssertions;
using MediatR;
using Mizan.Application.Tools;
using Mizan.Application.Tools.Admin;
using Mizan.Contracts.Mcp;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The task tool catalogue's contract (docs/MCP.md#task-tools): every mode builds a real command
/// with a handler, every mode is held to an area, and every capability of
/// the legacy MCP tools is reachable from a mode or the admin registry.
/// </summary>
public class TaskToolCatalogueTests
{
    private static readonly Type[] Handlers = typeof(TaskToolRunner).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false })
        .SelectMany(t => t.GetInterfaces())
        .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
        .Select(i => i.GetGenericArguments()[0])
        .ToArray();

    /// <summary>Legacy tools the catalogue leaves out on purpose: a resource, and dropped tools.</summary>
    private static readonly HashSet<string> Dropped =
    [
        // A resource now: mizan://reference/price-regions.
        "list_price_regions",
        // Dropped from the connector.
        "ask_ai", "get_ai_conversation", "list_ai_threads", "get_ai_thread", "delete_ai_thread", "set_ai_consent", "export_profile",
    ];

    [Fact]
    public void EveryMode_BindsToARequestWithAHandler()
    {
        var modes = TaskToolCatalogue.All.SelectMany(t => t.Modes.Select(m => (Tool: t.Name, Mode: m))).ToList();
        modes.Should().NotBeEmpty();

        var unbound = modes
            .SelectMany(x => x.Mode.Requests.DefaultIfEmpty().Select(r => (x.Tool, x.Mode.Name, Request: r)))
            .Where(x => x.Request is null || !typeof(IBaseRequest).IsAssignableFrom(x.Request) || !Handlers.Contains(x.Request))
            .Select(x => $"{x.Tool}.{x.Name} -> {x.Request?.Name ?? "nothing"}")
            .ToList();

        unbound.Should().BeEmpty("each mode builds an existing MediatR request that a handler answers");

        foreach (var (tool, mode) in modes.Where(x => x.Mode.Confirm is not null))
        {
            Handlers.Should().Contain(mode.Confirm!.Request, $"{tool}.{mode.Name} previews through a real query");
        }
    }

    [Fact]
    public void EveryTool_HasAUniqueName_AndEveryModeAUniqueValue_AndAKnownArea()
    {
        TaskToolCatalogue.All.Select(t => t.Name).Should().OnlyHaveUniqueItems();
        foreach (var tool in TaskToolCatalogue.All)
        {
            tool.Modes.Select(m => m.Name).Should().OnlyHaveUniqueItems($"{tool.Name} picks a mode by value");
            if (tool.Modes.Count > 1) tool.ModeArgument.Should().NotBeNull($"{tool.Name} has several modes");
            foreach (var mode in tool.Modes)
            {
                (mode.Area is ToolMode.AnyConnection or ToolMode.AnyWrite || McpScopes.Areas.Contains(mode.Area))
                    .Should().BeTrue($"{tool.Name}.{mode.Name} needs an area, not '{mode.Area}'");
                foreach (var legacy in mode.Replaces)
                {
                    McpToolScopes.TryGet(legacy, out _).Should().BeTrue($"{tool.Name}.{mode.Name} replaces {legacy}, which is a legacy tool");
                }
            }
        }
    }

    [Fact]
    public void EveryLegacyTool_IsReachedByAMode_ExceptTheDroppedOnes()
    {
        var replaced = TaskToolCatalogue.All.SelectMany(t => t.Modes).SelectMany(m => m.Replaces).ToHashSet();

        McpToolScopes.All.Keys.Where(name => !Dropped.Contains(name) && !replaced.Contains(name)).Should().BeEmpty(
            "every personal, household, coaching and admin capability of the legacy tools has a home in the task tools");
    }

    [Fact]
    public void TheAdminRegistry_HoldsEveryLegacyAdminTool_OnceEach()
    {
        var legacy = McpToolScopes.All.Keys.Where(name => name.StartsWith("admin_", StringComparison.Ordinal)).ToHashSet();

        AdminActionRegistry.All.Select(a => a.Id).Should().OnlyHaveUniqueItems().And.BeEquivalentTo(legacy);
        AdminActionRegistry.All.GroupBy(a => a.Kind).ToDictionary(g => g.Key, g => g.Count()).Should().BeEquivalentTo(new Dictionary<AdminActionKind, int>
        {
            [AdminActionKind.ReadOnly] = 40,
            [AdminActionKind.RunsAtOnce] = 26,
            [AdminActionKind.Destructive] = 26,
            [AdminActionKind.SafetyStop] = 3,
        });
    }

    [Fact]
    public void EveryDestructiveAdminAction_HasAPreview_AndEveryActionDescribesItsArguments()
    {
        AdminActionRegistry.All.Where(a => a.Kind == AdminActionKind.Destructive).Select(a => a.Id)
            .Should().BeEquivalentTo(GetAdminActionPreviewQueryHandler.Previewed);

        foreach (var action in AdminActionRegistry.All)
        {
            Handlers.Should().Contain(action.Request, $"{action.Id} builds a request a handler answers");
            action.Schema()["type"]!.GetValue<string>().Should().Be("object");
        }

        // A password never passes through a connected app.
        AdminActionRegistry.Get("admin_update_user").Schema()["properties"]!.AsObject().ContainsKey("newPassword").Should().BeFalse();
    }

    [Fact]
    public void ThePeopleWhoUseTheConnector_SeeTwentySixTools_AndTheAssistantFifteen()
    {
        var user = new ToolCallContext(Guid.NewGuid(), ToolSurface.Mcp, "user", null);
        Visible(user).Should().Be(26);

        var assistant = user with { Surface = ToolSurface.Assistant };
        Visible(assistant).Should().Be(15);
    }

    /// <summary>
    /// A person holds one role. A trainer adds the five coaching tools, an administrator the six admin
    /// tools; no one is both, so no one sees 37. Viewing Mizan as someone else leaves no admin tool.
    /// </summary>
    [Theory]
    [InlineData("user", false, 26)]
    [InlineData("trainer", false, 31)]
    [InlineData("admin", false, 32)]
    [InlineData("admin", true, 26)]
    public void TheRoleSetsTheCeiling(string role, bool impersonating, int tools)
    {
        Visible(new ToolCallContext(Guid.NewGuid(), ToolSurface.Mcp, role, null, impersonating)).Should().Be(tools);
    }

    private static int Visible(ToolCallContext context) => TaskToolCatalogue.All.Count(t => ToolFilter.Describe(t, context) is not null);
}
