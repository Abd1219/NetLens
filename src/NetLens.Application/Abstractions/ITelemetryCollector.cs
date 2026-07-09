using NetLens.Domain.Entities;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Abstraction for reading real-time network telemetry from the OS.
/// Decouples the Services layer from the Network layer implementations.
/// </summary>
public interface ITelemetryCollector
{
    /// <summary>
    /// Captures the current network state and returns an immutable WirelessSnapshot.
    /// Returns null if the wireless adapter is not connected or data is unavailable.
    /// </summary>
    Task<WirelessSnapshot?> CaptureSnapshotAsync(CancellationToken cancellationToken);
}
