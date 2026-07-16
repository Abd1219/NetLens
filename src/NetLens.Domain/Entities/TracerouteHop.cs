namespace NetLens.Domain.Entities;

using NetLens.Domain.Model;

/// <summary>
/// Represents a single hop in a traceroute diagnostics run.
/// </summary>
public sealed record TracerouteHop(
    int HopNumber,
    string IpAddress,
    string? Hostname,
    Latency Latency1,
    Latency Latency2,
    Latency Latency3)
{
    public Latency AverageLatency
    {
        get
        {
            var latencies = new[] { Latency1, Latency2, Latency3 };
            var active = latencies.Where(l => !l.IsTimeout).ToList();
            if (active.Count == 0)
                return Latency.Timeout;
            return new Latency(active.Average(l => l.Milliseconds));
        }
    }
}
