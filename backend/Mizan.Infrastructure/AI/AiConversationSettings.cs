using Mizan.Application.Ai;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

/// <summary>Reads <c>Ai.ConversationGapHours</c> per call, so a change in the admin area applies at once.</summary>
public sealed class AiConversationSettings : IAiConversationSettings
{
    private readonly IRuntimeOptions<AiOptions> _options;

    public AiConversationSettings(IRuntimeOptions<AiOptions> options) => _options = options;

    public async ValueTask<TimeSpan> GapAsync(CancellationToken cancellationToken = default)
    {
        var hours = (await _options.GetAsync(cancellationToken)).ConversationGapHours;
        return TimeSpan.FromHours(hours > 0 ? hours : 3);
    }
}
