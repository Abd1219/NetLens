using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Model;
using NetLens.Domain.Rules;
using NetLens.UI.Models;
using NetLens.UI.Services;

namespace NetLens.UI.ViewModels;

// ── WIFI EXPLORER VIEWMODEL ─────────────────────────────────────────────
public sealed partial class WifiExplorerViewModel : ObservableObject, IEventHandler<TelemetryCollectedEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ITelemetryCollector _telemetryCollector;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] private string _ssid = "Not Connected";
    [ObservableProperty] private string _bssid = "—";
    [ObservableProperty] private string _rssi = "— dBm";
    [ObservableProperty] private string _channel = "—";
    [ObservableProperty] private string _signalQuality = "—%";

    public ObservableCollection<WifiNetworkItem> SurroundingNetworks { get; } = [];

    public WifiExplorerViewModel(IEventBus eventBus, ITelemetryCollector telemetryCollector)
    {
        _eventBus = eventBus;
        _telemetryCollector = telemetryCollector;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _eventBus.Subscribe<TelemetryCollectedEvent>(this);

        _ = RefreshSurroundingNetworksAsync();
    }

    public async Task RefreshSurroundingNetworksAsync()
    {
        try
        {
            var networks = await _telemetryCollector.GetSurroundingNetworksAsync(CancellationToken.None);
            _dispatcher.TryEnqueue(() =>
            {
                SurroundingNetworks.Clear();
                foreach (var net in networks)
                {
                    SurroundingNetworks.Add(new WifiNetworkItem(
                        net.Ssid,
                        net.Bssid,
                        net.RssiDbm,
                        net.Channel,
                        net.Band.ToDisplayString(),
                        net.Security.ToDisplayString(),
                        net.PhysicalType));
                }
            });
        }
        catch
        {
            _dispatcher.TryEnqueue(() => SurroundingNetworks.Clear());
        }
    }

    public Task HandleAsync(TelemetryCollectedEvent @event, CancellationToken cancellationToken)
    {
        var s = @event.Snapshot;
        _dispatcher.TryEnqueue(() =>
        {
            Ssid = s.Ssid;
            Bssid = s.Bssid.Value;
            Rssi = s.Rssi.ToString();
            Channel = s.Channel.ToString();
            SignalQuality = s.SignalQuality.ToString();
        });

        _ = RefreshSurroundingNetworksAsync();
        return Task.CompletedTask;
    }
}

public sealed partial class WifiNetworkItem : ObservableObject
{
    public string Ssid { get; }
    public string Bssid { get; }
    public int Rssi { get; }
    public int Channel { get; }
    public string Band { get; }
    public string Security { get; }
    public string Standards { get; }

    public WifiNetworkItem(string ssid, string bssid, int rssi, int channel, string band, string security, string standards)
    {
        Ssid = ssid;
        Bssid = bssid;
        Rssi = rssi;
        Channel = channel;
        Band = band;
        Security = security;
        Standards = standards;
    }
}

// ── DIAGNOSTICS VIEWMODEL ───────────────────────────────────────────────
public sealed partial class DiagnosticsViewModel : ObservableObject, IEventHandler<DiagnosticCompletedEvent>
{
    private readonly ITelemetryCollector _telemetryCollector;
    private readonly IDiagnosticService _diagnosticService;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;

    private IReadOnlyList<DiagnosticResult> _lastAlerts = [];

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _healthScore = 100;
    
    public ObservableCollection<DiagnosticResultDisplay> Results { get; } = [];

    public ICommand RunDiagnosticCommand { get; }

    public DiagnosticsViewModel(
        ITelemetryCollector telemetryCollector,
        IDiagnosticService diagnosticService,
        IEventBus eventBus)
    {
        _telemetryCollector = telemetryCollector;
        _diagnosticService = diagnosticService;
        _loc = App.Services.GetRequiredService<LocalizationService>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        RunDiagnosticCommand = new AsyncRelayCommand(RunDiagnosticAsync);

        _statusMessage = _loc.GetString("Diagnostics_Ready");
        _loc.LanguageChanged += OnLanguageChanged;

        eventBus.Subscribe(this);
    }

