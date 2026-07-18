using Microsoft.IdentityModel.Tokens;

namespace Mizan.Infrastructure.Auth.BetterAuth;

public interface IJwksProvider
{
    IReadOnlyCollection<SecurityKey> GetSigningKeys();
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
