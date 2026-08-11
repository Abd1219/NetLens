namespace NetLens.Domain.Correlation;

/// <summary>
/// Summarized aggregate metrics computed from a rolling window of WirelessSnapshot records.
///
/// METRIC MAPPING — which WirelessSnapshot fields feed each group:
///
///   LAN-side metrics (local network, between this device and the gateway):
///     Source fields: GatewayLatency, Jitter, PacketLoss
///     These reflect conditions on the Wi-Fi + LAN segment up to the gateway.
///
///   Internet/WAN-side metrics (beyond the gateway, external path):
///     Source field: InternetLatency only.
///     NOTE: WirelessSnapshot provides a single PacketLoss measurement
///     (local/aggregate). There is NO separate WAN packet loss field.
///     Do NOT fabricate a WAN-specific packet loss from other fields.
///
///   Wi-Fi RF metrics:
///     Source fields: Rssi, TxRate, RxRate
///
///   Gateway / DNS:
///     Source fields: GatewayLatency, DnsLatency (when DnsLatency.IsTimeout == false)
///
/// Nullable fields indicate that a metric could not be computed —
/// typically because the window contained too few valid samples or
/// the underlying probe timed out consistently.
/// </summary>
public sealed record NetworkMetricsWindow
{
    // ── Window metadata ────────────────────────────────────────────────────

    /// <summary>Number of WirelessSnapshot records this window was built from.</summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Approximate duration covered by the window in seconds,
    /// calculated from the difference between the first and last snapshot timestamps.
    /// </summary>
    public double WindowDurationSeconds { get; init; }

    // ── LAN-side metrics (GatewayLatency, Jitter, PacketLoss) ─────────────

    /// <summary>Mean of GatewayLatency.Milliseconds across non-timeout samples. Null if all samples timed out.</summary>
    public double? LanLatencyAverageMs { get; init; }

    /// <summary>Minimum GatewayLatency.Milliseconds seen in the window.</summary>
    public double? LanLatencyMinMs { get; init; }

    /// <summary>Maximum GatewayLatency.Milliseconds seen in the window.</summary>
    public double? LanLatencyMaxMs { get; init; }

    /// <summary>Population standard deviation of GatewayLatency.Milliseconds. Low StdDev = stable LAN.</summary>
    public double? LanLatencyStdDevMs { get; init; }

    /// <summary>Mean of Jitter.Milliseconds across the window.</summary>
    public double? LanJitterAverageMs { get; init; }

    /// <summary>Maximum Jitter.Milliseconds observed in the window.</summary>
    public double? LanJitterMaxMs { get; init; }

    /// <summary>
    /// Mean of PacketLoss.Percentage across the window.
    /// This is the single aggregate packet loss measured by the telemetry probe.
    /// It includes both local Wi-Fi and LAN losses; no WAN-specific separation is available.
    /// </summary>
    public double? LanPacketLossPercent { get; init; }

    /// <summary>
    /// Fraction of samples (0.0–1.0) in which PacketLoss.Percentage exceeded the
    /// configured warning threshold. Used to distinguish transient spikes from sustained loss.
    /// </summary>
    public double? LanPacketLossPersistenceRatio { get; init; }

    // ── Internet/WAN-side metrics (InternetLatency only) ──────────────────
    // NOTE: No WAN packet loss field exists in WirelessSnapshot. Do not add one here.

    /// <summary>Mean of InternetLatency.Milliseconds across non-timeout samples. Null if all timed out.</summary>
    public double? InternetLatencyAverageMs { get; init; }

    /// <summary>Minimum InternetLatency.Milliseconds seen in the window.</summary>
    public double? InternetLatencyMinMs { get; init; }

    /// <summary>Maximum InternetLatency.Milliseconds seen in the window.</summary>
    public double? InternetLatencyMaxMs { get; init; }

    /// <summary>Population standard deviation of InternetLatency.Milliseconds.</summary>
    public double? InternetLatencyStdDevMs { get; init; }

    /// <summary>
    /// Fraction of samples (0.0–1.0) in which InternetLatency was a timeout.
    /// High values indicate complete or near-complete internet connectivity loss.
    /// </summary>
    public double? InternetTimeoutRatio { get; init; }

    // ── Wi-Fi RF metrics (Rssi, TxRate, RxRate) ───────────────────────────

    /// <summary>Mean RSSI in dBm across the window.</summary>
    public double? RssiAverageDbm { get; init; }

    /// <summary>Minimum (weakest) RSSI in dBm seen in the window.</summary>
    public double? RssiMinDbm { get; init; }

    /// <summary>Maximum (strongest) RSSI in dBm seen in the window.</summary>
    public double? RssiMaxDbm { get; init; }

    /// <summary>
    /// Estimated RSSI trend in dBm per minute, computed via linear regression over the window.
    /// Negative = signal is declining. Null if window duration is too short to be meaningful.
    /// </summary>
    public double? RssiTrendDbmPerMinute { get; init; }

    /// <summary>Mean TxRate (PHY upload rate) in Mbps.</summary>
    public double? PhyRateAverageMbps { get; init; }

    /// <summary>Minimum TxRate in Mbps seen in the window (reflects worst-case rate adaptation).</summary>
    public double? PhyRateMinMbps { get; init; }

    // ── Gateway / DNS ──────────────────────────────────────────────────────

    /// <summary>Mean GatewayLatency in ms (same underlying data as LanLatencyAverageMs, exposed separately for clarity).</summary>
    public double? GatewayLatencyAverageMs { get; init; }

    /// <summary>
    /// Mean DnsLatency in ms across samples where DnsLatency.IsTimeout == false.
    /// Null if all DNS probes timed out.
    /// </summary>
    public double? DnsLatencyAverageMs { get; init; }

    /// <summary>Fraction of samples (0.0–1.0) in which DnsLatency timed out.</summary>
    public double? DnsTimeoutRatio { get; init; }
}
