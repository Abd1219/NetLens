using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Model;
using NetLens.Network.Adapters;
using NetLens.Network.Diagnostics;

namespace NetLens.Network.Wifi;

/// <summary>
/// Captures a complete WirelessSnapshot by combining:
/// - WlanAPI (RSSI, PHY Rate, SSID, BSSID, Channel, Frequency)
/// - IP Helper API (Gateway IP, DNS IP, Local IP, MAC)
/// - PingService (Gateway/DNS/Internet latency, Packet Loss, Jitter)
/// - SystemMetricsCollector (CPU, RAM)
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WifiTelemetryCollector : ITelemetryCollector
{
    private readonly PingService _pingService;
    private readonly SystemMetricsCollector _systemMetrics;
    private readonly ILogger<WifiTelemetryCollector> _logger;

    public WifiTelemetryCollector(
        PingService pingService,
        SystemMetricsCollector systemMetrics,
        ILogger<WifiTelemetryCollector> logger)
    {
        _pingService = pingService;
        _systemMetrics = systemMetrics;
        _logger = logger;
    }

    public async Task<WirelessSnapshot?> CaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var wlanData = GetWlanConnectionAttributes();
            if (wlanData is null)
            {
                _logger.LogDebug("No active WiFi connection found.");
                return null;
            }

            var networkInfo = GetNetworkInfo();
            if (networkInfo is null)
            {
                _logger.LogDebug("No active network adapter with gateway found.");
                return null;
            }

            // Run active probes in parallel for performance
            var gatewayPingTask = _pingService.PingAsync(networkInfo.GatewayIp, 5, cancellationToken);
            var dnsPingTask = _pingService.PingAsync(networkInfo.DnsIp, 5, cancellationToken);
            var internetPingTask = _pingService.PingAsync("8.8.8.8", 5, cancellationToken);

            await Task.WhenAll(gatewayPingTask, dnsPingTask, internetPingTask);

            var gatewayResult = await gatewayPingTask;
            var dnsResult = await dnsPingTask;
            var internetResult = await internetPingTask;

            var (cpu, ram) = _systemMetrics.GetCurrentUsage();

            var rssi = new RSSI(wlanData.RssiDbm);
            var signalQuality = SignalQuality.FromRssi(rssi);

            return new WirelessSnapshot(
                capturedAt: DateTimeOffset.UtcNow,
                rssi: rssi,
                txRate: new PhyRate(wlanData.TxRateMbps),
                rxRate: new PhyRate(wlanData.RxRateMbps),
                channel: new Channel(wlanData.Channel),
                frequency: new Frequency(wlanData.FrequencyMhz),
                signalQuality: signalQuality,
                ssid: wlanData.Ssid,
                bssid: new MacAddress(wlanData.Bssid),
                physicalType: wlanData.PhysicalType,
                gatewayLatency: gatewayResult.AverageLatency,
                dnsLatency: dnsResult.AverageLatency,
                internetLatency: internetResult.AverageLatency,
                packetLoss: gatewayResult.PacketLoss,
                jitter: gatewayResult.Jitter,
                localIp: new IPAddressValue(networkInfo.LocalIp),
                gatewayIp: new IPAddressValue(networkInfo.GatewayIp),
                dnsIp: new IPAddressValue(networkInfo.DnsIp),
                adapterMac: new MacAddress(networkInfo.MacAddress),
                cpuUsagePercent: cpu,
                ramUsagePercent: ram
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture wireless snapshot.");
            return null;
        }
    }

    private WlanConnectionData? GetWlanConnectionAttributes()
    {
        var result = WlanOpenHandleSafe(out var handle);
        if (result != 0) return null;

        try
        {
            var interfaceListPtr = IntPtr.Zero;
            if (WlanApi.WlanEnumInterfaces(handle, IntPtr.Zero, out interfaceListPtr) != 0)
                return null;

            try
            {
                var infoList = Marshal.PtrToStructure<WlanApi.WLAN_INTERFACE_INFO_LIST>(interfaceListPtr);
                if (infoList.dwNumberOfItems == 0) return null;

                var interfaceInfo = Marshal.PtrToStructure<WlanApi.WLAN_INTERFACE_INFO>(
                    interfaceListPtr + Marshal.OffsetOf<WlanApi.WLAN_INTERFACE_INFO_LIST>("InterfaceInfo").ToInt32());

                if (interfaceInfo.isState != WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_connected)
                    return null;

                var guid = interfaceInfo.InterfaceGuid;
                var dataPtr = IntPtr.Zero;
                var opcode = WlanApi.WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection;

                if (WlanApi.WlanQueryInterface(handle, ref guid, opcode, IntPtr.Zero,
                        out _, ref dataPtr, IntPtr.Zero) != 0)
                    return null;

                try
                {
                    var attrs = Marshal.PtrToStructure<WlanApi.WLAN_CONNECTION_ATTRIBUTES>(dataPtr);
                    var assoc = attrs.wlanAssociationAttributes;

                    // Windows reports signal quality 0-100; convert back to dBm via inverse formula
                    var rssiDbm = (int)(assoc.wlanSignalQuality / 2.0) - 100;
                    rssiDbm = Math.Clamp(rssiDbm, -100, 0);

                    // Rates are in kbps from WlanAPI
                    var txMbps = assoc.ulTxRate / 1000.0;
                    var rxMbps = assoc.ulRxRate / 1000.0;

                    // Channel and frequency are not directly available in WLAN_ASSOCIATION_ATTRIBUTES
                    // We use a best-effort derivation from the PHY index (simplified for v0.5)
                    var frequencyMhz = assoc.dot11PhyType >= 5 ? 5180 : 2412; // 5GHz vs 2.4GHz heuristic
                    var channel = assoc.dot11PhyType >= 5 ? 36 : 1;

                    return new WlanConnectionData(
                        Ssid: assoc.dot11Ssid.GetSsid(),
                        Bssid: assoc.dot11Bssid.ToFormattedString(),
                        RssiDbm: rssiDbm,
                        TxRateMbps: txMbps,
                        RxRateMbps: rxMbps,
                        Channel: channel,
                        FrequencyMhz: frequencyMhz,
                        PhysicalType: WlanApi.GetPhysicalTypeName(assoc.dot11PhyType)
                    );
                }
                finally
                {
                    WlanApi.WlanFreeMemory(dataPtr);
                }
            }
            finally
            {
                WlanApi.WlanFreeMemory(interfaceListPtr);
            }
        }
        finally
        {
            WlanApi.WlanCloseHandle(handle, IntPtr.Zero);
        }
    }

    private static uint WlanOpenHandleSafe(out IntPtr handle)
    {
        return WlanApi.WlanOpenHandle(2, IntPtr.Zero, out _, out handle);
    }

    private static NetworkAdapterInfo? GetNetworkInfo()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = ni.GetIPProperties();
            var gateway = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (gateway is null) continue;

            var unicast = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (unicast is null) continue;

            var dns = props.DnsAddresses
                .FirstOrDefault(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (dns is null) continue;

            var mac = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

            return new NetworkAdapterInfo(
                LocalIp: unicast.Address.ToString(),
                GatewayIp: gateway.Address.ToString(),
                DnsIp: dns.ToString(),
                MacAddress: mac
            );
        }

        return null;
    }

    private sealed record WlanConnectionData(
        string Ssid,
        string Bssid,
        int RssiDbm,
        double TxRateMbps,
        double RxRateMbps,
        int Channel,
        int FrequencyMhz,
        string PhysicalType);

    private sealed record NetworkAdapterInfo(
        string LocalIp,
        string GatewayIp,
        string DnsIp,
        string MacAddress);
}
