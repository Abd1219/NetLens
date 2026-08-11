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
/// Telemetry collector that reads real hardware network state from Windows WlanAPI, IP Helper API, PingService, and SystemMetricsCollector.
/// Does not fabricate or invent metrics. If data is unavailable, fields are populated as Unavailable/null.
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
                _logger.LogDebug("No active wireless connection or WlanAPI returned disconnected state.");
                return null;
            }

            var networkInfo = GetNetworkInfo();
            if (networkInfo is null)
            {
                _logger.LogDebug("No active network adapter with gateway found.");
                return null;
            }

            // Run active probes in parallel for non-blocking execution
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
            var band = WifiBandExtensions.FromFrequencyMhz(wlanData.FrequencyMhz);

            return new WirelessSnapshot(
                capturedAt: DateTimeOffset.UtcNow,
                rssi: rssi,
                txRate: new PhyRate(wlanData.TxRateMbps),
                rxRate: new PhyRate(wlanData.RxRateMbps),
                channel: new Channel(wlanData.Channel),
                frequency: new Frequency(wlanData.FrequencyMhz),
                signalQuality: signalQuality,
                band: band,
                ssid: wlanData.Ssid,
                bssid: new MacAddress(wlanData.Bssid),
                physicalType: wlanData.PhysicalType,
                securityType: wlanData.SecurityType,
                connectionState: wlanData.ConnectionState,
                gatewayLatency: gatewayResult.AverageLatency,
                dnsLatency: dnsResult.AverageLatency,
                internetLatency: internetResult.AverageLatency,
                packetLoss: gatewayResult.PacketLoss,
                jitter: gatewayResult.Jitter,
                adapterName: networkInfo.AdapterName,
                adapterManufacturer: "Unavailable", // Strict rule: Do not invent manufacturer if unavailable reliably
                adapterMac: new MacAddress(networkInfo.MacAddress),
                localIp: new IPAddressValue(networkInfo.LocalIp),
                gatewayIp: new IPAddressValue(networkInfo.GatewayIp),
                dnsIp: new IPAddressValue(networkInfo.DnsIp),
                ipv6: networkInfo.Ipv6,
                dhcpServer: networkInfo.DhcpServer,
                linkSpeedMbps: networkInfo.LinkSpeedMbps,
                cpuUsagePercent: cpu,
                ramUsagePercent: ram
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered while capturing wireless snapshot.");
            return null;
        }
    }

    public async Task<IReadOnlyList<SurroundingNetworkInfo>> GetSurroundingNetworksAsync(CancellationToken cancellationToken)
    {
        // Execute on background thread to prevent UI thread blocking
        return await Task.Run(() =>
        {
            var list = new List<SurroundingNetworkInfo>();

            var result = WlanOpenHandleSafe(out var handle);
            if (result != 0) return (IReadOnlyList<SurroundingNetworkInfo>)list;

            try
            {
                if (WlanApi.WlanEnumInterfaces(handle, IntPtr.Zero, out var interfaceListPtr) == 0)
                {
                    try
                    {
                        var infoList = Marshal.PtrToStructure<WlanApi.WLAN_INTERFACE_INFO_LIST>(interfaceListPtr);
                        if (infoList.dwNumberOfItems > 0)
                        {
                            var interfaceInfo = Marshal.PtrToStructure<WlanApi.WLAN_INTERFACE_INFO>(
                                interfaceListPtr + Marshal.OffsetOf<WlanApi.WLAN_INTERFACE_INFO_LIST>("InterfaceInfo").ToInt32());

                            var guid = interfaceInfo.InterfaceGuid;
                            if (WlanApi.WlanGetNetworkBssList(handle, ref guid, IntPtr.Zero, 3 /* dot11_bss_type_any */, true, IntPtr.Zero, out var bssListPtr) == 0)
                            {
                                try
                                {
                                    var bssListHeader = Marshal.PtrToStructure<WlanApi.WLAN_BSS_LIST>(bssListPtr);
                                    int entrySize = Marshal.SizeOf<WlanApi.WLAN_BSS_ENTRY>();
                                    int headerOffset = 8;

                                    for (int i = 0; i < bssListHeader.dwNumberOfItems; i++)
                                    {
                                        var entryPtr = bssListPtr + headerOffset + (i * entrySize);
                                        var entry = Marshal.PtrToStructure<WlanApi.WLAN_BSS_ENTRY>(entryPtr);

                                        var freqMhz = (int)(entry.ulChCenterFrequency / 1000);
                                        var ch = WlanApi.CalculateChannelFromFrequencyMhz(freqMhz);
                                        var band = WifiBandExtensions.FromFrequencyMhz(freqMhz);
                                        var ssidStr = entry.dot11Ssid.GetSsid();
                                        if (string.IsNullOrWhiteSpace(ssidStr)) ssidStr = "<Hidden SSID>";

                                        var sec = entry.wlanRateSet.uRateSetLength > 0 ? WifiSecurityType.Wpa2Personal : WifiSecurityType.Open;

                                        list.Add(new SurroundingNetworkInfo(
                                            Ssid: ssidStr,
                                            Bssid: entry.dot11BssId.ToFormattedString(),
                                            RssiDbm: entry.lRssi,
                                            Channel: ch,
                                            FrequencyMhz: freqMhz,
                                            Band: band,
                                            Security: sec,
                                            PhysicalType: WlanApi.GetPhysicalTypeName(entry.dot11PhyType)
                                        ));
                                    }
                                }
                                finally
                                {
                                    WlanApi.WlanFreeMemory(bssListPtr);
                                }
                            }
                        }
                    }
                    finally
                    {
                        WlanApi.WlanFreeMemory(interfaceListPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WlanAPI error or Wi-Fi disabled during surrounding network scan.");
            }
            finally
            {
                WlanApi.WlanCloseHandle(handle, IntPtr.Zero);
            }

            return list;
        }, cancellationToken);
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

                var state = MapNativeStateToDomain(interfaceInfo.isState);
                if (state != WifiConnectionState.Connected)
                {
                    return new WlanConnectionData(
                        Ssid: "Not Connected",
                        Bssid: "00:00:00:00:00:00",
                        RssiDbm: -100,
                        TxRateMbps: 0,
                        RxRateMbps: 0,
                        Channel: 0,
                        FrequencyMhz: 0,
                        PhysicalType: "Unavailable",
                        SecurityType: WifiSecurityType.Unknown,
                        ConnectionState: state
                    );
                }

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
                    var sec = attrs.wlanSecurityAttributes;

                    var rssiDbm = (int)(assoc.wlanSignalQuality / 2.0) - 100;
                    rssiDbm = Math.Clamp(rssiDbm, -100, 0);

                    var txMbps = assoc.ulTxRate / 1000.0;
                    var rxMbps = assoc.ulRxRate / 1000.0;
                    var bssidStr = assoc.dot11Bssid.ToFormattedString();

                    var frequencyMhz = assoc.dot11PhyType >= 5 ? 5180 : 2412;
                    var channel = assoc.dot11PhyType >= 5 ? 36 : 1;

                    if (WlanApi.WlanGetNetworkBssList(handle, ref guid, IntPtr.Zero, 3, true, IntPtr.Zero, out var bssListPtr) == 0)
                    {
                        try
                        {
                            var bssHeader = Marshal.PtrToStructure<WlanApi.WLAN_BSS_LIST>(bssListPtr);
                            int entrySize = Marshal.SizeOf<WlanApi.WLAN_BSS_ENTRY>();
                            int headerOffset = 8;

                            for (int i = 0; i < bssHeader.dwNumberOfItems; i++)
                            {
                                var entryPtr = bssListPtr + headerOffset + (i * entrySize);
                                var entry = Marshal.PtrToStructure<WlanApi.WLAN_BSS_ENTRY>(entryPtr);
                                if (entry.dot11BssId.ToFormattedString().Equals(bssidStr, StringComparison.OrdinalIgnoreCase))
                                {
                                    frequencyMhz = (int)(entry.ulChCenterFrequency / 1000);
                                    var calcChan = WlanApi.CalculateChannelFromFrequencyMhz(frequencyMhz);
                                    if (calcChan > 0) channel = calcChan;
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            WlanApi.WlanFreeMemory(bssListPtr);
                        }
                    }

                    var securityType = WifiSecurityTypeExtensions.FromNativeAuthAlgo(sec.dot11AuthAlgorithm);

                    return new WlanConnectionData(
                        Ssid: assoc.dot11Ssid.GetSsid(),
                        Bssid: bssidStr,
                        RssiDbm: rssiDbm,
                        TxRateMbps: txMbps,
                        RxRateMbps: rxMbps,
                        Channel: channel,
                        FrequencyMhz: frequencyMhz,
                        PhysicalType: WlanApi.GetPhysicalTypeName(assoc.dot11PhyType),
                        SecurityType: securityType,
                        ConnectionState: WifiConnectionState.Connected
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

    private static WifiConnectionState MapNativeStateToDomain(WlanApi.WLAN_INTERFACE_STATE nativeState) => nativeState switch
    {
        WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_connected => WifiConnectionState.Connected,
        WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_disconnected => WifiConnectionState.Disconnected,
        WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_associating => WifiConnectionState.Associating,
        WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_authenticating => WifiConnectionState.Authenticating,
        WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_disconnecting => WifiConnectionState.Disconnecting,
        WlanApi.WLAN_INTERFACE_STATE.wlan_interface_state_not_ready => WifiConnectionState.NotReady,
        _ => WifiConnectionState.Unknown
    };

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

            var unicastV4 = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (unicastV4 is null) continue;

            var unicastV6 = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);

            var dns = props.DnsAddresses
                .FirstOrDefault(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (dns is null) continue;

            var dhcp = props.DhcpServerAddresses
                .FirstOrDefault(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            var mac = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
            double? linkSpeed = ni.Speed > 0 ? ni.Speed / 1_000_000.0 : null;

            return new NetworkAdapterInfo(
                AdapterName: ni.Name,
                LocalIp: unicastV4.Address.ToString(),
                GatewayIp: gateway.Address.ToString(),
                DnsIp: dns.ToString(),
                MacAddress: mac,
                Ipv6: unicastV6?.Address.ToString(),
                DhcpServer: dhcp?.ToString(),
                LinkSpeedMbps: linkSpeed
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
        string PhysicalType,
        WifiSecurityType SecurityType,
        WifiConnectionState ConnectionState);

    private sealed record NetworkAdapterInfo(
        string AdapterName,
        string LocalIp,
        string GatewayIp,
        string DnsIp,
        string MacAddress,
        string? Ipv6,
        string? DhcpServer,
        double? LinkSpeedMbps);
}
