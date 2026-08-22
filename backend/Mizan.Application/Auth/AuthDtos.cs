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
    bool HasPassword);

public record SessionSummaryDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);
