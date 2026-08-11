namespace NetLens.Application.Correlation;

/// <summary>
/// Central repository for all thresholds, weights, and limits used by
/// NetworkCorrelationEngine and NetworkMetricsWindowBuilder.
///
/// DESIGN PRINCIPLE:
///   Every number here has a documented rationale. No magic constants
///   are scattered in rule logic. Changes to sensitivity require
///   modifying only this file.
///
/// SCORING SYSTEM:
///   Each correlation accumulates points from individual indicators.
///   Points are clamped to [0, 100]. The final EvidenceScore determines
///   EvidenceStrength (Weak / Moderate / Strong / VeryStrong).
///   Scores below MinReportableScore are suppressed entirely.
/// </summary>
public static class CorrelationThresholds
{
    // ── Window requirements ────────────────────────────────────────────────

    /// <summary>
    /// Minimum number of snapshots required to run correlation analysis.
    /// Fewer than this returns an empty result set rather than speculative findings.
    /// Rationale: at 3s intervals, 5 samples = 15 seconds minimum observation window.
    /// </summary>
    public const int MinSamplesRequired = 5;

    /// <summary>
    /// Minimum EvidenceScore to report a correlation result.
    /// Results scoring below this are considered insufficient evidence and suppressed.
    /// Corresponds to the bottom of the Moderate band.
    /// </summary>
    public const int MinReportableScore = 30;

    // ── RSSI thresholds ────────────────────────────────────────────────────

    /// <summary>
    /// Average RSSI below which the signal is considered problematic for correlation.
    /// Industry standard: below -70 dBm, 802.11 rate adaptation starts reducing MCS,
    /// causing higher jitter and packet loss. Aligns with LowRSSIRule warning (-75 dBm)
    /// but set slightly higher here to catch trending issues earlier.
    /// </summary>
    public const int RssiWarnThresholdDbm = -70;

    /// <summary>
    /// RSSI trend in dBm per minute that indicates a progressive decline.
    /// Negative values = declining. -1.0 dBm/min means the signal drops ~1 dBm each minute,
    /// which over 5-10 minutes represents a meaningful change from -60 to -70 dBm.
    /// </summary>
    public const double RssiTrendWarningDbmPerMinute = -1.0;

    /// <summary>
    /// Minimum RSSI swing (max - min in dBm) required to confirm a SignalDegradation trend.
    /// Prevents false positives from RSSI measurement noise (±2–3 dBm is normal).
    /// 8 dBm swing is clearly meaningful and not noise.
    /// </summary>
    public const int RssiMinSpreadDbm = 8;

    // ── LAN latency thresholds ─────────────────────────────────────────────

    /// <summary>
    /// Average gateway latency above which the LAN connection is considered stressed.
    /// Rationale: gateway latency > 30 ms on a wired/Wi-Fi LAN typically indicates
    /// congestion or RF retransmissions. Normal LAN ping to gateway is under 5 ms.
    /// </summary>
    public const double LanLatencyWarnMs = 30.0;

    /// <summary>
    /// Average gateway latency below which the LAN is considered "stable" for
    /// ExternalNetworkIssue detection. Must be clearly good to confirm the LAN is healthy.
    /// </summary>
    public const double LanLatencyStableMs = 30.0;

    // ── Jitter thresholds ──────────────────────────────────────────────────

    /// <summary>
    /// Average jitter above which the connection quality is degraded.
    /// Rationale: ITU-T G.114 specifies max 20 ms jitter for VoIP quality.
    /// Above 20 ms, packet reordering and audio artifacts become noticeable.
    /// </summary>
    public const double JitterWarnMs = 20.0;

    // ── Packet loss thresholds ────────────────────────────────────────────

    /// <summary>
    /// Average packet loss percentage above which the connection is impacted.
    /// Rationale: > 2% causes visible TCP retransmissions and stream quality drops.
    /// PacketLoss is the aggregate local probe; no WAN-specific loss metric exists.
    /// </summary>
    public const double PacketLossWarnPercent = 2.0;

    /// <summary>
    /// Fraction of window samples (0.0–1.0) that must show packet loss above the
    /// warning threshold for it to count as a persistent issue, not a transient spike.
    /// 0.20 = at least 20% of samples must be degraded.
    /// </summary>
    public const double PacketLossPersistenceThreshold = 0.20;

