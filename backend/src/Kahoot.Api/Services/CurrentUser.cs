using System.Security.Claims;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Api.Services;

// Current User Context - Resolves authenticated user identity and claims from ambient HTTP request context.
internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Claims Principal Accessor - Retrieves ClaimsPrincipal attached to current HTTP request thread context.
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    // User Identifier Resolution - Parses JWT subject claim ("sub" or NameIdentifier) into strongly-typed Guid.
    public Guid? UserId
    {
        get
        {
            string? sub = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");

            return Guid.TryParse(sub, out Guid parsedId) ? parsedId : null;
        }
    }

    // User Role Claim - Reads role claim to support domain and application layer authorization checks.
    public string? Role => User?.FindFirstValue(ClaimTypes.Role)
        ?? User?.FindFirstValue("role");

    // Authentication State - Indicates whether current HTTP request carries an active, authenticated identity.
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
