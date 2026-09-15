namespace Kahoot.Api.Common;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public bool SecureCookies { get; set; } = true;
}
