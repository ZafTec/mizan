using Mizan.Application.Ai.Tools;
using Mizan.Contracts.Ai;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;

namespace Mizan.Application.Ai;

/// <summary>
/// The typed refusal for a tool call the person's consent did not allow. Every surface reads the
/// same record, so the web offers an <b>Allow</b> button, MCP names the setting and Telegram links
/// to it, all for the switch named here (docs/AI.md#consent-refusals).
/// </summary>
public static class AiConsentRequirements
{
    public static AiConsentRequirement For(DataAxis axis, AiToolAccess access) => new(
        AiErrorCodes.ConsentRequired,
        UserAiConsent.SwitchFor(axis) switch
        {
            DataAxis.Training => "training",
            DataAxis.Body => "body",
            _ => "nutrition",
        },
        access == AiToolAccess.Write ? "write" : "read");
}
