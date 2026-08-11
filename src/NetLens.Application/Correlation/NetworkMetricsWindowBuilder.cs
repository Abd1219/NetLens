using NetLens.Application.Abstractions;
using NetLens.Domain.Correlation;
using NetLens.Domain.Entities;

namespace NetLens.Application.Correlation;

/// <summary>
/// Computes a NetworkMetricsWindow by aggregating statistics from a list of WirelessSnapshots.
///
/// Statistics computed:
///   - Mean (average), min, max for continuous metrics
///   - Population standard deviation for latency (measures stability)
///   - Timeout/null ratios for probe-based metrics
///   - RSSI trend using least-squares linear regression over time
///
/// Data availability rules:
///   - Timeout samples (Latency.IsTimeout == true) are excluded from averages
///     but counted for timeout ratio calculation.
///   - If a metric has zero valid (non-timeout) samples, its average is null.
///   - RSSI trend is null if window duration < 10 seconds (insufficient for regression).
/// </summary>
public sealed class NetworkMetricsWindowBuilder : INetworkMetricsWindowBuilder
{
    public NetworkMetricsWindow Build(IReadOnlyList<WirelessSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        int n = snapshots.Count;

        if (n == 0)
        {
            return new NetworkMetricsWindow
            {
                SampleCount = 0,
                WindowDurationSeconds = 0
            };
        }

        double windowDuration = n > 1
            ? (snapshots[n - 1].CapturedAt - snapshots[0].CapturedAt).TotalSeconds
            : 0;

        // ── LAN: GatewayLatency ───────────────────────────────────────────
        var lanLatencies = snapshots
            .Where(s => !s.GatewayLatency.IsTimeout)
            .Select(s => s.GatewayLatency.Milliseconds)
            .ToList();

        double? lanLatAvg   = lanLatencies.Count > 0 ? lanLatencies.Average() : null;
        double? lanLatMin   = lanLatencies.Count > 0 ? lanLatencies.Min()     : null;
        double? lanLatMax   = lanLatencies.Count > 0 ? lanLatencies.Max()     : null;
        double? lanLatStdDev = lanLatencies.Count > 1 ? StdDev(lanLatencies)  : null;

        // ── LAN: Jitter ───────────────────────────────────────────────────
        var jitterValues = snapshots.Select(s => s.Jitter.Milliseconds).ToList();
        double? jitterAvg = jitterValues.Average();
        double? jitterMax = jitterValues.Max();

        // ── LAN: PacketLoss (aggregate probe; no WAN-specific loss available) ──
        var lossValues = snapshots.Select(s => s.PacketLoss.Percentage).ToList();
        double? lossAvg = lossValues.Average();

        // Persistence: fraction of samples where loss exceeds warning threshold
        double lossPersistence = (double)lossValues.Count(l => l > CorrelationThresholds.PacketLossWarnPercent) / n;

        // ── Internet latency (only WAN-side metric available) ─────────────
        var internetSamples  = snapshots.Select(s => s.InternetLatency).ToList();
        var validInternet    = internetSamples.Where(l => !l.IsTimeout).Select(l => l.Milliseconds).ToList();
        int internetTimeouts = internetSamples.Count(l => l.IsTimeout);

        double? inetAvg    = validInternet.Count > 0 ? validInternet.Average()  : null;
        double? inetMin    = validInternet.Count > 0 ? validInternet.Min()      : null;
        double? inetMax    = validInternet.Count > 0 ? validInternet.Max()      : null;
        double? inetStdDev = validInternet.Count > 1 ? StdDev(validInternet)    : null;
        double  inetTimeoutRatio = (double)internetTimeouts / n;

        // ── Wi-Fi RSSI ────────────────────────────────────────────────────
        var rssiValues = snapshots.Select(s => (double)s.Rssi.Value).ToList();
        double? rssiAvg = rssiValues.Average();
        double? rssiMin = rssiValues.Min();
        double? rssiMax = rssiValues.Max();

        // RSSI linear trend via least-squares regression (dBm per minute)
        // Only computed if window spans at least 10 seconds
        double? rssiTrend = null;
        if (windowDuration >= 10.0 && n >= 3)
        {
            // x = elapsed seconds from first snapshot, y = RSSI value
            var xs = snapshots.Select(s => (s.CapturedAt - snapshots[0].CapturedAt).TotalSeconds).ToArray();
            var ys = rssiValues.ToArray();
            double slope = LinearRegressionSlope(xs, ys);
            // Convert slope from dBm/second to dBm/minute
            rssiTrend = slope * 60.0;
        }

        // ── PHY Rate (TxRate) ─────────────────────────────────────────────
        var txRates = snapshots.Select(s => s.TxRate.Value).ToList();
        double? phyAvg = txRates.Average();
        double? phyMin = txRates.Min();

        // ── DNS ───────────────────────────────────────────────────────────
        var dnsSamples    = snapshots.Select(s => s.DnsLatency).ToList();
        var validDns      = dnsSamples.Where(d => !d.IsTimeout).Select(d => d.Milliseconds).ToList();
        int dnsTimeouts   = dnsSamples.Count(d => d.IsTimeout);
        double? dnsAvg    = validDns.Count > 0 ? validDns.Average() : null;
        double  dnsTimeoutRatio = (double)dnsTimeouts / n;

        return new NetworkMetricsWindow
        {
            SampleCount               = n,
            WindowDurationSeconds     = windowDuration,

            LanLatencyAverageMs       = lanLatAvg,
            LanLatencyMinMs           = lanLatMin,
            LanLatencyMaxMs           = lanLatMax,
            LanLatencyStdDevMs        = lanLatStdDev,
            LanJitterAverageMs        = jitterAvg,
            LanJitterMaxMs            = jitterMax,
            LanPacketLossPercent      = lossAvg,
            LanPacketLossPersistenceRatio = lossPersistence,

            InternetLatencyAverageMs  = inetAvg,
            InternetLatencyMinMs      = inetMin,
            InternetLatencyMaxMs      = inetMax,
            InternetLatencyStdDevMs   = inetStdDev,
            InternetTimeoutRatio      = inetTimeoutRatio,

            RssiAverageDbm            = rssiAvg,
            RssiMinDbm                = rssiMin,
            RssiMaxDbm                = rssiMax,
            RssiTrendDbmPerMinute     = rssiTrend,
            PhyRateAverageMbps        = phyAvg,
            PhyRateMinMbps            = phyMin,

            GatewayLatencyAverageMs   = lanLatAvg,   // same source as LAN latency
            DnsLatencyAverageMs       = dnsAvg,
            DnsTimeoutRatio           = dnsTimeoutRatio
        };
    }

    /// <summary>Population standard deviation of a list of doubles.</summary>
    private static double StdDev(IReadOnlyList<double> values)
    {
        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Least-squares linear regression slope (dy/dx).
    /// Returns the rate of change in y per unit of x.
    /// </summary>
    private static double LinearRegressionSlope(double[] xs, double[] ys)
    {
        int n     = xs.Length;
        double mx = xs.Average();
        double my = ys.Average();

        double numerator   = 0;
        double denominator = 0;
        for (int i = 0; i < n; i++)
        {
            numerator   += (xs[i] - mx) * (ys[i] - my);
            denominator += (xs[i] - mx) * (xs[i] - mx);
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }
}
