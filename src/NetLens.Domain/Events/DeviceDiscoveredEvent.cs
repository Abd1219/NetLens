namespace NetLens.Domain.Events;

using NetLens.Domain.Entities;

/// <summary>
/// Domain event published when a new device is discovered on the local network.
/// </summary>
public sealed record DeviceDiscoveredEvent(
    DiscoveredDevice Device) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
