using Mizan.Application.Exceptions;

namespace Mizan.Application.Interfaces;

/// <summary>
/// Tells the administrators that the AI provider refused a request for a reason retrying does
/// not fix (docs/AI.md#provider-errors). At most one notification per provider message an hour,
/// so a broken configuration does not flood the bell.
/// </summary>
public interface IAiProviderIncidents
{
    Task ReportAsync(AiProviderRejectedException refusal, CancellationToken cancellationToken = default);
}
