namespace Kahoot.Domain.Entities;

public sealed class ParticipantSessionToken
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid GameId { get; set; }

    public Guid ParticipantId { get; set; }

    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
