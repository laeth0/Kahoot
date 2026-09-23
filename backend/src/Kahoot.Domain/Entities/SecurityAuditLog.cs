using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class SecurityAuditLog
{
    public Guid Id { get; set; }

    public Guid ActorAccountId { get; set; }

    public UserRole ActorRole { get; set; }

    public Guid? TargetAccountId { get; set; }

    public required string Action { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public required string RequestId { get; set; }

    public required string Outcome { get; set; }

    public string? ReasonCode { get; set; }
}
