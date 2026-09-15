using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kahoot.Domain.Games;

namespace Kahoot.Application.Common.Observability;

public static class ObservabilityNames
{
    public const string ActivitySourceName = "Kahoot.Application";
    public const string MeterName = "Kahoot.Application";
}

public sealed class KahootTelemetry : IKahootTelemetry, IDisposable
{
    private static readonly GameStatus[] AllGameStatuses = Enum.GetValues<GameStatus>();

    private static readonly HashSet<string> AllowedBroadcastEvents =
    [
        "ParticipantPresenceChanged",
        "QuestionStarted",
        "QuestionEnded",
        "LeaderboardUpdated",
        "GameEnded"
    ];

    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    private readonly Counter<long> _gameSessionsCreated;
    private readonly Counter<long> _gameSessionsEnded;
    private readonly Counter<long> _playersJoined;
    private readonly Counter<long> _questionsServed;
    private readonly Counter<long> _answersSubmitted;
    private readonly Histogram<double> _answerProcessingDuration;
    private readonly Counter<long> _signalrReconnections;
    private readonly Counter<long> _signalrDisconnects;
    private readonly Counter<long> _signalrEventsSent;
    private readonly Histogram<double> _signalrBroadcastDuration;
    private readonly Counter<long> _gameTransitionFailures;
    private readonly Histogram<double> _applicationOperationDuration;
    private readonly Counter<long> _snapshotFailures;

    private Dictionary<GameStatus, int> _activeGamesSnapshot;
    private int _connectedPlayersSnapshot;

    public KahootTelemetry()
    {
        _activitySource = new ActivitySource(ObservabilityNames.ActivitySourceName);
        _meter = new Meter(ObservabilityNames.MeterName);

        _gameSessionsCreated = _meter.CreateCounter<long>("kahoot.game.sessions.created");
        _gameSessionsEnded = _meter.CreateCounter<long>("kahoot.game.sessions.ended");
        _playersJoined = _meter.CreateCounter<long>("kahoot.players.joined");
        _questionsServed = _meter.CreateCounter<long>("kahoot.questions.served");
        _answersSubmitted = _meter.CreateCounter<long>("kahoot.answers.submitted");
        _answerProcessingDuration = _meter.CreateHistogram<double>("kahoot.answer.processing.duration", "s");
        _signalrReconnections = _meter.CreateCounter<long>("kahoot.signalr.reconnections");
        _signalrDisconnects = _meter.CreateCounter<long>("kahoot.signalr.disconnects");
        _signalrEventsSent = _meter.CreateCounter<long>("kahoot.signalr.events.sent");
        _signalrBroadcastDuration = _meter.CreateHistogram<double>("kahoot.signalr.broadcast.duration", "s");
        _gameTransitionFailures = _meter.CreateCounter<long>("kahoot.game.transition.failures");
        _applicationOperationDuration = _meter.CreateHistogram<double>("kahoot.application.operation.duration", "s");
        _snapshotFailures = _meter.CreateCounter<long>("kahoot.observability.snapshot.failures");

        var initialGames = new Dictionary<GameStatus, int>();
        foreach (var status in AllGameStatuses)
        {
            initialGames[status] = 0;
        }
        _activeGamesSnapshot = initialGames;
        _connectedPlayersSnapshot = 0;

        _meter.CreateObservableGauge("kahoot.game.sessions.active", () => ObserveActiveGames());
        _meter.CreateObservableGauge("kahoot.players.connected", () => Volatile.Read(ref _connectedPlayersSnapshot));
    }

    private IEnumerable<Measurement<int>> ObserveActiveGames()
    {
        var snapshot = Volatile.Read(ref _activeGamesSnapshot);
        foreach (var status in AllGameStatuses)
        {
            var count = snapshot.TryGetValue(status, out var val) ? val : 0;
            yield return new Measurement<int>(count, new KeyValuePair<string, object?>("state", status.ToString()));
        }
    }

    public Activity? StartOperation(string operationName)
    {
        return _activitySource.StartActivity(operationName, ActivityKind.Internal);
    }

