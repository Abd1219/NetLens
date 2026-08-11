using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Correlation;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Model;
using NetLens.Domain.Rules;
using NetLens.UI.Models;
using NetLens.UI.Services;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NetLens.UI.ViewModels;

/// <summary>
/// ViewModel for the main Dashboard. Subscribes to DiagnosticCompletedEvent
/// via the IEventBus, updating observable properties that drive the WinUI 3 
/// bindings in real-time.
///
/// All updates are dispatched back to the UI thread via DispatcherQueue.
/// No logic beyond presentation transformation lives here.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject,
    IEventHandler<DiagnosticCompletedEvent>
{
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly LocalizationService _localizationService;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

    private const int MaxChartPoints = 60; // 3 minutes at 3s intervals
    private IReadOnlyList<DiagnosticResult> _lastAlerts = [];

    // ── Signal & Rate ────────────────────────────────────────────────
    [ObservableProperty] private string _rssi = "— dBm";
    [ObservableProperty] private string _signalQuality = "—%";
    [ObservableProperty] private string _phyTxRate = "— Mbps";
    [ObservableProperty] private string _phyRxRate = "— Mbps";
    [ObservableProperty] private string _physicalType = "—";
    [ObservableProperty] private string _channel = "—";
    [ObservableProperty] private string _frequency = "—";
    [ObservableProperty] private string _ssid = "Not Connected";
    [ObservableProperty] private string _bssid = "—";
    [ObservableProperty] private string _band = "—";
    [ObservableProperty] private string _securityType = "—";
    [ObservableProperty] private string _connectionState = "—";

    // ── Connectivity & Adapter ────────────────────────────────────────
    [ObservableProperty] private string _gatewayLatency = "—";
    [ObservableProperty] private string _dnsLatency = "—";
    [ObservableProperty] private string _internetLatency = "—";
    [ObservableProperty] private string _packetLoss = "—%";
    [ObservableProperty] private string _jitter = "—";
    [ObservableProperty] private string _localIp = "—";
    [ObservableProperty] private string _gatewayIp = "—";
    [ObservableProperty] private string _dnsIp = "—";
    [ObservableProperty] private string _adapterName = "—";
    [ObservableProperty] private string _adapterManufacturer = "Unavailable";
    [ObservableProperty] private string _ipv6 = "—";
    [ObservableProperty] private string _dhcpServer = "—";
    [ObservableProperty] private string _linkSpeed = "—";

    // ── System ───────────────────────────────────────────────────────
    [ObservableProperty] private string _cpu = "—%";
    [ObservableProperty] private string _ram = "—%";
    [ObservableProperty] private double _cpuValue;
    [ObservableProperty] private double _ramValue;

    // ── Health & Status ──────────────────────────────────────────────
    [ObservableProperty] private string _overallStatus = "Initializing";
    [ObservableProperty] private string _statusColor = "#888888";
    [ObservableProperty] private bool _isConnected;

    // ── Active Alerts & Correlations ──────────────────────────────────
    public ObservableCollection<DiagnosticResultDisplay> ActiveAlerts { get; } = [];
    public ObservableCollection<CorrelationResultDisplay> ActiveCorrelations { get; } = [];

    [ObservableProperty] private string _correlationSectionTitle = string.Empty;
    [ObservableProperty] private string _noCorrelationsMessage = string.Empty;

    // ── LiveCharts2 Series ───────────────────────────────────────────
    private readonly ObservableCollection<double> _rssiValues = [];
    private readonly ObservableCollection<double> _latencyValues = [];
    private readonly ObservableCollection<double> _packetLossValues = [];

    public ISeries[] RssiSeries { get; }
    public ISeries[] LatencySeries { get; }
    public ISeries[] PacketLossSeries { get; }

    public Axis[] TimeAxis { get; } =
    [
        new Axis { IsVisible = false }
    ];

    public Axis[] RssiYAxis { get; } =
    [
        new Axis
        {
            MinLimit = -100,
            MaxLimit = 0,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#888888")),
            Labeler = v => $"{v:N0} dBm"
        }
    ];

    public Axis[] LatencyYAxis { get; } =
    [
        new Axis
        {
            MinLimit = 0,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#888888")),
            Labeler = v => $"{v:N0} ms"
        }
    ];

    private readonly INetworkCorrelationEngine _correlationEngine;
    private readonly INetworkMetricsWindowBuilder _windowBuilder;
    private readonly List<WirelessSnapshot> _correlationSnapshots = [];

    public DashboardViewModel(
        IEventBus eventBus,
        ILogger<DashboardViewModel> logger,
        INetworkCorrelationEngine correlationEngine,
        INetworkMetricsWindowBuilder windowBuilder)
    {
        _logger = logger;
        _correlationEngine = correlationEngine;
        _windowBuilder = windowBuilder;
        _localizationService = App.Services.GetRequiredService<LocalizationService>();
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _localizationService.LanguageChanged += OnLanguageChanged;

        // Subscribe to diagnostic results processed by DiagnosticService
        eventBus.Subscribe<DiagnosticCompletedEvent>(this);

        // Initialize chart series
        RssiSeries =
        [
            new LineSeries<double>
            {
                Values = _rssiValues,
                Name = "RSSI (dBm)",
                Stroke = new SolidColorPaint(SKColor.Parse("#00B7C3"), 2),
                Fill = new LinearGradientPaint(
                    [SKColor.Parse("#1A00B7C3"), SKColor.Parse("#0000B7C3")],
                    new SKPoint(0, 0), new SKPoint(0, 1)),
                GeometrySize = 0,
                LineSmoothness = 0.4
            }
        ];

        LatencySeries =
        [
            new LineSeries<double>
            {
                Values = _latencyValues,
                Name = "Gateway Latency (ms)",
                Stroke = new SolidColorPaint(SKColor.Parse("#0078D4"), 2),
                Fill = new LinearGradientPaint(
                    [SKColor.Parse("#1A0078D4"), SKColor.Parse("#000078D4")],
                    new SKPoint(0, 0), new SKPoint(0, 1)),
                GeometrySize = 0,
                LineSmoothness = 0.4
            }
        ];

        PacketLossSeries =
        [
            new LineSeries<double>
            {
                Values = _packetLossValues,
                Name = "Packet Loss (%)",
                Stroke = new SolidColorPaint(SKColor.Parse("#C42B1C"), 2),
                Fill = new LinearGradientPaint(
                    [SKColor.Parse("#1AC42B1C"), SKColor.Parse("#00C42B1C")],
                    new SKPoint(0, 0), new SKPoint(0, 1)),
                GeometrySize = 0,
                LineSmoothness = 0.2
            }
        ];

        UpdateOverallStatusText(_lastAlerts);
    }

    private void OnLanguageChanged()
    {
        _dispatcher.TryEnqueue(() => UpdateOverallStatusText(_lastAlerts));
    }

    /// <summary>
    /// Handles incoming DiagnosticCompletedEvent. Updates all observable telemetry metrics
    /// and active diagnostic alerts on the UI thread.
    /// ZERO rule evaluation happens here.
    /// </summary>
    public Task HandleAsync(DiagnosticCompletedEvent @event, CancellationToken cancellationToken)
    {
        var snapshot = @event.Snapshot;
        var alerts = @event.Results;

        _dispatcher.TryEnqueue(() => UpdateFromSnapshot(snapshot, alerts));

        return Task.CompletedTask;
    }

    private void UpdateFromSnapshot(WirelessSnapshot s, IReadOnlyList<DiagnosticResult> alerts)
    {
        _lastAlerts = alerts;

        // RF & Connection metrics
        Rssi = s.Rssi.ToString();
        SignalQuality = s.SignalQuality.ToString();
        PhyTxRate = s.TxRate.ToString();
        PhyRxRate = s.RxRate.ToString();
        PhysicalType = s.PhysicalType;
        Channel = s.Channel.ToString();
        Frequency = s.Frequency.ToString();
        Band = s.Band.ToDisplayString();
        SecurityType = s.SecurityType.ToDisplayString();
        ConnectionState = s.ConnectionState.ToDisplayString();
        Ssid = s.Ssid;
        Bssid = s.Bssid.Value;

        // Connectivity & Adapter Details
        GatewayLatency = s.GatewayLatency.ToString();
        DnsLatency = s.DnsLatency.ToString();
        InternetLatency = s.InternetLatency.ToString();
        PacketLoss = s.PacketLoss.ToString();
        Jitter = s.Jitter.ToString();
        LocalIp = s.LocalIp.Value;
        GatewayIp = s.GatewayIp.Value;
        DnsIp = s.DnsIp.Value;
        AdapterName = string.IsNullOrWhiteSpace(s.AdapterName) ? "Unavailable" : s.AdapterName;
        AdapterManufacturer = s.AdapterManufacturer;
        Ipv6 = s.Ipv6 ?? "N/A";
        DhcpServer = s.DhcpServer ?? "N/A";
        LinkSpeed = s.LinkSpeedMbps.HasValue ? $"{s.LinkSpeedMbps.Value:N0} Mbps" : "N/A";

        // System
        CpuValue = s.CpuUsagePercent;
        Cpu = $"{s.CpuUsagePercent:N1}%";
        RamValue = s.RamUsagePercent;
        Ram = $"{s.RamUsagePercent:N1}%";
        IsConnected = s.ConnectionState == WifiConnectionState.Connected;

        // Update charts (maintain rolling window)
        AddChartPoint(_rssiValues, s.Rssi.Value);
        AddChartPoint(_latencyValues, s.GatewayLatency.IsTimeout ? 0 : s.GatewayLatency.Milliseconds);
        AddChartPoint(_packetLossValues, s.PacketLoss.Percentage);

        // Maintain correlation sliding window of 20 snapshots
        _correlationSnapshots.Add(s);
        if (_correlationSnapshots.Count > 20)
        {
            _correlationSnapshots.RemoveAt(0);
        }

        // Run Correlation Engine
        var metricsWindow = _windowBuilder.Build(_correlationSnapshots);
        var correlations = _correlationEngine.Analyze(metricsWindow);

        ActiveCorrelations.Clear();
        foreach (var corr in correlations)
        {
            ActiveCorrelations.Add(new CorrelationResultDisplay(corr, _localizationService));
        }

        // Update active alerts wrapped for dynamic localization
        ActiveAlerts.Clear();
        foreach (var alert in alerts)
            ActiveAlerts.Add(new DiagnosticResultDisplay(alert, _localizationService));

        // Update overall status text
        UpdateOverallStatusText(alerts);
    }

    private void UpdateOverallStatusText(IReadOnlyList<DiagnosticResult> alerts)
    {
        CorrelationSectionTitle = _localizationService.GetString("Corr_SectionTitle");
        NoCorrelationsMessage = _localizationService.GetString("Corr_NoCorrelations");

        // Refresh existing active correlations translation
        foreach (var display in ActiveCorrelations)
        {
            display.RefreshLocalization();
        }

        if (alerts.Count == 0)
        {
            OverallStatus = _localizationService.GetString("Status_AllNormal");
            StatusColor = "#107C10";
        }
        else if (alerts.Any(a => a.Severity == DiagnosticSeverity.Critical))
        {
            var count = alerts.Count(a => a.Severity == DiagnosticSeverity.Critical);
            OverallStatus = string.Format(_localizationService.GetString("Status_CriticalIssues"), count);
            StatusColor = "#C42B1C";
        }
        else
        {
            OverallStatus = string.Format(_localizationService.GetString("Status_Warnings"), alerts.Count);
            StatusColor = "#9D5D00";
        }
    }

    private static void AddChartPoint(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        if (series.Count > MaxChartPoints)
            series.RemoveAt(0);
    }
}
