namespace Kahoot.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; }

    public int RefreshTokenDays { get; set; }

    public static bool IsValid(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer)
            || string.IsNullOrWhiteSpace(options.Audience)
            || options.AccessTokenMinutes is < 1 or > 1440
            || options.RefreshTokenDays is < 1 or > 365
            || options.SigningKey is null
            || options.SigningKey.Length != 64)
        {
            return false;
        }

        return options.SigningKey.All(Uri.IsHexDigit);
    }
}
