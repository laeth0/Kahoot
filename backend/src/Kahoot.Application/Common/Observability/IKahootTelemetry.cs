using System.Diagnostics;
using Kahoot.Domain.Games;

namespace Kahoot.Application.Common.Observability;

public interface IKahootTelemetry
{
    Activity? StartOperation(string operationName);
    void RecordOperation(string operationName, string outcome, double durationSeconds);
    void RecordGameCreated();
    void RecordGameEnded();
    void RecordPlayerJoined();
    void RecordQuestionServed(string operation);
    void RecordAnswer(string outcome, double durationSeconds);
    void RecordReconnect(string outcome);
    void RecordDisconnect(string outcome);
    void RecordBroadcast(string eventName, string outcome, double durationSeconds);
    void RecordTransitionFailure(string transition, string errorCode);
    void UpdateBusinessSnapshot(IReadOnlyDictionary<GameStatus, int> activeGamesByStatus, int connectedPlayers);
    void RecordSnapshotFailure();
}
