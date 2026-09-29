namespace Kahoot.Application.Common.Interfaces;

public interface ICurrentUser
{
    // Caller Identifier - User ID extracted from verified JWT claims; null for unauthenticated requests
    Guid? UserId { get; }

    // Caller Role - Role claim (Host, SystemAdmin) extracted from verified JWT
    string? Role { get; }

    // Authentication Status - True if the ambient request carries a valid authenticated identity
    bool IsAuthenticated { get; }
}
