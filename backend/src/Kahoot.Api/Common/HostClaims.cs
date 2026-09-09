using System.Security.Claims;

namespace Kahoot.Api.Common;

internal static class HostClaims
{
    public static Guid? GetHostId(ClaimsPrincipal? principal)
    {
        string? subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal?.FindFirstValue("sub");

        return Guid.TryParse(subject, out Guid hostId) ? hostId : null;
    }
}
