using Kahoot.Api.Common;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.JoinGame;
using Kahoot.Application.Games.Reconnect;
using Kahoot.Application.Games.SubmitAnswer;
using Kahoot.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Api.Realtime;

public sealed class GameHub(
    ISender sender,
    IApplicationDbContext dbContext,
    GameNotifier notifier) : Hub<IGameClient>
{
    private const string GameIdItem = "gameId";
    private const string ParticipantIdItem = "participantId";
    private const string SessionTokenItem = "sessionToken";

    public async Task<RealtimeResponse<JoinGameResponse>> JoinGame(string pin, string nickname)
    {
        Result<JoinGameResponse> result = await sender.Send(
            new JoinGameCommand(pin, nickname), Context.ConnectionAborted);

        if (result.IsFailure)
        {
            return RealtimeResponse<JoinGameResponse>.Failure(result.Error);
        }

        JoinGameResponse value = result.Value;
        await TrackPlayerConnectionAsync(value.GameId, value.ParticipantId, value.SessionToken);
        await notifier.ParticipantJoinedAsync(
            value.GameId,
            new GameParticipantResponse(value.ParticipantId, value.Nickname, 0, null, true, false));

        return RealtimeResponse<JoinGameResponse>.Ok(value);
    }

    public async Task<RealtimeResponse<PlayerGameStateResponse>> Reconnect(string sessionToken)
    {
        Result<PlayerGameStateResponse> result = await sender.Send(
            new ReconnectParticipantCommand(sessionToken), Context.ConnectionAborted);

        if (result.IsFailure)
        {
            return RealtimeResponse<PlayerGameStateResponse>.Failure(result.Error);
        }

        PlayerGameStateResponse value = result.Value;
        await TrackPlayerConnectionAsync(value.GameId, value.ParticipantId, sessionToken);

        return RealtimeResponse<PlayerGameStateResponse>.Ok(value);
    }

    public async Task<RealtimeResponse<AnswerAckResponse>> SubmitAnswer(Guid questionId, Guid selectedChoiceId)
    {
        if (Context.Items[GameIdItem] is not Guid gameId || Context.Items[SessionTokenItem] is not string sessionToken)
        {
            return RealtimeResponse<AnswerAckResponse>.Failure(GameErrors.InvalidSessionToken);
        }

        Result<AnswerAckResponse> result = await sender.Send(
            new SubmitAnswerCommand(gameId, questionId, sessionToken, selectedChoiceId),
            Context.ConnectionAborted);

        return result.IsSuccess
            ? RealtimeResponse<AnswerAckResponse>.Ok(result.Value)
            : RealtimeResponse<AnswerAckResponse>.Failure(result.Error);
    }

    [Authorize]
    public async Task<RealtimeResponse<bool>> JoinAsHost(Guid gameId)
    {
        if (HostClaims.GetHostId(Context.User) is not { } hostId)
        {
            return RealtimeResponse<bool>.Failure(SharedErrors.Unauthorized);
        }

        bool ownsGame = await dbContext.GameSessions.AnyAsync(
            session => session.Id == gameId && session.HostId == hostId,
            Context.ConnectionAborted);

        if (!ownsGame)
        {
            return RealtimeResponse<bool>.Failure(GameErrors.NotFound);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroups.Host(gameId), Context.ConnectionAborted);

        return RealtimeResponse<bool>.Ok(true);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[ParticipantIdItem] is Guid participantId && Context.Items[GameIdItem] is Guid gameId)
        {
            await dbContext.Participants
                .Where(participant => participant.Id == participantId && participant.ConnectionId == Context.ConnectionId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(participant => participant.ConnectionId, (string?)null),
                    CancellationToken.None);

            await notifier.ParticipantLeftAsync(gameId, participantId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task TrackPlayerConnectionAsync(Guid gameId, Guid participantId, string sessionToken)
    {
        Context.Items[GameIdItem] = gameId;
        Context.Items[ParticipantIdItem] = participantId;
        Context.Items[SessionTokenItem] = sessionToken;

        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroups.Players(gameId), Context.ConnectionAborted);

        await dbContext.Participants
            .Where(participant => participant.Id == participantId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(participant => participant.ConnectionId, Context.ConnectionId),
                Context.ConnectionAborted);
    }
}
