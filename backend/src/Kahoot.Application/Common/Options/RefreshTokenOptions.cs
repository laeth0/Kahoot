namespace Kahoot.Application.Common.Options;

// Refresh Token Lifecycle Configuration - Governs individual token validity and rotation family maximum lifetime
public sealed class RefreshTokenOptions
{
    // Configuration Section Name - Key identifying refresh token settings in appsettings
    public const string SectionName = "RefreshToken";

    // Individual Token Lifetime (AUTH-EXP-001) - Number of days a single issued refresh token remains valid
    public int LifetimeDays { get; set; }

    // Family Maximum Lifetime (AUTH-ROT-001) - Absolute maximum lifetime in days for an entire rotation family chain
    public int FamilyMaxLifetimeDays { get; set; }
}
