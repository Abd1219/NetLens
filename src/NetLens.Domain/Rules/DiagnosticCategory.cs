namespace NetLens.Domain.Rules;

/// <summary>
/// Classifies which aspect of the network a diagnostic result pertains to.
/// Used by the UI and reporting layers to group and filter results.
/// </summary>
public enum DiagnosticCategory
{
    /// <summary>Radio Frequency metrics: RSSI, Signal Quality, PHY Rate, Band, Channel.</summary>
    RF,

    /// <summary>Local network metrics: Gateway latency, packet loss, jitter.</summary>
    Network,

    /// <summary>End-to-end connectivity: DNS, internet latency, full connectivity loss.</summary>
    Connectivity,

    /// <summary>Host resources: CPU, RAM.</summary>
    System,

    /// <summary>Multi-metric correlations spanning multiple categories.</summary>
    Correlation
}
