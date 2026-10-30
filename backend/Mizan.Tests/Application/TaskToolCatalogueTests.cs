using FluentAssertions;
using MediatR;
using Mizan.Application.Tools;
using Mizan.Contracts.Mcp;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The task tool catalogue's contract (docs/MCP.md#task-tools): every mode builds a real command
/// with a handler, every mode is held to an area, and every personal or household capability of
/// the legacy MCP tools is reachable from a mode.
/// </summary>
public class TaskToolCatalogueTests
{
    private static readonly Type[] Handlers = typeof(TaskToolRunner).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false })
        .SelectMany(t => t.GetInterfaces())
        .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
        .Select(i => i.GetGenericArguments()[0])
        .ToArray();

    /// <summary>Legacy tools the catalogue leaves out on purpose: a resource, dropped tools, and the coaching tools of phase 2.</summary>
    private static readonly HashSet<string> NotPersonal =
    [
        // A resource now: mizan://reference/price-regions.
        "list_price_regions",
        // Dropped from the connector.
        "ask_ai", "get_ai_conversation", "list_ai_threads", "get_ai_thread", "delete_ai_thread", "set_ai_consent", "export_profile",
        // Coaching tools for trainers (phase 2).
        "list_trainer_clients", "list_trainer_pending_requests", "get_client_nutrition", "get_client_workouts",
        "respond_to_trainer_request", "create_plan_for_client", "apply_template_for_client", "create_template",
        "update_template", "delete_template", "ask_ai_about_client",
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
    public void EveryPersonalAndHouseholdLegacyTool_IsReachedByAMode()
    {
        var replaced = TaskToolCatalogue.All.SelectMany(t => t.Modes).SelectMany(m => m.Replaces).ToHashSet();
        var personal = McpToolScopes.All.Keys
            .Where(name => !name.StartsWith("admin_", StringComparison.Ordinal) && !NotPersonal.Contains(name))
            .ToList();

        personal.Where(name => !replaced.Contains(name)).Should().BeEmpty(
            "every personal or household capability of the legacy tools has a home in the task tools");
    }

    [Fact]
    public void ThePeopleWhoUseTheConnector_SeeTwentySixTools_AndTheAssistantFifteen()
    {
        var user = new ToolCallContext(Guid.NewGuid(), ToolSurface.Mcp, "user", null);
        TaskToolCatalogue.All.Count(t => ToolFilter.Describe(t, user) is not null).Should().Be(26);

        var assistant = user with { Surface = ToolSurface.Assistant };
        TaskToolCatalogue.All.Count(t => ToolFilter.Describe(t, assistant) is not null).Should().Be(15);
    }
}
