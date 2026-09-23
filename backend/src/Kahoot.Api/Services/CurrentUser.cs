using System.Security.Claims;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Api.Services;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");

            return Guid.TryParse(sub, out var parsedId) ? parsedId : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var tenantClaim = User?.FindFirstValue("tenant_id")
                ?? User?.FindFirstValue("tenantId");

            return Guid.TryParse(tenantClaim, out var parsedId) ? parsedId : UserId;
        }
    }

    public string? Role => User?.FindFirstValue(ClaimTypes.Role)
        ?? User?.FindFirstValue("role");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
