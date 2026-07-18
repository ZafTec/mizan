using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mizan.Infrastructure.Auth.BetterAuth;

public sealed class JwksProvider : IJwksProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwksProvider> _logger;
    private readonly JwtOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private SecurityKey[] _keys = [];

    public JwksProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<JwtOptions> options,
        ILogger<JwksProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public IReadOnlyCollection<SecurityKey> GetSigningKeys() => Volatile.Read(ref _keys);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(JwksProvider));
            var json = await client.GetStringAsync(_options.JwksUrl, cancellationToken);
            var parsed = new JsonWebKeySet(json).Keys?.Cast<SecurityKey>().ToArray() ?? [];
            if (parsed.Length == 0)
            {
                throw new InvalidOperationException("JWKS endpoint returned no signing keys");
            }

            Volatile.Write(ref _keys, parsed);
        }
        catch (Exception ex) when (GetSigningKeys().Count > 0)
        {
            _logger.LogWarning(ex, "JWKS refresh failed; continuing with last-known-good keys");
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
