using System.Text;

namespace Kahoot.Application.Features.Auth;

internal static class UsernameNormalization
{
    public static string GetDisplayUsername(string username) => username.Trim(' ');

    public static string GetNormalizedUsername(string displayUsername) =>
        displayUsername.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
}
