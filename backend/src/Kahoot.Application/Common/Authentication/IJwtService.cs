using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Common.Authentication;

public interface IJwtService
{
    /// <summary>Issues tokens after the caller has authenticated the user.</summary>
    Task<Result<TokenPair>> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Rotates an active refresh token; each token can be used once.</summary>
    Task<Result<TokenPair>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes an active refresh token and reports whether it was found.</summary>
    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}
