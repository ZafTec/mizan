using System.Text.Json.Serialization;

namespace Mizan.Contracts.Ai;

/// <summary>
/// A public JSON Web Key (RFC 7517) for the assistant's end-user token, served at
/// <c>/.well-known/jwks.json</c> (docs/AI.md#behind-a-gateway). It has no member for a private part.
/// </summary>
public sealed record AiJsonWebKey(
    [property: JsonPropertyName("kty")] string Kty,
    [property: JsonPropertyName("crv")] string Crv,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y,
    [property: JsonPropertyName("kid")] string Kid,
    [property: JsonPropertyName("alg")] string Alg,
    [property: JsonPropertyName("use")] string Use);

/// <summary>The keys a gateway verifies the end-user token with. Empty when the token is off.</summary>
public sealed record AiJsonWebKeySet([property: JsonPropertyName("keys")] IReadOnlyList<AiJsonWebKey> Keys);
