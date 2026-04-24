using Microsoft.IdentityModel.Tokens;

namespace Mizan.Infrastructure.Auth.BetterAuth;

public interface IJwksProvider
{
    Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default);
}
