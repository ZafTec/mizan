using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Application.Tools;
using Mizan.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Mizan.Tests.Eval;

/// <summary>One tool call in a conversation, and whether it counts against the toolset.</summary>
public sealed record EvalCall(string Tool, string? Mode, bool IsError, bool Wrong, string Arguments, string Result);

public sealed record EvalRun(string Arm, string Case, string Group, int Run, bool Success, string? Failure, IReadOnlyList<EvalCall> Calls,
    int PromptTokens, int CompletionTokens, int ModelTurns, string Answer);

/// <summary>
/// Runs one live model against both MCP toolsets with the same seeded data and the same requests, and scores each
/// run by the database state or the answer (docs/MCP.md#evaluating-the-toolsets). Opt-in: see <see cref="ToolEvalFactAttribute"/>.
///
/// <c>MIZAN_TOOL_EVAL_RUNS</c> (3), <c>MIZAN_TOOL_EVAL_PARALLEL</c> (3), <c>MIZAN_TOOL_EVAL_ARMS</c> (legacy,tasks) and
/// <c>MIZAN_TOOL_EVAL_CASES</c> (all; a comma list of case ids or groups) narrow a run. Results are written to
/// <c>MIZAN_TOOL_EVAL_OUT</c>, by default the test's TestResults folder, which is not committed.
/// </summary>
[Collection("ApiIntegration")]
public class ToolsetEvalTests(ApiTestFixture fixture, ITestOutputHelper output)
{
    private const int MaxModelTurns = 16;
    private const int MaxResultChars = 20_000;

    [ToolEvalFact]
    public async Task Both_toolsets_serve_the_same_requests()
    {
        var runs = int.TryParse(Environment.GetEnvironmentVariable("MIZAN_TOOL_EVAL_RUNS"), out var r) ? r : 3;
        var parallel = int.TryParse(Environment.GetEnvironmentVariable("MIZAN_TOOL_EVAL_PARALLEL"), out var p) ? p : 3;
        var arms = (Environment.GetEnvironmentVariable("MIZAN_TOOL_EVAL_ARMS") ?? "legacy,tasks").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var only = Environment.GetEnvironmentVariable("MIZAN_TOOL_EVAL_CASES")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cases = EvalCases.All.Where(c => only is null || only.Contains(c.Id) || only.Contains(c.Group)).ToList();
        var outDir = Environment.GetEnvironmentVariable("MIZAN_TOOL_EVAL_OUT") ?? Path.Combine(AppContext.BaseDirectory, "TestResults", "tool-eval");
        Directory.CreateDirectory(outDir);

        await fixture.ResetDatabaseAsync();
        await RaiseAiCeilingsAsync();
        fixture.Ai.Fallback = request => request.ResponseSchema?.Name == "food_analysis_v1"
            ? new AiCompletionResponse(EvalWorld.SoupEstimate, new AiTokenUsage(40, 60), "test-model")
            : null;
        await EvalWorld.SeedCatalogueAsync(fixture);

        using var model = EvalModel.FromEnvironment();
        var results = new ConcurrentBag<EvalRun>();
        var log = new StreamWriter(Path.Combine(outDir, $"runs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jsonl")) { AutoFlush = true };
        var logLock = new object();

        foreach (var arm in arms)
        {
            await using var server = new EvalMcpArm(arm, fixture);
            var work = cases.SelectMany(c => Enumerable.Range(1, runs).Select(n => (Case: c, Run: n))).ToList();
            using var gate = new SemaphoreSlim(parallel);
            var index = 0;
            await Task.WhenAll(work.Select(async item =>
            {
                await gate.WaitAsync();
                try
                {
                    var world = await EvalWorld.SeedAsync(fixture, Interlocked.Increment(ref index));
                    var run = await RunAsync(model, server, item.Case, item.Run, world);
                    results.Add(run);
                    lock (logLock)
                    {
                        log.WriteLine(JsonSerializer.Serialize(run));
                        output.WriteLine($"{arm,-6} {item.Case.Id,-26} #{item.Run} {(run.Success ? "ok  " : "FAIL")} calls={run.Calls.Count} wrong={run.Calls.Count(x => x.Wrong)} tokens={run.PromptTokens + run.CompletionTokens} {run.Failure}");
                    }
                }
                finally
                {
                    gate.Release();
                }
            }));
        }
        log.Dispose();

        var report = Report(results.ToList(), model.Model, runs);
        await File.WriteAllTextAsync(Path.Combine(outDir, "summary.md"), report);
        output.WriteLine(report);
    }

