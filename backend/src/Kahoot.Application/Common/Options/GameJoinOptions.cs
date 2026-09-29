namespace Kahoot.Application.Common.Options;

public sealed class GameJoinOptions
{
    // Configuration Section Name - Key identifying player join settings in appsettings
    public const string SectionName = "GameJoin";

    // Client Web URL - Base origin used for constructing player join links and QR codes
    public string ClientBaseUrl { get; set; } = string.Empty;

    // Secure Origin Boundary Validation (JOIN-SEC-001) - Enforces HTTPS (or loopback HTTP), root path, and rejects query/fragment injections
    public static bool HasValidOrigin(GameJoinOptions options)
    {
        if (options.ClientBaseUrl != options.ClientBaseUrl.Trim() ||
            options.ClientBaseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(options.ClientBaseUrl, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttps ||
                (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) &&
               !string.IsNullOrEmpty(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               uri.AbsolutePath == "/" &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }
}
