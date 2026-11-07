using MediatR;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Ai;

/// <summary>
/// Proposals for the rest of today against what is left of the person's targets. It spends the
/// AI allowance and writes a usage row, so every surface sends it as a write.
/// </summary>
public sealed record SuggestMealsCommand : IRequest<MealSuggestionResult>;

public sealed class SuggestMealsCommandHandler : IRequestHandler<SuggestMealsCommand, MealSuggestionResult>
{
    private readonly INutritionAiService _ai;
    private readonly ICurrentUserService _currentUser;

    public SuggestMealsCommandHandler(INutritionAiService ai, ICurrentUserService currentUser)
    {
        _ai = ai;
        _currentUser = currentUser;
    }

    public Task<MealSuggestionResult> Handle(SuggestMealsCommand request, CancellationToken cancellationToken) =>
        _ai.SuggestMealsAsync(_currentUser.UserId ?? throw new UnauthorizedAccessException(), cancellationToken);
}