    private void OnLanguageChanged()
    {
        _dispatcher.TryEnqueue(() => UpdateStatusMessage(_lastAlerts));
    }

    public Task HandleAsync(DiagnosticCompletedEvent @event, CancellationToken cancellationToken)
    {
        _dispatcher.TryEnqueue(() => DisplayResults(@event.Results));
        return Task.CompletedTask;
    }

    private async Task RunDiagnosticAsync()
    {
        IsRunning = true;
        StatusMessage = _loc.GetString("Diagnostics_Capturing");
        Results.Clear();

        try
        {
            var snapshot = await _telemetryCollector.CaptureSnapshotAsync(CancellationToken.None);
            if (snapshot == null)
            {
                StatusMessage = _loc.GetString("Diagnostics_AdapterError");
                HealthScore = 0;
                return;
            }

            StatusMessage = _loc.GetString("Diagnostics_Analyzing");
            var alerts = _diagnosticService.AnalyzeSnapshot(snapshot);
            DisplayResults(alerts);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(_loc.GetString("Diagnostics_Failed"), ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void DisplayResults(IReadOnlyList<DiagnosticResult> alerts)
    {
        _lastAlerts = alerts;
        Results.Clear();
        int score = 100;
        foreach (var alert in alerts)
        {
            score -= alert.Severity switch
            {
                DiagnosticSeverity.Critical => 25,
                DiagnosticSeverity.Warning  => 10,
                DiagnosticSeverity.Info     => 2,
                _                           => 0
            };
            Results.Add(new DiagnosticResultDisplay(alert, _loc));
        }
        HealthScore = Math.Max(0, score);

        UpdateStatusMessage(alerts);
    }

    private void UpdateStatusMessage(IReadOnlyList<DiagnosticResult> alerts)
    {
        if (IsRunning) return;

        StatusMessage = alerts.Count > 0 
            ? string.Format(_loc.GetString("Diagnostics_CompleteIssues"), alerts.Count)
            : _loc.GetString("Diagnostics_CompleteNormal");
    }
}

// ── HISTORY VIEWMODEL ───────────────────────────────────────────────────
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IReportGenerator _reportGenerator;

    public ObservableCollection<SessionSummary> Sessions { get; } = [];

    public ICommand LoadSessionsCommand { get; }
    public ICommand ExportReportCommand { get; }

    public HistoryViewModel(ISessionRepository sessionRepository, IReportGenerator reportGenerator)
    {
        _sessionRepository = sessionRepository;
        _reportGenerator = reportGenerator;

        LoadSessionsCommand = new AsyncRelayCommand(LoadSessionsAsync);
        ExportReportCommand = new AsyncRelayCommand<SessionSummary>(ExportReportAsync);

        _ = LoadSessionsAsync();
    }

    public async Task LoadSessionsAsync()
    {
        Sessions.Clear();
        var list = await _sessionRepository.GetRecentSessionsAsync(50, CancellationToken.None);
        foreach (var s in list.OrderByDescending(x => x.StartedAt))
        {
            Sessions.Add(s);
        }
    }

    private async Task ExportReportAsync(SessionSummary? summary)
    {
        if (summary == null) return;

        var fullSession = await _sessionRepository.GetSessionByIdAsync(summary.SessionId, CancellationToken.None);
        if (fullSession == null) return;

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("PDF Document", new List<string>() { ".pdf" });
        savePicker.SuggestedFileName = $"NetLens_Report_{summary.SessionId.ToString()[..8]}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            try
            {
                var pdfBytes = _reportGenerator.GeneratePdfReport(fullSession);
                await Windows.Storage.FileIO.WriteBytesAsync(file, pdfBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
            }
        }
    }
}
