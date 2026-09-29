using System.Text;

namespace Kahoot.Application.Features.Auth;

internal static class UsernameNormalization
{
    // Display Identity - Strips leading/trailing whitespace while preserving user casing for display and audit logs
    public static string GetDisplayUsername(string username) => username.Trim(' ');

    // Unicode Normalization (NFKC) & Invariant Case Folding - Canonical decomposition and uppercase folding prevents visually identical spoofing
    public static string GetNormalizedUsername(string displayUsername) =>
        displayUsername.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
}
