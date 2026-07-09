using System.Net.NetworkInformation;
using NetLens.Domain.Model;

namespace NetLens.Network.Diagnostics;

/// <summary>
/// Executes ICMP ping probes and computes latency, packet loss, and jitter.
/// All probes are non-blocking and respect the CancellationToken.
/// </summary>
public sealed class PingService
{
    private const int PingTimeoutMs = 2000;

    /// <summary>
    /// Sends multiple ICMP pings to a host and returns aggregated metrics.
    /// </summary>
    public async Task<PingResult> PingAsync(
        string host,
        int probeCount = 5,
        CancellationToken cancellationToken = default)
    {
        var roundTripTimes = new List<double>();
        int timeouts = 0;

        for (int i = 0; i < probeCount; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, PingTimeoutMs);

                if (reply.Status == IPStatus.Success)
                {
                    roundTripTimes.Add(reply.RoundtripTime);
                }
                else
                {
                    timeouts++;
                }
            }
            catch (PingException)
            {
                timeouts++;
            }

            // Small delay between probes to avoid burst flooding the gateway
            if (i < probeCount - 1)
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        int totalSent = roundTripTimes.Count + timeouts;

        if (roundTripTimes.Count == 0)
        {
            return new PingResult(
                AverageLatency: Latency.Timeout,
                PacketLoss: new PacketLossRate(100),
                Jitter: new Jitter(0));
        }

        var avgMs = roundTripTimes.Average();
        var lossPercent = (double)timeouts / totalSent * 100.0;

        // Jitter = average of absolute differences between consecutive round-trip times
        double jitterMs = 0;
        if (roundTripTimes.Count > 1)
        {
            var diffs = new List<double>();
            for (int i = 1; i < roundTripTimes.Count; i++)
                diffs.Add(Math.Abs(roundTripTimes[i] - roundTripTimes[i - 1]));
            jitterMs = diffs.Average();
        }

        return new PingResult(
            AverageLatency: new Latency(avgMs),
            PacketLoss: new PacketLossRate(Math.Round(lossPercent, 1)),
            Jitter: new Jitter(Math.Round(jitterMs, 1)));
    }
}

/// <summary>
/// Aggregated result of a multi-probe ping test.
/// </summary>
public sealed record PingResult(
    Latency AverageLatency,
    PacketLossRate PacketLoss,
    Jitter Jitter);
