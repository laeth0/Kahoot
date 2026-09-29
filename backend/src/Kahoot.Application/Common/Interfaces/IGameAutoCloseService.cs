namespace Kahoot.Application.Common.Interfaces;

public interface IGameAutoCloseService
{
    // Dynamic Question Auto-Close (GAME-AUTO-002) - Checks if all remaining eligible participants submitted answers; triggers early transition if complete
    Task<bool> TryAutoCloseQuestionAsync(Guid gameId, CancellationToken cancellationToken);
}
