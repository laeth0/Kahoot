namespace Kahoot.Infrastructure.Security;

// JWT Subsystem Configuration - Declares issuer, audience, symmetric signing key, and access token lifetime boundaries.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // JWT Issuer Authority - Identifies the principal security authority issuing bearer tokens.
    public string Issuer { get; set; } = string.Empty;

    // JWT Audience Boundary - Identifies the target resource servers authorized to accept the token.
    public string Audience { get; set; } = string.Empty;

    // Symmetric Signing Key - Base64-encoded secret key used to compute and verify HMAC-SHA256 signatures.
    public string SigningKey { get; set; } = string.Empty;

    // Access Token Lifetime - Bounded 15-minute validity window minimizing exposure of bearer tokens upon interception.
    public int AccessTokenMinutes { get; set; } = 15;
}
