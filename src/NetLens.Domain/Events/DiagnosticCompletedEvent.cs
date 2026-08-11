using NetLens.Domain.Entities;
using NetLens.Domain.Rules;

namespace NetLens.Domain.Events;

/// <summary>
/// Domain event published when the Diagnostic Engine has finished evaluating rules (atomic + correlation)
/// and conflict suppression on a snapshot.
/// </summary>
public sealed record DiagnosticCompletedEvent(
    Guid SessionId,
    WirelessSnapshot Snapshot,
    IReadOnlyList<DiagnosticResult> Results) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
