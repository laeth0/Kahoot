namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Features.Games.Models;

public sealed record GameNotificationRecord(
    string EventName,
    Guid HostAccountId,
    Guid GameId,
    long Version,
    object? PrimaryPayload,
    object? SecondaryPayload,
    CancellationToken CancellationToken);

public sealed class RecordingGameNotificationService : IGameNotificationService
{
    private readonly ConcurrentBag<GameNotificationRecord> _records = new();

    public IReadOnlyList<GameNotificationRecord> Records => _records.ToArray();

    public Func<GameNotificationRecord, Task>? Callback { get; set; }

    public async Task PublishQuestionStartedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object playerPayload,
        object hostPayload,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "QuestionStarted",
            hostAccountId,
            gameId,
            stateVersion,
            playerPayload,
            hostPayload,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishQuestionEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "QuestionEnded",
            hostAccountId,
            gameId,
            stateVersion,
            payload,
            null,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishQuestionEndedWithPersonalResultsAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object aggregatePayload,
        IReadOnlyList<PersonalQuestionResultEvent> personalResults,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "QuestionEndedWithPersonalResults",
            hostAccountId,
            gameId,
            stateVersion,
            aggregatePayload,
            personalResults,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishLeaderboardUpdatedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "LeaderboardUpdated",
            hostAccountId,
            gameId,
            stateVersion,
            payload,
            null,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishLeaderboardUpdatedWithPersonalRanksAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object aggregatePayload,
        IReadOnlyList<PersonalLeaderboardEvent> personalRanks,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "LeaderboardUpdatedWithPersonalRanks",
            hostAccountId,
            gameId,
            stateVersion,
            aggregatePayload,
            personalRanks,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishGameEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "GameEnded",
            hostAccountId,
            gameId,
            stateVersion,
            payload,
            null,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishGameEndedWithPersonalRanksAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object aggregatePayload,
        IReadOnlyList<PersonalGameEndedEvent> personalRanks,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "GameEndedWithPersonalRanks",
            hostAccountId,
            gameId,
            stateVersion,
            aggregatePayload,
            personalRanks,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }

    public async Task PublishParticipantPresenceChangedAsync(
        Guid hostAccountId,
        Guid gameId,
        long presenceVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        GameNotificationRecord record = new(
            "ParticipantPresenceChanged",
            hostAccountId,
            gameId,
            presenceVersion,
            payload,
            null,
            cancellationToken);

        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }
}
