namespace NetLens.Domain.Rules;

/// <summary>
/// Standard evidence key constants used across all diagnostic rules.
/// Prevents string literals from being scattered across rule implementations.
/// </summary>
public static class EvidenceKeys
{
    public const string Rssi               = "RSSI";
    public const string SignalQuality      = "SignalQuality";
    public const string TxRate             = "TxRate";
    public const string RxRate             = "RxRate";
    public const string Band               = "Band";
    public const string PhysicalType       = "PhysicalType";
    public const string Channel            = "Channel";
    public const string PacketLoss         = "PacketLoss";
    public const string GatewayLatency     = "GatewayLatency";
    public const string DnsLatency         = "DnsLatency";
    public const string InternetLatency    = "InternetLatency";
    public const string Jitter             = "Jitter";
    public const string Ssid               = "SSID";
    public const string Bssid              = "BSSID";
    public const string GatewayIp          = "GatewayIp";
    public const string DnsServer          = "DnsServer";
    public const string WarningThreshold   = "WarningThreshold";
    public const string CriticalThreshold  = "CriticalThreshold";
    public const string Reason             = "Reason";
    public const string ConnectionState    = "ConnectionState";
}
