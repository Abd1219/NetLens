using System;
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
using NetLens.Network.Discovery;
using NetLens.Network.Diagnostics;

namespace NetLens.UI.ViewModels;

/// <summary>
/// Flat row ViewModel for displaying a discovered device in the ListView.
/// Uses plain strings to avoid XAML compiler issues with value objects.
/// </summary>
public sealed class DeviceRowViewModel
{
    public string IpAddress { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string Hostname { get; init; } = "Unknown";
    public string LatencyMs { get; init; } = string.Empty;
    public string DeviceType { get; init; } = string.Empty;
    public ICommand? TraceCommand { get; init; }

    public static DeviceRowViewModel From(DiscoveredDevice device, ICommand traceCommand)
        => new()
        {
            IpAddress = device.IpAddress.Value,
            MacAddress = device.MacAddress.Value,
            Hostname = device.Hostname ?? "—",
            LatencyMs = $"{device.ResponseTime.Milliseconds:F1} ms",
            DeviceType = device.DeviceType,
            TraceCommand = traceCommand
        };
}

/// <summary>
/// Flat row ViewModel for displaying a traceroute hop in the ListView.
/// </summary>
public sealed class HopRowViewModel
{
    public string HopNumber { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string AverageLatency { get; init; } = string.Empty;

    public static HopRowViewModel From(TracerouteHop hop)
        => new()
        {
            HopNumber = hop.HopNumber.ToString(),
            IpAddress = hop.IpAddress,
            Hostname = hop.Hostname ?? "Unknown host",
            AverageLatency = $"{hop.AverageLatency.Milliseconds:F1} ms"
        };
}

/// <summary>
/// ViewModel driving the subnet discovery and traceroute diagnostic page.
/// </summary>
public sealed partial class DiscoveryViewModel : ObservableObject, IEventHandler<DeviceDiscoveredEvent>
{
    private readonly SubnetScanner _subnetScanner;
    private readonly TracerouteService _tracerouteService;
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private int _progressPercentage;
    [ObservableProperty] private string _statusMessage = "Ready to scan subnet.";
    [ObservableProperty] private string _subnetRange = "—";

    /// <summary>Flat device rows for the ListView (plain string properties, {x:Bind} safe).</summary>
    public ObservableCollection<DeviceRowViewModel> DeviceRows { get; } = [];

    /// <summary>Flat hop rows for the traceroute ListView.</summary>
    public ObservableCollection<HopRowViewModel> TracerouteHops { get; } = [];

    [ObservableProperty] private DeviceRowViewModel? _selectedDevice;

    [ObservableProperty] private bool _isTracerouting;
    [ObservableProperty] private string _tracerouteTarget = "";
    [ObservableProperty] private string _tracerouteStatusMessage = "Select a device to trace route";
    [ObservableProperty] private string _tracerouteHeaderMessage = "Route Path Analysis";

    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand RunTracerouteCommand { get; }

    public DiscoveryViewModel(
        SubnetScanner subnetScanner,
        TracerouteService tracerouteService,
        IEventBus eventBus)
    {
        _subnetScanner = subnetScanner;
        _tracerouteService = tracerouteService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        eventBus.Subscribe<DeviceDiscoveredEvent>(this);

        RunTracerouteCommand = new AsyncRelayCommand<string>(RunTracerouteAsync);
        StartScanCommand = new AsyncRelayCommand(StartScanAsync);
        CancelScanCommand = new RelayCommand(CancelScan);

        UpdateSubnetRange();
    }

    private void UpdateSubnetRange()
    {
        var ip = GetLocalIpAddress();
        if (System.Net.IPAddress.TryParse(ip, out var parsedIp))
        {
            var bytes = parsedIp.GetAddressBytes();
            SubnetRange = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24";
        }
        else
        {
            SubnetRange = "Unknown";
        }
    }

    private async Task StartScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        ProgressPercentage = 0;
        StatusMessage = "Initializing subnet scan...";
        DeviceRows.Clear();
        _scanCts = new CancellationTokenSource();

        try
        {
            var localIp = GetLocalIpAddress();
            StatusMessage = $"Scanning range {SubnetRange} via {localIp}...";

            var progress = new Progress<int>(p =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    ProgressPercentage = p;
                    StatusMessage = $"Scanning subnet... {p}%";
                });
            });

            var result = await _subnetScanner.ScanSubnetAsync(localIp, progress, _scanCts.Token);
            StatusMessage = $"Scan completed. Found {result.Count} devices.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void CancelScan()
    {
        _scanCts?.Cancel();
    }

    private async Task RunTracerouteAsync(string? targetIp)
    {
        if (string.IsNullOrWhiteSpace(targetIp)) return;

        IsTracerouting = true;
        TracerouteTarget = targetIp;
        TracerouteStatusMessage = $"Tracing route to {targetIp}...";
        TracerouteHeaderMessage = $"Route path to {targetIp}";
        TracerouteHops.Clear();

        try
        {
            var hops = await _tracerouteService.TraceAsync(targetIp, maxHops: 30, CancellationToken.None);
            foreach (var hop in hops)
            {
                TracerouteHops.Add(HopRowViewModel.From(hop));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Traceroute failed: {ex.Message}");
        }
        finally
        {
            IsTracerouting = false;
        }
    }

    public Task HandleAsync(DeviceDiscoveredEvent @event, CancellationToken cancellationToken)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (!DeviceRows.Any(d => d.IpAddress == @event.Device.IpAddress.Value))
            {
                DeviceRows.Add(DeviceRowViewModel.From(@event.Device, RunTracerouteCommand));
            }
        });
        return Task.CompletedTask;
    }

    private string GetLocalIpAddress()
    {
        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

            var props = ni.GetIPProperties();
            var gateway = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (gateway is null) continue;

            var unicast = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (unicast is null) continue;

            return unicast.Address.ToString();
        }

        return "127.0.0.1";
    }
}
