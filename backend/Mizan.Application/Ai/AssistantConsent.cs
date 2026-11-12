using Microsoft.EntityFrameworkCore;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Interfaces;
using Mizan.Application.Tools;
using Mizan.Contracts.Ai;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

/// <summary>
/// What the person allowed an assistant acting for them to do, applied to one tool mode. It is the one rule for
/// every assistant: the in-app chat (<c>AiToolRunner</c>) and a hosted assistant through the gateway
/// (<see cref="TaskToolRunner"/>). Reading an area needs its share switch; changing it needs <c>allowWrites</c> and
/// its write switch; a change outside the three areas needs <c>allowWrites</c>. A connected app the person granted
/// scopes to is not held to it: the grant is their choice for that app (docs/AI.md#consent, docs/MCP.md).
/// </summary>
public static class AssistantConsent
{
    /// <summary>The axis a refusal names when a change touches none of the three areas: the master write switch.</summary>
    public const string AnyArea = "any";

    public static AiToolAccess AccessOf(ToolMode mode) => mode.Writes ? AiToolAccess.Write : AiToolAccess.Read;

    /// <summary>Null when the person's consent allows the mode, else the switch they would turn on.</summary>
    public static async Task<AiConsentRequirement?> RefusalAsync(
        IMizanDbContext context, Guid userId, ToolMode mode, CancellationToken cancellationToken) =>
        Refusal(await LoadAsync(context, userId, cancellationToken), AiToolCatalogue.AxisOf(mode), AccessOf(mode));

    /// <summary>Null when the person's consent allows the access, else the switch they would turn on.</summary>
    public static async Task<AiConsentRequirement?> RefusalAsync(
        IMizanDbContext context, Guid userId, DataAxis? axis, AiToolAccess access, CancellationToken cancellationToken) =>
        Refusal(await LoadAsync(context, userId, cancellationToken), axis, access);

    public static AiConsentRequirement? Refusal(UserAiConsent consent, DataAxis? axis, AiToolAccess access)
    {
        if (axis is { } area)
        {
            var allowed = access == AiToolAccess.Write ? consent.AllowsWrite(area) : consent.Allows(area);
            return allowed ? null : AiConsentRequirements.For(area, access);
        }

        // Account, social, household and coaching modes hold no nutrition, training or body data. Reading them is
        // open; changing anything still needs the person to have let the assistant make changes at all.
        if (access == AiToolAccess.Read || consent.AllowWrites) return null;
        return new AiConsentRequirement(AiErrorCodes.ConsentRequired, AnyArea, "write");
    }

    /// <summary>The sentence a model passes on: what is off and where the person turns it on.</summary>
    public static string Explain(AiConsentRequirement requirement)
    {
        var what = requirement.Axis switch
        {
            "training" => "training",
            "body" => "body measurements",
            "nutrition" => "food, goals and meal plans",
            _ => "account",
        };
        var verb = requirement.Access == "write" ? "change" : "read";
        var where = requirement.Axis == AnyArea
            ? "the switch that lets the assistant make changes"
            : $"the {requirement.Access} switch for {what}";
        return $"The person has not allowed the Mizan assistant to {verb} their {what}. "
            + $"They can turn on {where} in Mizan under Settings, Assistant.";
    }

    private static async Task<UserAiConsent> LoadAsync(IMizanDbContext context, Guid userId, CancellationToken cancellationToken) =>
        // No row means never asked, which means no.
        await context.UserAiConsents.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
        ?? UserAiConsent.None(userId);
}
