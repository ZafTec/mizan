using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Domain.Usage;

namespace Mizan.Infrastructure.Ai;

/// <summary>The tier allowances as the admin area last set them, read per call like the ceilings.</summary>
public sealed class AiTierDefaults : IAiTierDefaults
{
    private readonly IRuntimeOptions<AiOptions> _settings;

    public AiTierDefaults(IRuntimeOptions<AiOptions> settings) => _settings = settings;

    public async Task<UsageLimitValues> ForAsync(AiQuotaLine line, bool isPro, CancellationToken cancellationToken = default) =>
        (await _settings.GetAsync(cancellationToken)).For(line, isPro);
}
