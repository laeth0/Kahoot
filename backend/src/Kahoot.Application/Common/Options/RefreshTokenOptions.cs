namespace Kahoot.Application.Common.Options;

/// <summary>
/// Runtime configuration for refresh-token lifecycle.
/// Owned by Application because Login and future Refresh use cases
/// both need these values to set DB expiry and family limits.
/// The API layer reads the same options to keep cookie MaxAge in sync.
/// </summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>Number of days a single refresh token remains valid.</summary>
    public int LifetimeDays { get; set; }

    /// <summary>
    /// Maximum number of days a token family (rotation chain) may live
    /// before all tokens in the family are considered expired.
    /// Must be >= LifetimeDays.
    /// </summary>
    public int FamilyMaxLifetimeDays { get; set; }
}
