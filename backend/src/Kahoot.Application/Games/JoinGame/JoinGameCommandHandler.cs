using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.JoinGame;

internal sealed class JoinGameCommandHandler(
    IApplicationDbContext dbContext,
    ISecureTokenGenerator secureTokenGenerator,
    ITokenHasher tokenHasher,
    IDbExceptionInterpreter dbExceptionInterpreter,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry) : ICommandHandler<JoinGameCommand, JoinGameResponse>
{
    public async Task<Result<JoinGameResponse>> Handle(JoinGameCommand command, CancellationToken cancellationToken)
    {
        string pin = command.Pin.Trim();
        string nickname = command.Nickname.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        GameSession? game = await dbContext.GameSessions
            .FromSqlInterpolated($"SELECT * FROM game_sessions WHERE pin = {pin} AND status != 'Finished' FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.InvalidPin);
        }

        if (game.Status != GameStatus.Lobby)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.NotJoinable);
        }


        string sessionToken = secureTokenGenerator.GenerateToken();

        Participant participant = new()
        {
            GameSessionId = game.Id,
            Nickname = nickname,
            NicknameNormalized = nickname.ToLowerInvariant(),
            SessionTokenHash = tokenHasher.Hash(sessionToken),
            LastSeenAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Participants.Add(participant);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("participant.id", participant.Id);
            telemetry.RecordPlayerJoined();
        }
        catch (DbUpdateException exception)
            when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_participant_game_nickname"))
        {
            return Result.Failure<JoinGameResponse>(GameErrors.NicknameTaken);
        }

        return Result.Success(new JoinGameResponse(
            game.Id,
            participant.Id,
            sessionToken,
            participant.Nickname,
            game.Status));
    }
}
