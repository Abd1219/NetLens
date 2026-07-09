namespace NetLens.Domain.Events;

/// <summary>
/// Defines a contract for all domain events published within the NetLens platform.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The unique identifier of the event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// The timestamp when the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