    // ── Internet latency thresholds ───────────────────────────────────────

    /// <summary>
    /// Average internet latency above which the external connection is considered degraded.
    /// Rationale: > 100 ms is perceptible to humans (reaction time threshold, ITU-T G.114).
    /// Normal internet latency to major CDN endpoints is typically 10–50 ms.
    /// </summary>
    public const double InternetLatencyWarnMs = 100.0;

    /// <summary>
    /// Average internet latency below which the internet path is considered stable.
    /// Used as an upper bound for the "WAN is OK" side of WifiInstability detection.
    /// Set higher than the "warn" value to create a hysteresis zone.
    /// </summary>
    public const double InternetLatencyStableMs = 80.0;

    /// <summary>
    /// Fraction of samples (0.0–1.0) in which internet probes timed out.
    /// If this fraction is high, it counts as internet connectivity degradation.
    /// </summary>
    public const double InternetTimeoutRatioWarn = 0.20;

    // ── NetworkInstability: minimum degraded sample ratio ─────────────────

    /// <summary>
    /// For NetworkInstability detection, at least this fraction of samples must show
    /// simultaneous LAN + internet degradation. Prevents a single-spike false positive.
    /// 0.30 = at least 30% of the window must be consistently degraded.
    /// </summary>
    public const double NetworkInstabilityPersistenceThreshold = 0.30;

    // ── PossibleCongestion ────────────────────────────────────────────────

    /// <summary>
    /// For PossibleCongestion, the LAN latency standard deviation must exceed this
    /// value to indicate variable (bursty) latency, which is a hallmark of congestion.
    /// </summary>
    public const double CongestionLatencyStdDevMs = 15.0;

    // ── Evidence score weights ────────────────────────────────────────────
    // These weights are added when the corresponding indicator is present.
    // Each correlation uses a subset of these weights, and total is clamped to 100.
    // The values are chosen so that "all indicators present" = ~100 for strong cases.

    // WifiInstability weights (total possible = 100)
    public const int W_Wifi_LowRssi         = 30; // RSSI below warn threshold
    public const int W_Wifi_HighJitter      = 25; // LAN jitter above warn threshold
    public const int W_Wifi_PacketLoss      = 25; // Packet loss above warn threshold
    public const int W_Wifi_PersistentLoss  = 10; // Packet loss is persistent (>20% of samples)
    public const int W_Wifi_InternetStable  = 10; // Internet latency is below stable threshold (confirming local cause)

    // ExternalNetworkIssue weights (total possible = 100)
    public const int W_Ext_LanStable          = 30; // LAN latency below stable threshold
    public const int W_Ext_InternetHigh       = 35; // Internet latency above warn threshold
    public const int W_Ext_InternetVeryHigh   = 15; // Internet latency > 2× warn threshold (extra severity weight)
    public const int W_Ext_InternetTimeouts   = 20; // Internet probe timeouts observed

    // NetworkInstability weights (total possible = 100)
    public const int W_Net_LanDegraded         = 30; // LAN latency above warn
    public const int W_Net_InternetDegraded    = 30; // Internet latency above warn
    public const int W_Net_JitterHigh          = 20; // Jitter above warn
    public const int W_Net_PacketLoss          = 20; // Packet loss above warn

    // SignalDegradation weights (total possible = 100)
    public const int W_Sig_TrendNegative       = 40; // Trend is below threshold (declining)
    public const int W_Sig_Spread              = 30; // RSSI spread >= MinSpreadDbm
    public const int W_Sig_TrendStrong         = 20; // Trend < 2× threshold (accelerating decline)
    public const int W_Sig_LowCurrentRssi     = 10; // Current average RSSI is below warn threshold

    // PossibleCongestion weights (total possible = 100)
    public const int W_Cong_LanLatencyHigh    = 25; // LAN latency above warn
    public const int W_Cong_InternetLatencyHigh = 25; // Internet latency above warn
    public const int W_Cong_JitterHigh        = 25; // Jitter above warn
    public const int W_Cong_LatencyVariance   = 25; // LAN latency std dev indicates bursty behavior
}
