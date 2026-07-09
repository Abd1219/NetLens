namespace NetLens.Domain.Entities;

using NetLens.Domain.Model;

/// <summary>
/// An immutable snapshot of the wireless connection state captured at a single point in time.
/// This acts as the raw "frame" stored by the FlightRecordingLedger.
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

    // Network identity
    public string Ssid { get; }
    public MacAddress Bssid { get; }
    public string PhysicalType { get; }  // e.g. "802.11ax", "802.11ac"

    // Active probe results
    public Latency GatewayLatency { get; }
    public Latency DnsLatency { get; }
    public Latency InternetLatency { get; }
    public PacketLossRate PacketLoss { get; }
    public Jitter Jitter { get; }

    // Network configuration
    public IPAddressValue LocalIp { get; }
    public IPAddressValue GatewayIp { get; }
    public IPAddressValue DnsIp { get; }
    public MacAddress AdapterMac { get; }

    // System
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
        string ssid,
        MacAddress bssid,
        string physicalType,
        Latency gatewayLatency,
        Latency dnsLatency,
        Latency internetLatency,
        PacketLossRate packetLoss,
        Jitter jitter,
        IPAddressValue localIp,
        IPAddressValue gatewayIp,
        IPAddressValue dnsIp,
        MacAddress adapterMac,
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
        Ssid = ssid;
        Bssid = bssid;
        PhysicalType = physicalType;
        GatewayLatency = gatewayLatency;
        DnsLatency = dnsLatency;
        InternetLatency = internetLatency;
        PacketLoss = packetLoss;
        Jitter = jitter;
        LocalIp = localIp;
        GatewayIp = gatewayIp;
        DnsIp = dnsIp;
        AdapterMac = adapterMac;
        CpuUsagePercent = cpuUsagePercent;
        RamUsagePercent = ramUsagePercent;
    }
}
