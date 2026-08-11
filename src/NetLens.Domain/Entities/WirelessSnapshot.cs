namespace NetLens.Domain.Entities;

using NetLens.Domain.Model;

/// <summary>
/// An immutable snapshot of the wireless connection state and hardware adapter captured at a single point in time.
/// Strictly typed domain entity for telemetry recording.
/// </summary>
public sealed record WirelessSnapshot
{
    public DateTimeOffset CapturedAt { get; }

    // RF metrics
    public RSSI Rssi { get; }
    public PhyRate TxRate { get; }
    public PhyRate RxRate { get; }
    public Channel Channel { get; }
    public Frequency Frequency { get; }
    public SignalQuality SignalQuality { get; }
    public WifiBand Band { get; }

    // Network identity & Security
    public string Ssid { get; }
    public MacAddress Bssid { get; }
    public string PhysicalType { get; }
    public WifiSecurityType SecurityType { get; }
    public WifiConnectionState ConnectionState { get; }

    // Active probe results
    public Latency GatewayLatency { get; }
    public Latency DnsLatency { get; }
    public Latency InternetLatency { get; }
    public PacketLossRate PacketLoss { get; }
    public Jitter Jitter { get; }

    // Network adapter & IP configuration
    public string AdapterName { get; }
    public string AdapterManufacturer { get; }
    public MacAddress AdapterMac { get; }
    public IPAddressValue LocalIp { get; }
    public IPAddressValue GatewayIp { get; }
    public IPAddressValue DnsIp { get; }
    public string? Ipv6 { get; }
    public string? DhcpServer { get; }
    public double? LinkSpeedMbps { get; }

    // System resource metrics
    public double CpuUsagePercent { get; }
    public double RamUsagePercent { get; }

    public WirelessSnapshot(
        DateTimeOffset capturedAt,
        RSSI rssi,
        PhyRate txRate,
        PhyRate rxRate,
        Channel channel,
        Frequency frequency,
        SignalQuality signalQuality,
        WifiBand band,
        string ssid,
        MacAddress bssid,
        string physicalType,
        WifiSecurityType securityType,
        WifiConnectionState connectionState,
        Latency gatewayLatency,
        Latency dnsLatency,
        Latency internetLatency,
        PacketLossRate packetLoss,
        Jitter jitter,
        string adapterName,
        string adapterManufacturer,
        MacAddress adapterMac,
        IPAddressValue localIp,
        IPAddressValue gatewayIp,
        IPAddressValue dnsIp,
        string? ipv6,
        string? dhcpServer,
        double? linkSpeedMbps,
        double cpuUsagePercent,
        double ramUsagePercent)
    {
        CapturedAt = capturedAt;
        Rssi = rssi;
        TxRate = txRate;
        RxRate = rxRate;
        Channel = channel;
        Frequency = frequency;
        SignalQuality = signalQuality;
        Band = band;
        Ssid = ssid;
        Bssid = bssid;
        PhysicalType = physicalType;
        SecurityType = securityType;
        ConnectionState = connectionState;
        GatewayLatency = gatewayLatency;
        DnsLatency = dnsLatency;
        InternetLatency = internetLatency;
        PacketLoss = packetLoss;
        Jitter = jitter;
        AdapterName = adapterName;
        AdapterManufacturer = adapterManufacturer;
        AdapterMac = adapterMac;
        LocalIp = localIp;
        GatewayIp = gatewayIp;
        DnsIp = dnsIp;
        Ipv6 = ipv6;
        DhcpServer = dhcpServer;
        LinkSpeedMbps = linkSpeedMbps;
        CpuUsagePercent = cpuUsagePercent;
        RamUsagePercent = ramUsagePercent;
    }
}
