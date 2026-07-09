using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;

namespace NetLens.Services;

/// <summary>
/// Long-running background service that drives the telemetry collection loop.
/// Runs independently of the UI thread. Publishes TelemetryCollectedEvent
/// on each successful snapshot capture, triggering the Rule Engine downstream.
/// </summary>
public sealed class TelemetryBackgroundService : BackgroundService
{
    // Collection interval: configurable via options in future, fixed for v0.5
    private static readonly TimeSpan CollectionInterval = TimeSpan.FromSeconds(3);

    private readonly ITelemetryCollector _collector;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TelemetryBackgroundService> _logger;

    private DiagnosticSession? _activeSession;

    public TelemetryBackgroundService(
        ITelemetryCollector collector,
        IEventBus eventBus,
        ILogger<TelemetryBackgroundService> logger)
    {
        _collector = collector;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Starts a diagnostic session and begins the telemetry collection loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NetLens Telemetry Service starting...");

        _activeSession = new DiagnosticSession();
        _activeSession.Start();

        _logger.LogInformation("Diagnostic session {SessionId} started.", _activeSession.SessionId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _collector.CaptureSnapshotAsync(stoppingToken);

                if (snapshot is not null)
                {
                    _activeSession.RecordSnapshot(snapshot);

                    await _eventBus.PublishAsync(
                        new TelemetryCollectedEvent(_activeSession.SessionId, snapshot),
                        stoppingToken);

                    _logger.LogDebug(
                        "Snapshot captured: RSSI={Rssi}, PHY TX={Tx}, Packet Loss={Loss}, Gateway={Gw}",
                        snapshot.Rssi,
                        snapshot.TxRate,
                        snapshot.PacketLoss,
                        snapshot.GatewayLatency);
                }

                await Task.Delay(CollectionInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in telemetry collection loop.");
                // Back off briefly before retrying to avoid hot error loops
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }

        _activeSession.End();
        _logger.LogInformation(
            "Diagnostic session {SessionId} ended. {Count} snapshots recorded.",
            _activeSession.SessionId,
            _activeSession.Ledger.Count);
    }

    /// <summary>
    /// Returns the currently active diagnostic session, if any.
    /// </summary>
    public DiagnosticSession? GetActiveSession() => _activeSession;
}
