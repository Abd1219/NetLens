namespace NetLens.Domain.Events;

/// <summary>
/// Domain event published when the correlation engine flags a complex network anomaly.
/// </summary>
public sealed record CorrelationAlertEvent(
    string AlertType,
    string Description,
    string Severity,
    IReadOnlyDictionary<string, string> Evidence) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
