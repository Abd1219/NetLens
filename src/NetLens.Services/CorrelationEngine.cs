using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Events;
using System.Collections.Concurrent;
using NetLens.Domain.Entities;

namespace NetLens.Services;

/// <summary>
/// Background analyzer that correlates multi-stream events (e.g. RSSI, Latency, and Gateway IP)
/// to detect complex anomalies like Roaming Flaps and Gateway Failovers.
/// </summary>
public sealed class CorrelationEngine : BackgroundService, IEventHandler<TelemetryCollectedEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<CorrelationEngine> _logger;
    private readonly ConcurrentQueue<WirelessSnapshot> _snapshots = new();
    private readonly TimeSpan _windowDuration = TimeSpan.FromMinutes(5);

    public CorrelationEngine(IEventBus eventBus, ILogger<CorrelationEngine> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Correlation Engine starting and subscribing to event stream...");
        _eventBus.Subscribe<TelemetryCollectedEvent>(this);

        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() =>
        {
            _logger.LogInformation("Correlation Engine stopping and unsubscribing...");
            _eventBus.Unsubscribe<TelemetryCollectedEvent>(this);
            tcs.SetResult();
        });

        return tcs.Task;
    }

    public async Task HandleAsync(TelemetryCollectedEvent @event, CancellationToken cancellationToken)
    {
        var newSnapshot = @event.Snapshot;
        _snapshots.Enqueue(newSnapshot);

        // Keep 5 minute sliding window
        var cutoff = DateTimeOffset.UtcNow - _windowDuration;
        while (_snapshots.TryPeek(out var oldest) && oldest.CapturedAt < cutoff)
        {
            _snapshots.TryDequeue(out _);
        }

        var recentSnapshots = _snapshots.ToList();

        // 1. Roaming Flap Detection: BSSID changes > 3 times in 60 seconds
        var oneMinuteAgo = DateTimeOffset.UtcNow.AddSeconds(-60);
        var oneMinuteSnapshots = recentSnapshots.Where(s => s.CapturedAt >= oneMinuteAgo).OrderBy(s => s.CapturedAt).ToList();
        if (oneMinuteSnapshots.Count > 1)
        {
            int bssidChanges = 0;
            for (int i = 1; i < oneMinuteSnapshots.Count; i++)
            {
                if (oneMinuteSnapshots[i].Bssid != oneMinuteSnapshots[i - 1].Bssid)
                {
                    bssidChanges++;
                }
            }

            if (bssidChanges > 3)
            {
                var evidence = new Dictionary<string, string>
                {
                    { "BssidChanges", bssidChanges.ToString() },
                    { "TimeWindowSeconds", "60" },
                    { "SSID", newSnapshot.Ssid }
                };

                _logger.LogWarning("Roaming Flap detected: SSID {SSID} changed BSSID {BssidChanges} times in 60 seconds.", newSnapshot.Ssid, bssidChanges);
                await _eventBus.PublishAsync(new CorrelationAlertEvent(
                    "RoamingFlap",
                    $"Device roamed between BSSIDs {bssidChanges} times in the last minute, indicating unstable wireless coverage.",
                    NetLens.Domain.Rules.DiagnosticSeverity.Warning,
                    evidence
                ), cancellationToken).ConfigureAwait(false);
            }
        }

        // 2. Gateway Failover Detection: Gateway IP changed
        if (recentSnapshots.Count > 1)
        {
            var currentGateway = newSnapshot.GatewayIp;
            var previousGateway = recentSnapshots[^2].GatewayIp;
            if (currentGateway.Value != previousGateway.Value)
            {
                var evidence = new Dictionary<string, string>
                {
                    { "OldGateway", previousGateway.Value },
                    { "NewGateway", currentGateway.Value }
                };

                _logger.LogWarning("Gateway failover detected: IP changed from {OldGateway} to {NewGateway}.", previousGateway.Value, currentGateway.Value);
                await _eventBus.PublishAsync(new CorrelationAlertEvent(
                    "GatewayFailover",
                    $"Local gateway changed from {previousGateway.Value} to {currentGateway.Value}.",
                    NetLens.Domain.Rules.DiagnosticSeverity.Critical,
                    evidence
                ), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
