namespace Kahoot.Domain.Entities;

public sealed class GameQuestionSnapshot
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid GameId { get; set; }

    public int OrderIndex { get; set; }

    public required string Text { get; set; }

    public Guid? ImageId { get; set; }

    public string? ImageUrl { get; set; }

    public int DurationSeconds { get; set; }

    public int BasePoints { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public int InitialEligibleParticipantCount { get; set; }

    public int EffectiveEligibleParticipantCount { get; set; }

    public int AcceptedAnswerCount { get; set; }

    public DateTimeOffset? ResultsMaterializedAt { get; set; }
}