    public void RecordOperation(string operationName, string outcome, double durationSeconds)
    {
        var validOutcome = outcome switch
        {
            "success" => "success",
            "failure" => "failure",
            "exception" => "exception",
            _ => throw new ArgumentException($"Invalid operation outcome '{outcome}'.", nameof(outcome))
        };

        _applicationOperationDuration.Record(
            durationSeconds,
            new KeyValuePair<string, object?>("operation", operationName),
            new KeyValuePair<string, object?>("outcome", validOutcome));
    }

    public void RecordGameCreated()
    {
        _gameSessionsCreated.Add(1);
    }

    public void RecordGameEnded()
    {
        _gameSessionsEnded.Add(1);
    }

    public void RecordPlayerJoined()
    {
        _playersJoined.Add(1);
    }

    public void RecordQuestionServed(string operation)
    {
        var validOp = operation switch
        {
            "start" => "start",
            "advance" => "advance",
            _ => throw new ArgumentException($"Invalid question operation '{operation}'.", nameof(operation))
        };

        _questionsServed.Add(1, new KeyValuePair<string, object?>("operation", validOp));
    }

    public void RecordAnswer(string outcome, double durationSeconds)
    {
        var validOutcome = outcome switch
        {
            "accepted" => "accepted",
            "duplicate" => "duplicate",
            "late" => "late",
            "rejected" => "rejected",
            "exception" => "exception",
            _ => throw new ArgumentException($"Invalid answer outcome '{outcome}'.", nameof(outcome))
        };

        _answerProcessingDuration.Record(durationSeconds, new KeyValuePair<string, object?>("outcome", validOutcome));
        _answersSubmitted.Add(1, new KeyValuePair<string, object?>("outcome", validOutcome));
    }

    public void RecordReconnect(string outcome)
    {
        var validOutcome = outcome switch
        {
            "success" => "success",
            "failure" => "failure",
            _ => throw new ArgumentException($"Invalid reconnect outcome '{outcome}'.", nameof(outcome))
        };

        _signalrReconnections.Add(1, new KeyValuePair<string, object?>("outcome", validOutcome));
    }

    public void RecordDisconnect(string outcome)
    {
        var validOutcome = outcome switch
        {
            "normal" => "normal",
            "error" => "error",
            _ => throw new ArgumentException($"Invalid disconnect outcome '{outcome}'.", nameof(outcome))
        };

        _signalrDisconnects.Add(1, new KeyValuePair<string, object?>("outcome", validOutcome));
    }

    public void RecordBroadcast(string eventName, string outcome, double durationSeconds)
    {
        if (!AllowedBroadcastEvents.Contains(eventName))
        {
            throw new ArgumentException($"Invalid broadcast event name '{eventName}'.", nameof(eventName));
        }

        var validOutcome = outcome switch
        {
            "success" => "success",
            "failure" => "failure",
            _ => throw new ArgumentException($"Invalid broadcast outcome '{outcome}'.", nameof(outcome))
        };

        _signalrBroadcastDuration.Record(
            durationSeconds,
            new KeyValuePair<string, object?>("event", eventName),
            new KeyValuePair<string, object?>("outcome", validOutcome));

        _signalrEventsSent.Add(
            1,
            new KeyValuePair<string, object?>("event", eventName),
            new KeyValuePair<string, object?>("outcome", validOutcome));
    }

    public void RecordTransitionFailure(string transition, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(transition))
        {
            throw new ArgumentException("Transition name cannot be empty.", nameof(transition));
        }

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("Error code cannot be empty.", nameof(errorCode));
        }

        _gameTransitionFailures.Add(
            1,
            new KeyValuePair<string, object?>("transition", transition),
            new KeyValuePair<string, object?>("error.code", errorCode));
    }

    public void UpdateBusinessSnapshot(IReadOnlyDictionary<GameStatus, int> activeGamesByStatus, int connectedPlayers)
    {
        var newSnapshot = new Dictionary<GameStatus, int>();
        foreach (var status in AllGameStatuses)
        {
            newSnapshot[status] = activeGamesByStatus.TryGetValue(status, out var count) ? count : 0;
        }

        Volatile.Write(ref _activeGamesSnapshot, newSnapshot);
        Volatile.Write(ref _connectedPlayersSnapshot, connectedPlayers);
    }

    public void RecordSnapshotFailure()
    {
        _snapshotFailures.Add(1);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
