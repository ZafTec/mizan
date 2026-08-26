using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Composes hard preamble + editable body + soft policy, in that order. The
/// preamble is first because a later instruction that contradicts it is easier
/// to spot in a diff than one buried above.
/// </summary>
public class AiPromptResolver : IAiPromptResolver
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IMizanDbContext _context;
    private readonly ILogger<AiPromptResolver> _logger;

    public AiPromptResolver(IMizanDbContext context, ILogger<AiPromptResolver> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResolvedPrompt> ResolveAsync(string key, CancellationToken cancellationToken = default)
    {
        var published = await _context.AiPromptVersions.AsNoTracking()
            .Where(v => v.Prompt!.Key == key && v.Status == AiPromptStatus.Published)
            .Select(v => new { v.Id, v.Version, v.Body, v.SoftPolicy })
            .FirstOrDefaultAsync(cancellationToken);

        if (published is null)
        {
            // Nothing published for this key: a fresh database must not mean a
            // mute assistant, so the built-in default answers.
            return new ResolvedPrompt(null, null, Compose(AiPromptDefaults.Body(key), "{}"));
        }

        return new ResolvedPrompt(
            published.Id, published.Version, Compose(published.Body, published.SoftPolicy));
    }

    public string Compose(string body, string softPolicyJson)
    {
        var composed = new StringBuilder(AiHardConstraints.Preamble);
        composed.Append("\n\n");
        composed.Append(body.Trim());

        var policy = Parse(softPolicyJson);
        if (policy is null) return composed.ToString();

        if (!string.IsNullOrWhiteSpace(policy.Tone))
        {
            composed.Append("\n\nTone: ").Append(policy.Tone.Trim());
        }

        if (!string.IsNullOrWhiteSpace(policy.Verbosity))
        {
            composed.Append("\nLength: ").Append(policy.Verbosity.Trim());
        }

        if (policy.RefusalTopics is { Count: > 0 })
        {
            composed.Append("\nDecline and redirect on: ")
                .Append(string.Join(", ", policy.RefusalTopics.Where(t => !string.IsNullOrWhiteSpace(t))));
        }

        return composed.ToString();
    }

    private SoftPolicy? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<SoftPolicy>(json, Json);
        }
        catch (JsonException ex)
        {
            // A malformed policy degrades to the body alone rather than taking
            // the assistant down. The console validates on save.
            _logger.LogWarning(ex, "Ignoring malformed soft policy JSON");
            return null;
        }
    }

    private sealed record SoftPolicy(string? Tone, string? Verbosity, List<string>? RefusalTopics);
}
