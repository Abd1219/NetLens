using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Abstraction for reading real-time network telemetry from the OS.
/// Decouples the Application and Services layers from Network infrastructure implementations.
/// </summary>
public interface ITelemetryCollector
{
    /// <summary>
    /// Captures the current network state and returns an immutable WirelessSnapshot.
    /// Returns null if the wireless adapter is not connected or data is unavailable.
    /// </summary>
    Task<WirelessSnapshot?> CaptureSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Scans for neighboring Wi-Fi access points using native WLAN APIs.
    /// Uses 100% real data from Windows WlanAPI. Returns empty list if no APs or Wi-Fi disabled.
    /// </summary>
    Task<IReadOnlyList<SurroundingNetworkInfo>> GetSurroundingNetworksAsync(CancellationToken cancellationToken);
}

public sealed record SurroundingNetworkInfo(
    string Ssid,
    string Bssid,
    int RssiDbm,
    int Channel,
    int FrequencyMhz,
    WifiBand Band,
    WifiSecurityType Security,
    string PhysicalType);
