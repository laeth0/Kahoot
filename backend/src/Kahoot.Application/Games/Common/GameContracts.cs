using Kahoot.Domain.Games;

namespace Kahoot.Application.Games.Common;

public sealed record CreateGameResponse(Guid GameId, string Pin, GameStatus Status);

public sealed record JoinGameResponse(
    Guid GameId,
    Guid ParticipantId,
    string SessionToken,
    string Nickname,
    GameStatus Status);

public sealed record PlayerChoiceResponse(Guid Id, int OrderIndex, string? Text, string? ImageUrl);

public sealed record HostChoiceResponse(Guid Id, int OrderIndex, string? Text, string? ImageUrl, bool IsCorrect);

public sealed record PlayerQuestionResponse(
    Guid QuestionId,
    int QuestionIndex,
    int TotalQuestions,
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    DateTimeOffset EndsAt,
    IReadOnlyList<PlayerChoiceResponse> Choices);

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

public sealed record ChoiceResultResponse(Guid ChoiceId, string? Text, int AnswerCount, bool IsCorrect);

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
    IReadOnlyList<GameParticipantResponse> Participants);

public sealed record PlayerGameStateResponse(
    Guid GameId,
    GameStatus Status,
    Guid ParticipantId,
    string Nickname,
    int TotalScore,
    int? Rank,
    bool AlreadyAnsweredCurrentQuestion,
    PlayerQuestionResponse? CurrentQuestion,
    QuestionResultsResponse? LastQuestionResults,
    LeaderboardResponse? Leaderboard);
