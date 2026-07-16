namespace NetLens.Domain.Entities;

using NetLens.Domain.Model;

/// <summary>
/// Represents a device discovered during a local subnet scan.
/// </summary>
public sealed class DiscoveredDevice
{
    public Guid DeviceId { get; }
    public IPAddressValue IpAddress { get; }
    public MacAddress MacAddress { get; }
    public string? Hostname { get; }
    public Latency ResponseTime { get; }
    public string DeviceType { get; }
    public DateTimeOffset DiscoveredAt { get; }

    public DiscoveredDevice(
        Guid deviceId,
        IPAddressValue ipAddress,
        MacAddress macAddress,
        string? hostname,
        Latency responseTime,
        string deviceType,
        DateTimeOffset discoveredAt)
    {
        DeviceId = deviceId;
        IpAddress = ipAddress;
        MacAddress = macAddress;
        Hostname = hostname;
        ResponseTime = responseTime;
        DeviceType = deviceType;
        DiscoveredAt = discoveredAt;
    }
}
