namespace Mizan.Application.Interfaces;

public record AiContext(string Summary, Guid? HouseholdId, IReadOnlyList<string> IncludedAxes)
{
    public bool IsEmpty => IncludedAxes.Count == 0;
}

/// <summary>
/// Assembles what the model is told about a person. It asks
/// <see cref="IDataAccessPolicy"/> which axes it may include and is handed only
/// those - it never receives the whole log and filters afterwards, because a
/// filter that runs late is a filter that can be forgotten
/// (docs/REFOCUS.md §11).
/// </summary>
public interface IAiContextBuilder
{
    Task<AiContext> BuildAsync(Guid principalId, Guid subjectId, CancellationToken cancellationToken = default);
}
