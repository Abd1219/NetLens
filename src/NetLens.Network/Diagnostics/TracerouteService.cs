using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using NetLens.Domain.Entities;
using NetLens.Domain.Model;
using NetLens.Network.Discovery;

namespace NetLens.Network.Diagnostics;

/// <summary>
/// Executes TTL-based ICMP ping traceroutes.
/// </summary>
public sealed class TracerouteService
{
    private readonly HostnameResolver _hostnameResolver;
    private readonly ILogger<TracerouteService> _logger;
    private const int TimeoutMs = 1500;

    public TracerouteService(HostnameResolver hostnameResolver, ILogger<TracerouteService> logger)
    {
        _hostnameResolver = hostnameResolver;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TracerouteHop>> TraceAsync(
        string host,
        int maxHops = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting traceroute to {Host}", host);
        var hops = new List<TracerouteHop>();

        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var latencies = new Latency[3];
            string? respondedIp = null;

            var options = new PingOptions(ttl, dontFragment: true);

            for (int i = 0; i < 3; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    using var ping = new Ping();
                    var stopwatch = Stopwatch.StartNew();
                    var reply = await ping.SendPingAsync(host, TimeoutMs, new byte[32], options)
                        .WaitAsync(TimeSpan.FromMilliseconds(TimeoutMs + 500), cancellationToken)
                        .ConfigureAwait(false);
                    stopwatch.Stop();

                    if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                    {
                        latencies[i] = new Latency(reply.RoundtripTime > 0 ? reply.RoundtripTime : stopwatch.Elapsed.TotalMilliseconds);
                        respondedIp = reply.Address?.ToString();
                    }
                    else
                    {
                        latencies[i] = Latency.Timeout;
                    }
                }
                catch
                {
                    latencies[i] = Latency.Timeout;
                }

                if (i < 2)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }

            if (respondedIp == null)
            {
                var timeoutHop = new TracerouteHop(ttl, "*", null, latencies[0], latencies[1], latencies[2]);
                hops.Add(timeoutHop);
                _logger.LogDebug("Hop {Ttl}: * (Request timed out)", ttl);
            }
            else
            {
                string? hostname = await _hostnameResolver.ResolveHostnameAsync(respondedIp, cancellationToken).ConfigureAwait(false);
                var hop = new TracerouteHop(ttl, respondedIp, hostname, latencies[0], latencies[1], latencies[2]);
                hops.Add(hop);
                _logger.LogDebug("Hop {Ttl}: {IpAddress} ({Hostname}) - {AvgLatency}", ttl, respondedIp, hostname ?? "unknown", hop.AverageLatency);

                if (respondedIp == host)
                {
                    break;
                }

                try
                {
                    var hostAddress = (await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
                    if (hostAddress != null && respondedIp == hostAddress.ToString())
                    {
                        break;
                    }
                }
                catch
                {
                    // Ignore resolution errors for host comparison
                }
            }
        }

        return hops;
    }
}
