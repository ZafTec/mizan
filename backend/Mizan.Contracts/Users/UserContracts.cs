namespace Mizan.Contracts.Users;

/// <summary>
/// What <c>GET /api/Users/me</c> answers: the profile, the active goal's
/// targets, the streak, and the preferences the web, MCP and the bot read.
/// </summary>
/// <param name="NotifyByTelegram">Whether notifications also go to the linked Telegram chat.</param>
public record UserDto(
    Guid Id,
    string Email,
    string? Name,
    string? Image,
    DateTime CreatedAt,
    UserGoalSummaryDto? CurrentGoal,
    int StreakCount,
    string? PriceRegionCode = null,
    bool NotifyByTelegram = false
);

public record UserGoalSummaryDto(
    decimal? TargetCalories,
    decimal? TargetProteinGrams,
    decimal? TargetCarbsGrams,
    decimal? TargetFatGrams
);

/// <summary>
/// Body of <c>PUT /api/Users/me</c>. Null leaves a field as it is, so a name
/// change cannot reset a theme.
/// </summary>
/// <param name="NotifyByTelegram">
/// True sends notifications to the linked Telegram chat as well; the API
/// refuses it with 400 when no chat is linked. False always works.
/// </param>
public record UpdateUserRequest(
    string? Name,
    string? Image,
    string? ThemePreference = null,
    bool? CompactMode = null,
    bool? ReduceAnimations = null,
    string? PriceRegionCode = null,
    bool? NotifyByTelegram = null);
