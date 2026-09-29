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
    long StateVersion,
    long PresenceVersion,
    int ReservedParticipantCount,
    int ConnectedParticipantCount,
    string Nickname,
    int SeatNumber,
    string Reason);

public sealed record ParticipantRemovedEvent(
    Guid GameId,
    long StateVersion,
    Guid ParticipantId);

// Scorecard Feedback Telemetry (SCORE-RES-002) - Personal question result delivered to each participant upon question conclusion
public sealed record PersonalQuestionResultEvent(
    Guid ParticipantId,
    Guid GameId,
    long StateVersion,
    Guid QuestionId,
    int OrderIndex,
    bool Submitted,
    bool IsCorrect,
    int PointsAwarded,
    long TotalScore);

// Leaderboard Standing Telemetry (GAME-CTRL-003) - Personal rank and score position delivered to each participant upon leaderboard display
public sealed record PersonalLeaderboardEvent(
    Guid ParticipantId,
    Guid GameId,
    long StateVersion,
    int Rank,
    long TotalScore);

// Final Podium Telemetry (SCORE-RANK-001) - Personal final standing delivered to each participant upon game termination
public sealed record PersonalGameEndedEvent(
    Guid ParticipantId,
    Guid GameId,
    long StateVersion,
    int Rank,
    long TotalScore);

// Reconnection Catch-Up Choice Projection (RECON-CATCH-001, RECON-SEC-002) - Choice option snapshot omitting correctness indicators
public sealed record PlayerChoiceSnapshotDto(Guid Id, string Text, int OrderIndex);

// Reconnection Catch-Up Leaderboard Entry (RECON-CATCH-001) - Top-ranked contender summary for visible standings
public sealed record LeaderboardPlayerDto(Guid ParticipantId, string Nickname, int SeatNumber, long TotalScore, int? Rank);

// Reconnection Catch-Up Podium Entry (RECON-CATCH-001) - Top-3 finalist summary for read-only game conclusion
public sealed record PodiumPlayerDto(Guid ParticipantId, string Nickname, int SeatNumber, long TotalScore, int? Rank);

// Lobby Catch-Up State (RECON-CATCH-001) - Self-contained lobby projection reflecting assigned seat and reservation count
public sealed record PlayerLobbyStateResponse(
    string Status,
    Guid GameId,
    long StateVersion,
    string Title,
    string Nickname,
    int SeatNumber,
    int TotalParticipants);

// Question Active Catch-Up State (RECON-CATCH-001, RECON-SEC-002) - Active question projection withholding correctness and provisional points
public sealed record PlayerQuestionActiveStateResponse(
    string Status,
    Guid GameId,
    long StateVersion,
    int QuestionIndex,
    int TotalQuestions,
    string QuestionText,
    string? ImageUrl,
    DateTimeOffset? DeadlineUtc,
    int RemainingSeconds,
    bool AlreadyAnswered,
    List<PlayerChoiceSnapshotDto> Choices,
    long TotalScore);

// Question Results Catch-Up State (RECON-CATCH-001) - Full reveal of question choices, correctness, stats, and personal score
public sealed record PlayerQuestionResultsStateResponse(
    string Status,
    Guid GameId,
    long StateVersion,
    int QuestionIndex,
    int TotalQuestions,
    string QuestionText,
    string? ImageUrl,
    List<QuestionChoiceResultDto> Choices,
    List<Guid> CorrectChoiceIds,
    bool Submitted,
    bool IsCorrect,
    int PointsAwarded,
    List<Guid> SelectedChoiceIds,
    long TotalScore);

// Leaderboard Catch-Up State (RECON-CATCH-001) - Visible standings with top 5 contenders and personal sequential rank
public sealed record PlayerLeaderboardStateResponse(
    string Status,
    Guid GameId,
    long StateVersion,
    long TotalScore,
    int Rank,
    List<LeaderboardPlayerDto> TopParticipants);

// Finished Game Catch-Up State (RECON-CATCH-001, RECON-WINDOW-001) - Read-only final summary with podium, rank, and total accepted answers
public sealed record PlayerFinishedStateResponse(
    string Status,
    Guid GameId,
    long StateVersion,
    long TotalScore,
    int Rank,
    int AcceptedAnswers,
    List<PodiumPlayerDto> Podium);
