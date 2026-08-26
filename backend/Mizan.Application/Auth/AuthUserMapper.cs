using Mizan.Domain.Entities;

namespace Mizan.Application.Auth;

public static class AuthUserMapper
{
    public static AuthUserDto ToDto(User user) => new(
        user.Id,
        user.Email,
        user.Name,
        user.Image,
        user.Role,
        user.EmailVerified,
        user.ThemePreference,
        user.CompactMode,
        user.ReduceAnimations,
        user.PasswordHash is not null,
        user.TimeZoneId);
}
