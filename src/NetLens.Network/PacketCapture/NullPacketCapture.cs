using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;

namespace NetLens.Network.PacketCapture;

/// <summary>
/// A no-op fallback implementation of packet capture used when native libraries (like Npcap) are unavailable.
/// </summary>
public sealed class NullPacketCapture : IPacketCapture
{
    private readonly ILogger<NullPacketCapture> _logger;

    public bool IsCapturing => false;

    public NullPacketCapture(ILogger<NullPacketCapture> logger)
    {
        _logger = logger;
    }

    public Task StartCaptureAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Npcap/Packet capture native libraries are not installed. Packet capture is in fallback Null mode on interface {Interface}", interfaceName);
        return Task.CompletedTask;
    }

    public void StopCapture()
    {
    }

    public async IAsyncEnumerable<CapturedPacket> GetPacketsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }
}
