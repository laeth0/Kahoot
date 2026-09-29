namespace Kahoot.Api.Options;

// CORS Configuration Options - Binds allowed frontend origin URLs for browser cross-origin policy enforcement.
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    // Allowed Origins Allowlist - Explicit list of origins permitted to issue authenticated cross-origin requests.
    public string[] AllowedOrigins { get; set; } = [];
}
