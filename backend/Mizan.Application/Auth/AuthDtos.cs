namespace Mizan.Application.Auth;

/// <summary>What every authenticated surface needs to know about the caller.</summary>
public record AuthUserDto(
    Guid Id,
    string Email,
    string? Name,
    string? Image,
    string Role,
    bool EmailVerified,
    string ThemePreference,
    bool CompactMode,
    bool ReduceAnimations,
    bool HasPassword,
    /// <summary>
    /// Null until the user has told us. Every screen that shows a day boundary
    /// needs it, so it rides the session rather than costing a request.
    /// </summary>
    string? TimeZoneId);

public record SessionSummaryDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);
