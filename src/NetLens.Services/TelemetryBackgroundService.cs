using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;

namespace NetLens.Services;

/// <summary>
/// Long-running background service driving real-time telemetry collection and state monitoring.
/// Evaluates state transitions (Connection, Disconnection, SSID change, BSSID change, Adapter change)
/// and publishes domain events asynchronously over the IEventBus without blocking the UI thread.
/// </summary>
public sealed class TelemetryBackgroundService : BackgroundService
{
    private static readonly TimeSpan CollectionInterval = TimeSpan.FromSeconds(3);

    private readonly ITelemetryCollector _collector;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TelemetryBackgroundService> _logger;

    private DiagnosticSession? _activeSession;
    private WirelessSnapshot? _previousSnapshot;

    public TelemetryBackgroundService(
        ITelemetryCollector collector,
        IEventBus eventBus,
        ILogger<TelemetryBackgroundService> logger)
    {
        _collector = collector;
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring started. NetLens Telemetry Service initialization complete.");

        _activeSession = new DiagnosticSession();
        _activeSession.Start();

        _logger.LogInformation("Diagnostic session {SessionId} started.", _activeSession.SessionId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _collector.CaptureSnapshotAsync(stoppingToken);

                // Detect and publish state transitions
                await EvaluateStateTransitionsAsync(_previousSnapshot, snapshot, _eventBus, _logger, stoppingToken);
                _previousSnapshot = snapshot;

                if (snapshot is not null)
                {
                    _activeSession.RecordSnapshot(snapshot);

                    await _eventBus.PublishAsync(
                        new TelemetryCollectedEvent(_activeSession.SessionId, snapshot),
                        stoppingToken);

                    _logger.LogDebug(
                        "Snapshot recorded: RSSI={Rssi}, Band={Band}, State={State}, Tx={Tx}, GatewayLatency={Gw}",
                        snapshot.Rssi,
                        snapshot.Band,
                        snapshot.ConnectionState,
                        snapshot.TxRate,
                        snapshot.GatewayLatency);
                }

                await Task.Delay(CollectionInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WlanAPI or telemetry exception encountered in collection loop. Retrying cleanly.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }

        _activeSession.End();
        _logger.LogInformation(
            "Monitoring stopped. Diagnostic session {SessionId} ended with {Count} snapshots.",
            _activeSession.SessionId,
            _activeSession.Ledger.Count);
    }

    /// <summary>
    /// Evaluates network state transitions between consecutive snapshots and emits corresponding domain events.
    /// Internal static method to allow explicit unit testing without requiring real hardware.
    /// </summary>
    internal static async Task EvaluateStateTransitionsAsync(
        WirelessSnapshot? previous,
        WirelessSnapshot? current,
        IEventBus eventBus,
        ILogger logger,
        CancellationToken ct)
    {
        var now = current?.CapturedAt ?? DateTimeOffset.UtcNow;

        // Disconnected -> Connected
        if (previous is null && current is not null)
        {
            logger.LogInformation("WiFi connected: SSID={Ssid}, BSSID={Bssid}", current.Ssid, current.Bssid);
            await eventBus.PublishAsync(new WifiConnectedEvent(now, current.Ssid, current.Bssid.Value), ct);
        }
        // Connected -> Disconnected
        else if (previous is not null && current is null)
        {
            logger.LogInformation("WiFi disconnected: Interface connection lost.");
            await eventBus.PublishAsync(new WifiDisconnectedEvent(now, "Connection Lost"), ct);
        }
        else if (previous is not null && current is not null)
        {
            // SSID Changed
            if (!string.Equals(previous.Ssid, current.Ssid, StringComparison.Ordinal))
            {
                logger.LogInformation("SSID changed: {OldSsid} -> {NewSsid}", previous.Ssid, current.Ssid);
                await eventBus.PublishAsync(new SsidChangedEvent(now, previous.Ssid, current.Ssid), ct);
            }

            // BSSID Changed
            if (!string.Equals(previous.Bssid.Value, current.Bssid.Value, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("BSSID changed: {OldBssid} -> {NewBssid}", previous.Bssid, current.Bssid);
                await eventBus.PublishAsync(new BssidChangedEvent(now, previous.Bssid.Value, current.Bssid.Value), ct);
            }

            // Adapter Changed
            if (!string.Equals(previous.AdapterName, current.AdapterName, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Adapter changed: {OldAdapter} -> {NewAdapter}", previous.AdapterName, current.AdapterName);
                await eventBus.PublishAsync(new AdapterChangedEvent(now, previous.AdapterName, current.AdapterName), ct);
            }
        }
    }

    public DiagnosticSession? GetActiveSession() => _activeSession;
}
