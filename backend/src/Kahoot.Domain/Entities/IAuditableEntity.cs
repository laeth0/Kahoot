namespace Kahoot.Domain.Entities;

public interface IAuditableEntity
{
    // Creation Timestamp - UTC timestamp populated automatically when the entity is inserted
    DateTimeOffset CreatedAt { get; set; }

    // Modification Timestamp - UTC timestamp updated automatically whenever the entity is mutated
    DateTimeOffset UpdatedAt { get; set; }

    // Creator Identity - Optional user ID captured from the current ambient execution context
    Guid? CreatedBy { get; set; }

    // Modifier Identity - Optional user ID captured from the current ambient execution context upon mutation
    Guid? UpdatedBy { get; set; }
}
