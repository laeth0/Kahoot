namespace Kahoot.Application.Common.Options;

public sealed class GameJoinOptions
{
    public const string SectionName = "GameJoin";

    public string ClientBaseUrl { get; set; } = string.Empty;

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
