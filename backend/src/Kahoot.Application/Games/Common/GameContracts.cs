using Kahoot.Domain.Games;

namespace Kahoot.Application.Games.Common;

public sealed record CreateGameResponse(Guid GameId, string Pin, GameStatus Status, string? JoinUrl = null);

public sealed record JoinGameResponse(
    Guid GameId,
    Guid ParticipantId,
    string SessionToken,
    string Nickname,
    GameStatus Status);

public sealed record PlayerChoiceResponse(Guid Id, int OrderIndex, string Text);

public sealed record HostChoiceResponse(Guid Id, int OrderIndex, string Text, bool IsCorrect);

public sealed record PlayerQuestionResponse(
    Guid QuestionId,
    int QuestionIndex,
    int TotalQuestions,
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    DateTimeOffset EndsAt,
    IReadOnlyList<PlayerChoiceResponse> Choices,
    bool AllowMultipleAnswers = false);

public sealed record HostQuestionResponse(
    Guid QuestionId,
    int QuestionIndex,
    int TotalQuestions,
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    IReadOnlyList<Guid> CorrectChoiceIds,
    IReadOnlyList<HostChoiceResponse> Choices);

public sealed record QuestionStartedResponse(HostQuestionResponse Host, PlayerQuestionResponse Player);

public sealed record AnswerAckResponse(bool Accepted, bool AlreadyAnswered);

public sealed record ChoiceResultResponse(Guid ChoiceId, string Text, int AnswerCount, bool IsCorrect);

public sealed record QuestionResultsResponse(
    Guid QuestionId,
    int QuestionIndex,
    IReadOnlyList<Guid> CorrectChoiceIds,
    int ParticipantCount,
    int AnswerCount,
    IReadOnlyList<ChoiceResultResponse> Choices);

public sealed record LeaderboardEntryResponse(int Rank, Guid ParticipantId, string Nickname, int TotalScore);

public sealed record LeaderboardResponse(IReadOnlyList<LeaderboardEntryResponse> Entries);

public sealed record GameParticipantResponse(
    Guid Id,
    string Nickname,
    int TotalScore,
    int? Rank,
    bool IsConnected,
    bool IsRemoved);

public static class ParticipantPresenceReasons
{
    public const string Joined = "Joined";
    public const string Reconnected = "Reconnected";
    public const string Disconnected = "Disconnected";
    public const string Removed = "Removed";
}

public sealed record ParticipantPresenceResponse(
    int ParticipantCount,
    long PresenceVersion,
    string Reason,
    GameParticipantResponse Participant);

public sealed record ParticipantPresenceMutationResponse(
    ParticipantPresenceResponse Presence,
    bool Changed);

public sealed record HostGameStateResponse(
    Guid GameId,
    string Pin,
    string QuizTitle,
    GameStatus Status,
    int? CurrentQuestionIndex,
    int TotalQuestions,
    DateTimeOffset? CurrentQuestionStartedAt,
    DateTimeOffset? CurrentQuestionEndsAt,
    int AnsweredCount,
    int ParticipantCount,
    long PresenceVersion,
    IReadOnlyList<GameParticipantResponse> Participants,
    string JoinUrl);

public sealed record PlayerGameStateResponse(
    Guid GameId,
    GameStatus Status,
    Guid ParticipantId,
    string Nickname,
    int TotalScore,
    int? Rank,
    int ParticipantCount,
    long PresenceVersion,
    bool AlreadyAnsweredCurrentQuestion,
    PlayerQuestionResponse? CurrentQuestion,
    QuestionResultsResponse? LastQuestionResults,
    LeaderboardResponse? Leaderboard);
