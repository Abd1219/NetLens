namespace NetLens.Domain.Correlation;

/// <summary>
/// Standard evidence key constants for correlation results.
/// Keeps string literals out of engine implementation code.
/// Prefixed with "Corr_" to distinguish from DiagnosticResult EvidenceKeys.
/// </summary>
public static class CorrelationEvidenceKeys
{
    // LAN-side
    public const string LanLatencyAvg      = "LanLatencyAvg_ms";
    public const string LanLatencyStdDev   = "LanLatencyStdDev_ms";
    public const string LanJitterAvg       = "LanJitterAvg_ms";
    public const string LanJitterMax       = "LanJitterMax_ms";
    public const string LanPacketLossAvg   = "LanPacketLoss_pct";
    public const string LanPacketLossPersistence = "LanPacketLossPersistence_pct";

    // Internet/WAN-side
    public const string InternetLatencyAvg    = "InternetLatencyAvg_ms";
    public const string InternetLatencyStdDev = "InternetLatencyStdDev_ms";
    public const string InternetTimeoutRatio  = "InternetTimeoutRatio_pct";

    // Wi-Fi RF
    public const string RssiAvg       = "RssiAvg_dBm";
    public const string RssiMin       = "RssiMin_dBm";
    public const string RssiMax       = "RssiMax_dBm";
    public const string RssiTrend     = "RssiTrend_dBm_per_min";
    public const string PhyRateAvg    = "PhyRateAvg_Mbps";
    public const string PhyRateMin    = "PhyRateMin_Mbps";

    // Gateway / DNS
    public const string GatewayLatencyAvg = "GatewayLatencyAvg_ms";
    public const string DnsLatencyAvg     = "DnsLatencyAvg_ms";
    public const string DnsTimeoutRatio   = "DnsTimeoutRatio_pct";

    // Scoring metadata
    public const string EvidenceScore   = "EvidenceScore";
    public const string SampleCount     = "SampleCount";
    public const string WindowDuration  = "WindowDuration_s";
}
