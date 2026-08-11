namespace NetLens.Domain.Correlation;

/// <summary>
/// Identifies the type of cross-metric correlation detected by NetworkCorrelationEngine.
///
/// Each value represents a distinct network problem pattern inferred from
/// analyzing multiple metrics across a rolling window of observations.
/// These types are deterministic, not probabilistic.
///
/// Design note: This enum is kept in NetLens.Domain so that it can be referenced
/// by UI localization layers without a dependency on NetLens.Application.
/// </summary>
public enum CorrelationType
{
    /// <summary>
    /// Low RSSI combined with elevated LAN jitter and/or packet loss while
    /// internet latency remains relatively stable. Suggests the Wi-Fi radio link
    /// is the degrading factor, not the upstream network.
    /// </summary>
    WifiInstability,

    /// <summary>
    /// LAN metrics (gateway latency, jitter, packet loss) remain stable while
    /// internet latency is elevated. Points to an issue in the ISP or external
    /// network path, not in the local Wi-Fi or LAN infrastructure.
    /// </summary>
    ExternalNetworkIssue,

    /// <summary>
    /// Simultaneous degradation across both LAN and internet-side metrics
    /// (gateway latency, jitter, packet loss, internet latency). Suggests a
    /// systemic network problem rather than an isolated issue on one side.
    /// </summary>
    NetworkInstability,

    /// <summary>
    /// A sustained, progressive decline in RSSI over time. This is distinct from
    /// "signal is always low" (handled by the atomic LOW_RSSI rule). Requires
    /// a measurable negative trend across the observation window.
    /// </summary>
    SignalDegradation,

    /// <summary>
    /// Elevated LAN and internet latency combined with high jitter, suggesting
    /// traffic congestion or bandwidth saturation. Reported as "possible" because
    /// congestion cannot be confirmed without traffic-level data.
    /// </summary>
    PossibleCongestion
}
