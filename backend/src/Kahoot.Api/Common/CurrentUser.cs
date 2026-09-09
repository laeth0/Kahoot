using Kahoot.Application.Common.Security;

namespace Kahoot.Api.Common;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? HostId => HostClaims.GetHostId(httpContextAccessor.HttpContext?.User);

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
