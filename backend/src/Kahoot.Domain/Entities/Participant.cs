namespace Kahoot.Domain.Entities;

public sealed class Participant
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid GameId { get; set; }

    public required string DisplayNickname { get; set; }

    public required string NormalizedNickname { get; set; }

    public int SeatNumber { get; set; }

    public required byte[] JoinOperationIdHash { get; set; }

    public DateTimeOffset JoinRecoveryExpiresAt { get; set; }

    public bool IsRemoved { get; set; }

    public DateTimeOffset? RemovedAt { get; set; }

    public long TotalScore { get; set; }

    public int? Rank { get; set; }

    public long ConnectionGeneration { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
