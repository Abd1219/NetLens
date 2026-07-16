using NetLens.Domain.Entities;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Defines a contract for packet sniffing operations.
/// </summary>
public interface IPacketCapture
{
    bool IsCapturing { get; }
    Task StartCaptureAsync(string interfaceName, CancellationToken cancellationToken = default);
    void StopCapture();
    IAsyncEnumerable<CapturedPacket> GetPacketsAsync(CancellationToken cancellationToken = default);
}
