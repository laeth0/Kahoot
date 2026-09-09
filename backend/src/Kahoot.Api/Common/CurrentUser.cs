using System.Security.Claims;
using Kahoot.Application.Common.Security;

namespace Kahoot.Api.Common;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? HostId
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            string? subject = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
            return Guid.TryParse(subject, out Guid hostId) ? hostId : null;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