    private async Task<EvalRun> RunAsync(EvalModel model, EvalMcpArm server, EvalCase evalCase, int run, EvalWorld world)
    {
        var actor = evalCase.Actor switch
        {
            EvalActor.Trainer => world.Trainer,
            EvalActor.Admin => world.Admin,
            _ => world.User,
        };
        var access = await fixture.CreateMcpAccessAsync(actor);
        using var client = server.Connect(access.Token);
        var instructions = await EvalMcpArm.InstructionsAsync(client);
        var tools = await EvalMcpArm.ToolsAsync(client);

        var system = $"""
            You are an assistant connected to the person's Mizan account through the Mizan MCP connector.
            Today is {world.Today.DayOfWeek}, {world.Today:yyyy-MM-dd}. The person's time zone is UTC.
            Carry out the person's request with the tools. When the request is clear, act on it; ask only when you cannot go on without an answer. Reply briefly when you are done.

            Instructions from the Mizan server:
            {instructions}
            """;
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = system },
            new JsonObject { ["role"] = "user", ["content"] = evalCase.Prompt(world) },
        };

        var calls = new List<EvalCall>();
        var answer = new StringBuilder();
        int prompt = 0, completion = 0, turns = 0;
        var followedUp = false;
        string? failure = "the model did not finish";

        while (turns < MaxModelTurns)
        {
            ModelReply reply;
            try
            {
                reply = await model.CompleteAsync(messages, tools, CancellationToken.None);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                break;
            }
            turns++;
            prompt += reply.PromptTokens;
            completion += reply.CompletionTokens;
            messages.Add(reply.Message);
            if (!string.IsNullOrWhiteSpace(reply.Content)) answer.AppendLine(reply.Content);

            if (reply.ToolCalls.Count == 0)
            {
                failure = await evalCase.Check(new EvalContext(fixture, world, answer.ToString()));
                if (failure is null || followedUp) break;
                // The person answers once, as they would when asked to confirm or to choose.
                followedUp = true;
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = EvalCases.FollowUpFor(evalCase) });
                continue;
            }

            foreach (var call in reply.ToolCalls)
            {
                McpCallOutcome outcome;
                JsonObject? arguments = null;
                try
                {
                    arguments = string.IsNullOrWhiteSpace(call.Arguments) ? new JsonObject() : JsonNode.Parse(call.Arguments) as JsonObject;
                }
                catch (JsonException)
                {
                }
                outcome = arguments is null
                    ? new McpCallOutcome("The arguments are not a JSON object.", true)
                    : await EvalMcpArm.CallAsync(client, call.Name, arguments);

                var mode = server.Toolset == "tasks" ? ModeOf(call.Name, arguments) : null;
                var allowed = server.Toolset == "tasks" ? evalCase.Tasks : evalCase.Legacy;
                var serves = allowed.Contains(call.Name) || (mode is not null && allowed.Contains($"{call.Name}:{mode}"));
                calls.Add(new EvalCall(call.Name, mode, outcome.IsError, !serves || outcome.IsError, call.Arguments, Clip(outcome.Text, 2000)));

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = call.Id,
                    ["content"] = Clip(outcome.IsError ? "Error: " + outcome.Text : outcome.Text, MaxResultChars),
                });
            }
        }

        return new EvalRun(server.Toolset, evalCase.Id, evalCase.Group, run, failure is null, failure, calls, prompt, completion, turns, Clip(answer.ToString(), 2000));
    }

    /// <summary>The mode a task tool call selects: its mode argument, or the tool's default. Null for a legacy tool.</summary>
    private static string? ModeOf(string tool, JsonObject? arguments)
    {
        var task = TaskToolCatalogue.Find(tool);
        if (task?.ModeArgument is null) return null;
        return arguments?[task.ModeArgument] is JsonValue value && value.TryGetValue<string>(out var mode) ? mode : task.DefaultMode;
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max] + " [cut]";

    private async Task RaiseAiCeilingsAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IRuntimeSettings>();
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["Ai.Pro.DailyRequests"] = "10000",
            ["Ai.Pro.DailyTokens"] = "100000000",
            ["Ai.GlobalDailyTokens"] = "100000000",
        })
        {
            await settings.SetAsync(key, value, null);
        }
        fixture.Services.GetRequiredService<Mizan.Infrastructure.Settings.RuntimeSettingsCache>().Invalidate();
    }

    public static string Report(IReadOnlyList<EvalRun> runs, string model, int perCase)
    {
        var text = new StringBuilder();
        text.AppendLine($"Model `{model}`, {runs.Select(r => r.Case).Distinct().Count()} requests, {perCase} runs each per arm.");
        text.AppendLine();
        text.AppendLine("| Arm | Success | Wrong calls | Calls | Prompt tokens | Completion tokens | Total tokens |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var arm in runs.GroupBy(r => r.Arm).OrderBy(g => g.Key))
        {
            var total = arm.Count();
            var ok = arm.Count(r => r.Success);
            text.AppendLine($"| {arm.Key} | {ok}/{total} ({100.0 * ok / total:0.0}%) | {arm.Sum(r => r.Calls.Count(c => c.Wrong))} | {arm.Sum(r => r.Calls.Count)} | "
                + $"{arm.Sum(r => (long)r.PromptTokens):N0} | {arm.Sum(r => (long)r.CompletionTokens):N0} | {arm.Sum(r => (long)r.PromptTokens + r.CompletionTokens):N0} |");
        }
        text.AppendLine();
        text.AppendLine("| Group | " + string.Join(" | ", runs.Select(r => r.Arm).Distinct().Order().Select(a => $"{a} success / wrong")) + " |");
        text.AppendLine("| --- |" + string.Concat(runs.Select(r => r.Arm).Distinct().Select(_ => " ---: |")));
        foreach (var group in runs.GroupBy(r => r.Group))
        {
            text.Append($"| {group.Key} |");
            foreach (var arm in runs.Select(r => r.Arm).Distinct().Order())
            {
                var mine = group.Where(r => r.Arm == arm).ToList();
                text.Append($" {mine.Count(r => r.Success)}/{mine.Count} / {mine.Sum(r => r.Calls.Count(c => c.Wrong))} |");
            }
            text.AppendLine();
        }
        text.AppendLine();
        text.AppendLine("Failures:");
        foreach (var failed in runs.Where(r => !r.Success).OrderBy(r => r.Arm).ThenBy(r => r.Case))
        {
            text.AppendLine($"- {failed.Arm} {failed.Case} #{failed.Run}: {failed.Failure}");
        }
        return text.ToString();
    }
}
