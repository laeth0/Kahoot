namespace Kahoot.Application.Common.Interfaces;

public interface IGameAutoCloseService
{
    Task<bool> TryAutoCloseQuestionAsync(Guid gameId, CancellationToken cancellationToken);
}
