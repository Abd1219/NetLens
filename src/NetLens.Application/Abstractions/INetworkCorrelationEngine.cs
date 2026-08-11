using NetLens.Domain.Correlation;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Analyzes a NetworkMetricsWindow and produces correlation findings.
///
/// The engine is stateless and deterministic: given the same input window,
/// it always produces the same output. It has no dependency on the UI,
/// WinUI controls, LocalizationService, or any persistence layer.
/// </summary>
public interface INetworkCorrelationEngine
{
    /// <summary>
    /// Runs all correlation analyses against the provided metrics window.
    /// </summary>
    /// <param name="window">Aggregated metrics from a recent rolling window of snapshots.</param>
    /// <returns>
    /// List of detected correlations, ordered by EvidenceScore descending.
    /// Results with EvidenceStrength.Weak are suppressed and not included.
    /// Returns an empty list when no meaningful correlations are detected.
    /// Never returns null.
    /// </returns>
    IReadOnlyList<NetworkCorrelationResult> Analyze(NetworkMetricsWindow window);
}
