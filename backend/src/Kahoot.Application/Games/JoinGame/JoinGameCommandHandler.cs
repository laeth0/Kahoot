using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
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
    TimeProvider timeProvider) : ICommandHandler<JoinGameCommand, JoinGameResponse>
{
    public async Task<Result<JoinGameResponse>> Handle(JoinGameCommand command, CancellationToken cancellationToken)
    {
        string pin = command.Pin.Trim();
        string nickname = command.Nickname.Trim();

        var game = await dbContext.GameSessions
            .Where(session => session.Pin == pin && session.Status != GameStatus.Finished)
            .Select(session => new { session.Id, session.Status })
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
