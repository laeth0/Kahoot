namespace Kahoot.Domain.Entities;

public sealed class GameCommandIdempotency
{
    public Guid GameId { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid CommandId { get; set; }

    public required string CommandName { get; set; }

    public required byte[] RequestHash { get; set; }

    public long ResultStateVersion { get; set; }

    public required string ResponsePayload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
