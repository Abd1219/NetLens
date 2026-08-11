namespace NetLens.Domain.Events;

/// <summary>
/// Domain event published when a Wi-Fi connection is established.
/// </summary>
public sealed record WifiConnectedEvent(
    DateTimeOffset OccurredAt,
    string Ssid,
    string Bssid) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

/// <summary>
/// Domain event published when the Wi-Fi connection is lost or disconnected.
/// </summary>
public sealed record WifiDisconnectedEvent(
    DateTimeOffset OccurredAt,
    string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

/// <summary>
/// Domain event published when the active SSID changes.
/// </summary>
public sealed record SsidChangedEvent(
    DateTimeOffset OccurredAt,
    string OldSsid,
    string NewSsid) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

/// <summary>
/// Domain event published when the active BSSID changes.
/// Structured to allow future roaming analysis engines to process BSSID transitions.
/// </summary>
public sealed record BssidChangedEvent(
    DateTimeOffset OccurredAt,
    string OldBssid,
    string NewBssid) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

/// <summary>
/// Domain event published when the active network interface or physical adapter changes.
/// </summary>
public sealed record AdapterChangedEvent(
    DateTimeOffset OccurredAt,
    string OldAdapter,
    string NewAdapter) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
