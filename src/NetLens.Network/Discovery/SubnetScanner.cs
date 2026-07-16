using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Model;
using NetLens.Network.Diagnostics;

namespace NetLens.Network.Discovery;

/// <summary>
/// Scans a /24 subnet using parallel pinging and ARP resolution.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SubnetScanner
{
    private readonly PingService _pingService;
    private readonly ArpResolver _arpResolver;
    private readonly HostnameResolver _hostnameResolver;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SubnetScanner> _logger;

    public SubnetScanner(
        PingService pingService,
        ArpResolver arpResolver,
        HostnameResolver hostnameResolver,
        IEventBus eventBus,
        ILogger<SubnetScanner> logger)
    {
        _pingService = pingService;
        _arpResolver = arpResolver;
        _hostnameResolver = hostnameResolver;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredDevice>> ScanSubnetAsync(
        string localIp,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting subnet scan for local IP: {LocalIp}", localIp);

        if (!IPAddress.TryParse(localIp, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            _logger.LogError("Invalid or non-IPv4 address provided for subnet scan: {LocalIp}", localIp);
            return Array.Empty<DiscoveredDevice>();
        }

        var ipBytes = ip.GetAddressBytes();
        var prefix = $"{ipBytes[0]}.{ipBytes[1]}.{ipBytes[2]}.";

        var devices = new List<DiscoveredDevice>();
        int completedCount = 0;
        const int totalHosts = 254;

        var ipList = Enumerable.Range(1, totalHosts).Select(i => $"{prefix}{i}").ToList();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 32,
            CancellationToken = cancellationToken
        };

        var lockObj = new object();

        await Parallel.ForEachAsync(ipList, parallelOptions, async (targetIp, ct) =>
        {
            try
            {
                // Ping the target IP with 1 probe (fast scan)
                var pingResult = await _pingService.PingAsync(targetIp, probeCount: 1, cancellationToken: ct).ConfigureAwait(false);

                if (!pingResult.AverageLatency.IsTimeout)
                {
                    // Active host! Resolve MAC and Hostname
                    var macStr = _arpResolver.ResolveMAC(targetIp);
                    if (macStr != null)
                    {
                        var hostname = await _hostnameResolver.ResolveHostnameAsync(targetIp, ct).ConfigureAwait(false);

                        // Heuristic for device type
                        string deviceType = DetermineDeviceType(targetIp, hostname, macStr);

                        var device = new DiscoveredDevice(
                            Guid.NewGuid(),
                            new IPAddressValue(targetIp),
                            new MacAddress(macStr),
                            hostname,
                            pingResult.AverageLatency,
                            deviceType,
                            DateTimeOffset.UtcNow
                        );

                        lock (lockObj)
                        {
                            devices.Add(device);
                        }

                        // Publish Domain Event
                        await _eventBus.PublishAsync(new DeviceDiscoveredEvent(device), ct).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error scanning host {TargetIp}", targetIp);
            }
            finally
            {
                var newCompleted = Interlocked.Increment(ref completedCount);
                int percent = (int)((double)newCompleted / totalHosts * 100.0);
                progress?.Report(percent);
            }
        }).ConfigureAwait(false);

        _logger.LogInformation("Subnet scan completed. Found {Count} devices.", devices.Count);
        return devices;
    }

    private static string DetermineDeviceType(string ip, string? hostname, string mac)
    {
        hostname = hostname?.ToLowerInvariant() ?? "";
        mac = mac.Replace(":", "").ToUpperInvariant();

        if (hostname.Contains("gateway") || hostname.Contains("router") || ip.EndsWith(".1"))
            return "Gateway";

        if (hostname.Contains("printer") || hostname.Contains("hp") || hostname.Contains("epson"))
            return "Printer";

        if (hostname.Contains("phone") || hostname.Contains("iphone") || hostname.Contains("android"))
            return "Mobile";

        if (hostname.Contains("tv") || hostname.Contains("smart") || hostname.Contains("roku") || hostname.Contains("chromecast"))
            return "Smart TV";

        // Some OUI heuristics
        if (mac.StartsWith("001132")) // Synology
            return "NAS/Server";

        return "Workstation";
    }
}
