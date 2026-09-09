using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.CreateGame;

internal sealed class CreateGameCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IGamePinGenerator pinGenerator,
    IDbExceptionInterpreter dbExceptionInterpreter) : ICommandHandler<CreateGameCommand, CreateGameResponse>
{
    private const int MaxPinAttempts = 5;

    public async Task<Result<CreateGameResponse>> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<CreateGameResponse>(SharedErrors.Unauthorized);
        }

        bool? isPublished = await dbContext.Quizzes
            .Where(quiz => quiz.Id == command.QuizId && quiz.HostId == hostId)
            .Select(quiz => (bool?)quiz.IsPublished)
            .FirstOrDefaultAsync(cancellationToken);

        if (isPublished is null)
        {
            return Result.Failure<CreateGameResponse>(QuizErrors.NotFound);
        }

        if (isPublished is false)
        {
            return Result.Failure<CreateGameResponse>(GameErrors.QuizNotPublished);
        }

        for (int attempt = 0; attempt < MaxPinAttempts; attempt++)
        {
            string pin = await pinGenerator.GenerateUniquePinAsync(cancellationToken);

            GameSession game = new()
            {
                QuizId = command.QuizId,
                HostId = hostId,
                Pin = pin,
                Status = GameStatus.Lobby
            };
            dbContext.GameSessions.Add(game);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return Result.Success(new CreateGameResponse(game.Id, game.Pin, game.Status));
            }
            catch (DbUpdateException exception)
                when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_game_session_active_pin"))
            {
                dbContext.GameSessions.Remove(game);
            }
        }

        return Result.Failure<CreateGameResponse>(GameErrors.PinUnavailable);
    }
}
