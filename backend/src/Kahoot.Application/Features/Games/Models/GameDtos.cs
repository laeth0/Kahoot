namespace Kahoot.Application.Features.Games.Models;

public sealed record QuestionChoiceDto(
    Guid ChoiceId,
    int OrderIndex,
    string Text,
    bool? IsCorrect);

public sealed record PlayerQuestionChoiceDto(Guid ChoiceId, int OrderIndex, string Text);

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
    List<PlayerQuestionChoiceDto> Choices);

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
