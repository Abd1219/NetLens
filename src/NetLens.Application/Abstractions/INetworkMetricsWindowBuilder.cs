using NetLens.Domain.Correlation;
using NetLens.Domain.Entities;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Builds a NetworkMetricsWindow by aggregating statistics from a collection of WirelessSnapshots.
/// </summary>
public interface INetworkMetricsWindowBuilder
{
    /// <summary>
    /// Computes aggregated metrics from the provided snapshots.
    /// </summary>
    /// <param name="snapshots">
    /// Ordered list of snapshots (oldest first). Should contain at least
    /// CorrelationThresholds.MinSamplesRequired snapshots for meaningful output.
    /// </param>
    /// <returns>
    /// A NetworkMetricsWindow with populated fields where data was available,
    /// and null fields where data was missing or insufficient.
    /// </returns>
    NetworkMetricsWindow Build(IReadOnlyList<WirelessSnapshot> snapshots);
}
