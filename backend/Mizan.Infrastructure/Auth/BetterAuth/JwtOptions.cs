namespace Mizan.Infrastructure.Auth.BetterAuth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public string JwksUrl { get; init; } = string.Empty;
    public int JwksCacheMinutes { get; init; } = 10;
}
