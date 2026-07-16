using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Model;
using NetLens.Domain.Rules;

namespace NetLens.UI.ViewModels;

// ── WIFI EXPLORER VIEWMODEL ─────────────────────────────────────────────
public sealed partial class WifiExplorerViewModel : ObservableObject, IEventHandler<TelemetryCollectedEvent>
{
    private readonly IEventBus _eventBus;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] private string _ssid = "Not Connected";
    [ObservableProperty] private string _bssid = "—";
    [ObservableProperty] private string _rssi = "— dBm";
    [ObservableProperty] private string _channel = "—";
    [ObservableProperty] private string _signalQuality = "—%";

    public ObservableCollection<WifiNetworkItem> SurroundingNetworks { get; } = [];

    public WifiExplorerViewModel(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _eventBus.Subscribe<TelemetryCollectedEvent>(this);

        PopulateSurroundingNetworks();
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

            // Simulate minor real-time RSSI variations for surrounding networks
            var rand = new Random();
            foreach (var net in SurroundingNetworks)
            {
                var delta = rand.Next(-2, 3);
                net.Rssi = Math.Clamp(net.BaseRssi + delta, -95, -30);
            }
        });
        return Task.CompletedTask;
    }

    private void PopulateSurroundingNetworks()
    {
        SurroundingNetworks.Add(new WifiNetworkItem("Corporate-Secure", "00:11:22:33:44:55", -52, 6, "WPA3-Enterprise", "802.11ax (Wi-Fi 6)"));
        SurroundingNetworks.Add(new WifiNetworkItem("Guest-WiFi", "00:11:22:33:44:66", -65, 11, "WPA2-Personal", "802.11n (Wi-Fi 4)"));
        SurroundingNetworks.Add(new WifiNetworkItem("NetLens-Lab-5G", "AA:BB:CC:DD:EE:FF", -45, 36, "WPA3-Personal", "802.11ac (Wi-Fi 5)"));
        SurroundingNetworks.Add(new WifiNetworkItem("Home-Network", "AA:BB:CC:DD:EE:11", -78, 149, "WPA2-Personal", "802.11ac (Wi-Fi 5)"));
        SurroundingNetworks.Add(new WifiNetworkItem("Public-Hotspot", "55:44:33:22:11:00", -85, 1, "None (Open)", "802.11g"));
    }
}

public sealed partial class WifiNetworkItem : ObservableObject
{
    public string Ssid { get; }
    public string Bssid { get; }
    public int BaseRssi { get; }
    public int Channel { get; }
    public string Security { get; }
    public string Standards { get; }

    [ObservableProperty] private int _rssi;

    public WifiNetworkItem(string ssid, string bssid, int baseRssi, int channel, string security, string standards)
    {
        Ssid = ssid;
        Bssid = bssid;
        BaseRssi = baseRssi;
        _rssi = baseRssi;
        Channel = channel;
        Security = security;
        Standards = standards;
    }
}

// ── DIAGNOSTICS VIEWMODEL ───────────────────────────────────────────────
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly ITelemetryCollector _telemetryCollector;
    private readonly IRuleEngine _ruleEngine;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusMessage = "Ready to perform diagnostic scan.";
    [ObservableProperty] private int _healthScore = 100;
    
    public ObservableCollection<DiagnosticResult> Results { get; } = [];

    public ICommand RunDiagnosticCommand { get; }

    public DiagnosticsViewModel(ITelemetryCollector telemetryCollector, IRuleEngine ruleEngine)
    {
        _telemetryCollector = telemetryCollector;
        _ruleEngine = ruleEngine;
        RunDiagnosticCommand = new AsyncRelayCommand(RunDiagnosticAsync);
    }

    private async Task RunDiagnosticAsync()
    {
        IsRunning = true;
        StatusMessage = "Capturing network interface snapshots...";
        Results.Clear();

        try
        {
            var snapshot = await _telemetryCollector.CaptureSnapshotAsync(CancellationToken.None);
            if (snapshot == null)
            {
                StatusMessage = "Error: Wireless adapter is not connected or metrics are unavailable.";
                HealthScore = 0;
                return;
            }

            StatusMessage = "Analyzing telemetry rules...";
            var alerts = _ruleEngine.Evaluate(snapshot);

            int score = 100;
            foreach (var alert in alerts)
            {
                score -= alert.Severity == DiagnosticSeverity.Critical ? 25 : 10;
            }
            HealthScore = Math.Max(0, score);

            foreach (var alert in alerts)
            {
                Results.Add(alert);
            }

            StatusMessage = alerts.Count > 0 
                ? $"Scan complete: {alerts.Count} issue(s) detected." 
                : "Scan complete: All systems normal.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Diagnostic failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
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
