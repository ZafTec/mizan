using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mizan.Infrastructure.Auth.BetterAuth;

public static class JwtAuthenticationExtensions
{
    public const string BetterAuthHttpClientName = nameof(JwksProvider);

    // Registers everything needed to validate BetterAuth-issued EdDSA JWTs:
    // options binding, HttpClient, singleton JwksProvider, and JwtBearer setup.
    // SignatureValidator wiring is done through an IPostConfigureOptions so the
    // provider is resolved from DI rather than a static accessor.
    public static AuthenticationBuilder AddBetterAuthJwtBearer(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        builder.Services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddHttpClient(BetterAuthHttpClientName);
        builder.Services.AddSingleton<IJwksProvider, JwksProvider>();
        builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        return builder.AddJwtBearer(options =>
        {
            options.MapInboundClaims = true;
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer),
                ValidIssuer = jwt.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwt.Audience),
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = false,
                RequireSignedTokens = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role,
                ValidAlgorithms = new[] { "EdDSA" }
            };
            options.TokenHandlers.Clear();
            options.TokenHandlers.Add(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler());
        });
    }

    // Resolved at options-build time, after DI is ready. Sets the
    // SignatureValidator closure with the singleton JwksProvider so we never
    // need a static accessor or a duplicated root provider.
    private sealed class JwtBearerOptionsSetup : IPostConfigureOptions<JwtBearerOptions>
    {
        private readonly IJwksProvider _jwksProvider;

        public JwtBearerOptionsSetup(IJwksProvider jwksProvider)
        {
            _jwksProvider = jwksProvider;
        }

        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme) return;
            options.TokenValidationParameters.SignatureValidator = (token, parameters) =>
                EdDsaJwtSignatureValidator.Validate(token, parameters, _jwksProvider);
        }
    }
}
