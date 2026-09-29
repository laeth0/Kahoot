namespace Kahoot.Application.Features.Games.Models;

public sealed record QuestionChoiceDto(
    Guid ChoiceId,
    int OrderIndex,
    string Text,
    bool? IsCorrect);

public sealed record PlayerQuestionChoiceDto(Guid ChoiceId, int OrderIndex, string Text);

// System Design & Audience Isolation: Player projection omits correct choices to eliminate client-side cheating via network inspection
public sealed record PlayerQuestionStartedEvent(
    Guid GameId,
    long StateVersion,
    Guid QuestionId,
    int OrderIndex,
    int TotalQuestions,
    string Text,
    string? ImageUrl,
    int DurationSeconds,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    List<PlayerQuestionChoiceDto> Choices)
{
    public int QuestionIndex => OrderIndex - 1;
}

// System Design & State Topology: Host projection includes correct choices and grading telemetry for presentation display
public sealed record HostQuestionStartedEvent(
    Guid GameId,
    long StateVersion,
    Guid QuestionId,
    int OrderIndex,
    int TotalQuestions,
    string Text,
    string? ImageUrl,
    int DurationSeconds,
    int BasePoints,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    int EffectiveEligibleParticipantCount,
    List<Guid> CorrectChoiceIds,
    List<QuestionChoiceDto> Choices);

// Realtime Telemetry: Question results broadcast with materialized selection distributions
public sealed record QuestionEndedEvent(
    Guid GameId,
    long StateVersion,
    Guid QuestionId,
    int OrderIndex,
    int TotalAnswers,
    int EffectiveEligibleParticipantCount,
    DateTimeOffset ResultsMaterializedAt,
    List<Guid> CorrectChoiceIds,
    List<QuestionChoiceResultDto> Choices);

// State Synchronization - Carries question timer bounds and snapshot metadata for late-joining or reconnecting hosts
public sealed record CurrentQuestionDto(
    Guid QuestionId,
    int OrderIndex,
    string Text,
    string? ImageUrl,
    int DurationSeconds,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    int EligibleParticipants,
    List<QuestionChoiceDto> Choices);

public sealed record QuestionChoiceResultDto(
    Guid ChoiceId,
    int OrderIndex,
    string Text,
    bool IsCorrect,
    int SelectionCount);

// System Design & Bounded Payload: Top-ranked leaderboard projection bounds payload size to top 5 contenders
public sealed record LeaderboardParticipantDto(
    Guid ParticipantId,
    string Nickname,
    long TotalScore,
    int Rank);

public sealed record PodiumParticipantDto(
    int Rank,
    string Nickname,
    long TotalScore);

public sealed record QuestionReportDto(
    int OrderIndex,
    string Text,
    int TotalAnswers,
    int CorrectAnswers,
    List<QuestionChoiceResultDto> Choices);

public sealed record ParticipantPresenceChangedEvent(
    Guid GameId,
    long PresenceVersion,
    int ReservedParticipantCount,
    int ConnectedParticipantCount,
    string Nickname,
    int SeatNumber,
    string Reason);

public sealed record ParticipantRemovedEvent(
    Guid GameId,
    Guid ParticipantId,
    string Reason);
