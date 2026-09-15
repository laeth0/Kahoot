using Kahoot.Api.Common;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.EndGame;
using Kahoot.Application.Games.EndQuestion;
using Kahoot.Application.Games.JoinGame;
using Kahoot.Application.Games.Presence;
using Kahoot.Application.Games.Reconnect;
using Kahoot.Application.Games.SubmitAnswer;
using System.Text.Json;
using Kahoot.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kahoot.Api.Realtime;

public sealed class GameHub(
    ISender sender,
    GameNotifier notifier,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry,
    IHostPresenceTracker hostPresenceTracker,
    IServiceScopeFactory scopeFactory,
    ILogger<GameHub> logger) : Hub<IGameClient>
{
    private const string GameIdItem = "gameId";
    private const string ParticipantIdItem = "participantId";
    private const string SessionTokenItem = "sessionToken";
    private const string SubmitWindowItem = "submitWindow";
    private const string IsHostItem = "isHost";
    private const string HostGameIdItem = "hostGameId";

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
        Result<ParticipantPresenceMutationResponse> trackResult = await TrackPlayerConnectionAsync(
            value.GameId,
            value.ParticipantId,
            value.SessionToken,
            ParticipantPresenceReasons.Joined);
        if (trackResult.IsFailure)
        {
            return RealtimeResponse<JoinGameResponse>.Failure(trackResult.Error);
        }

        if (trackResult.Value.Changed)
        {
            await notifier.ParticipantPresenceChangedAsync(value.GameId, trackResult.Value.Presence);
        }

        return RealtimeResponse<JoinGameResponse>.Ok(value);
    }

    public async Task<RealtimeResponse<PlayerGameStateResponse>> Reconnect(string sessionToken)
    {
        string outcome = "failure";

        try
        {
            Result<PlayerGameStateResponse> result = await sender.Send(
                new ReconnectParticipantCommand(sessionToken), Context.ConnectionAborted);

            if (result.IsFailure)
            {
                return RealtimeResponse<PlayerGameStateResponse>.Failure(result.Error);
            }

            PlayerGameStateResponse value = result.Value;
            Result<ParticipantPresenceMutationResponse> trackResult = await TrackPlayerConnectionAsync(
                value.GameId,
                value.ParticipantId,
                sessionToken,
                ParticipantPresenceReasons.Reconnected);
            if (trackResult.IsFailure)
            {
                return RealtimeResponse<PlayerGameStateResponse>.Failure(trackResult.Error);
            }

            ParticipantPresenceMutationResponse mutation = trackResult.Value;
            value = value with
            {
                ParticipantCount = mutation.Presence.ParticipantCount,
                PresenceVersion = mutation.Presence.PresenceVersion
            };

            if (mutation.Changed)
            {
                await notifier.ParticipantPresenceChangedAsync(value.GameId, mutation.Presence);
            }

            outcome = "success";
            return RealtimeResponse<PlayerGameStateResponse>.Ok(value);
        }
        finally
        {
            telemetry.RecordReconnect(outcome);
        }
    }

    public async Task<RealtimeResponse<AnswerAckResponse>> SubmitAnswer(Guid questionId, JsonElement selectedChoices)
    {
        if (Context.Items[GameIdItem] is not Guid gameId || Context.Items[SessionTokenItem] is not string sessionToken)
        {
            return RealtimeResponse<AnswerAckResponse>.Failure(GameErrors.InvalidSessionToken);
        }

        if (!AllowSubmit())
        {
            return RealtimeResponse<AnswerAckResponse>.Failure(GameErrors.TooManyAnswerAttempts);
        }

        List<Guid> choiceIds = [];
        if (selectedChoices.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in selectedChoices.EnumerateArray())
            {
                if (element.TryGetGuid(out Guid id))
                {
                    choiceIds.Add(id);
                }
                else if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out Guid parsedId))
                {
                    choiceIds.Add(parsedId);
                }
            }
        }
        else if (selectedChoices.ValueKind == JsonValueKind.String)
        {
            if (selectedChoices.TryGetGuid(out Guid id))
            {
                choiceIds.Add(id);
            }
            else if (Guid.TryParse(selectedChoices.GetString(), out Guid parsedId))
            {
                choiceIds.Add(parsedId);
            }
        }

        if (choiceIds.Count == 0)
        {
            return RealtimeResponse<AnswerAckResponse>.Failure(GameErrors.ChoiceNotInQuestion);
        }

        Result<AnswerAckResponse> result = await sender.Send(
            new SubmitAnswerCommand(gameId, questionId, sessionToken, choiceIds),
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            return RealtimeResponse<AnswerAckResponse>.Failure(result.Error);
        }

        if (result.Value.Accepted && !result.Value.AlreadyAnswered)
        {
            Result<QuestionResultsResponse?> autoEndResult = await sender.Send(
                new TryAutoEndQuestionCommand(gameId, questionId),
                Context.ConnectionAborted);

            if (autoEndResult.IsSuccess && autoEndResult.Value is not null)
            {
                await notifier.QuestionEndedAsync(gameId, autoEndResult.Value);
            }
        }

        return RealtimeResponse<AnswerAckResponse>.Ok(result.Value);
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

        Context.Items[IsHostItem] = true;
        Context.Items[HostGameIdItem] = gameId;

        hostPresenceTracker.HostConnected(gameId, Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroups.Host(gameId), Context.ConnectionAborted);

        return RealtimeResponse<bool>.Ok(true);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (Context.Items[IsHostItem] is true && Context.Items[HostGameIdItem] is Guid hostGameId)
            {
                hostPresenceTracker.HostDisconnected(hostGameId, Context.ConnectionId, async id =>
                {
                    await AutoEndGameAsync(id);
                });
            }

            if (Context.Items[ParticipantIdItem] is Guid participantId && Context.Items[GameIdItem] is Guid gameId)
            {
                Result<ParticipantPresenceMutationResponse?> detachResult = await sender.Send(
                    new DetachParticipantConnectionCommand(gameId, participantId, Context.ConnectionId));
                if (detachResult.IsSuccess && detachResult.Value is { } mutation)
                {
                    await notifier.ParticipantPresenceChangedAsync(gameId, mutation.Presence);

                    Result<QuestionResultsResponse?> autoEndResult = await sender.Send(
                        new TryAutoEndQuestionCommand(gameId, null),
                        CancellationToken.None);

                    if (autoEndResult.IsSuccess && autoEndResult.Value is not null)
                    {
                        await notifier.QuestionEndedAsync(gameId, autoEndResult.Value);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        finally
        {
            telemetry.RecordDisconnect(exception is null ? "normal" : "error");
        }
    }

    private async Task AutoEndGameAsync(Guid gameId)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ISender scopeSender = scope.ServiceProvider.GetRequiredService<ISender>();
            IHostPresenceTracker scopeTracker = scope.ServiceProvider.GetRequiredService<IHostPresenceTracker>();

            Result<LeaderboardResponse?> result = await scopeSender.Send(new AutoEndGameCommand(gameId));
            scopeTracker.RemoveGame(gameId);

            if (result.IsSuccess && result.Value is not null)
            {
                await notifier.GameEndedAsync(gameId, result.Value);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to auto-end game {GameId} after host disconnect", gameId);
        }
    }

    private async Task<Result<ParticipantPresenceMutationResponse>> TrackPlayerConnectionAsync(
        Guid gameId,
        Guid participantId,
        string sessionToken,
        string reason)
    {
        Result<ParticipantPresenceMutationResponse> attachResult = await sender.Send(
            new AttachParticipantConnectionCommand(gameId, participantId, Context.ConnectionId, reason),
            Context.ConnectionAborted);

        if (attachResult.IsFailure)
        {
            return Result.Failure<ParticipantPresenceMutationResponse>(attachResult.Error);
        }

        Context.Items[GameIdItem] = gameId;
        Context.Items[ParticipantIdItem] = participantId;
        Context.Items[SessionTokenItem] = sessionToken;

        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroups.Players(gameId), Context.ConnectionAborted);
        return attachResult;
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
