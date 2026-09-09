using Kahoot.Api.Common;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.JoinGame;
using Kahoot.Application.Games.Presence;
using Kahoot.Application.Games.Reconnect;
using Kahoot.Application.Games.SubmitAnswer;
using Kahoot.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kahoot.Api.Realtime;

public sealed class GameHub(
    ISender sender,
    GameNotifier notifier,
    TimeProvider timeProvider) : Hub<IGameClient>
{
    private const string GameIdItem = "gameId";
    private const string ParticipantIdItem = "participantId";
    private const string SessionTokenItem = "sessionToken";
    private const string SubmitWindowItem = "submitWindow";

    private static readonly TimeSpan SubmitWindow = TimeSpan.FromSeconds(3);
    private const int MaxSubmitsPerWindow = 5;

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

        if (!AllowSubmit())
        {
            return RealtimeResponse<AnswerAckResponse>.Failure(GameErrors.TooManyAnswerAttempts);
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

        Result<bool> ownership = await sender.Send(
            new AuthorizeHostGameQuery(gameId, hostId), Context.ConnectionAborted);

        if (ownership.IsFailure || !ownership.Value)
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
            await sender.Send(new DetachParticipantConnectionCommand(participantId, Context.ConnectionId));
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
        await sender.Send(
            new AttachParticipantConnectionCommand(participantId, Context.ConnectionId),
            Context.ConnectionAborted);
    }

    private bool AllowSubmit()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (Context.Items[SubmitWindowItem] is not SubmitRateWindow window)
        {
            Context.Items[SubmitWindowItem] = new SubmitRateWindow(now, 1);
            return true;
        }

        if (now - window.StartedAt > SubmitWindow)
        {
            window.StartedAt = now;
            window.Count = 1;
            return true;
        }

        if (window.Count >= MaxSubmitsPerWindow)
        {
            return false;
        }

        window.Count++;
        return true;
    }

    private sealed class SubmitRateWindow(DateTimeOffset startedAt, int count)
    {
        public DateTimeOffset StartedAt { get; set; } = startedAt;

        public int Count { get; set; } = count;
    }
}
