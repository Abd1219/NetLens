using NetLens.Domain.Entities;
using NetLens.Domain.Events;

namespace NetLens.Domain.Events;

/// <summary>
/// Published when a new WirelessSnapshot is successfully captured and added to the session ledger.
/// Triggers downstream rule evaluation and dashboard refresh.
/// </summary>
public sealed record TelemetryCollectedEvent(
    Guid SessionId,
    WirelessSnapshot Snapshot) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
