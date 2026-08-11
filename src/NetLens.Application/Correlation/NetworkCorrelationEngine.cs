using NetLens.Application.Abstractions;
using NetLens.Domain.Correlation;
using NetLens.Domain.Rules;

namespace NetLens.Application.Correlation;

/// <summary>
/// NetworkCorrelationEngine: analyzes relationships between network metrics across a
/// rolling window of observations to produce contextual evidence for diagnosis.
///
/// DESIGN PRINCIPLES:
///   - Deterministic: same input always produces same output
///   - Explicable: every result includes evidence showing which metrics fired and why
///   - Conservative: requires sufficient samples and persistent patterns (not single spikes)
///   - Independent: no UI, no XAML, no localization, no persistence dependency
///
/// PIPELINE (per Analyze() call):
///   1. Guard: SampleCount >= MinSamplesRequired; else return empty
///   2. Run all 5 correlation analyses independently
///   3. Collect non-null results
///   4. Suppress results with EvidenceScore < MinReportableScore (Weak evidence)
///   5. Sort by EvidenceScore descending
///
/// WHAT THIS ENGINE IS NOT:
///   - It does NOT replace the 11 atomic diagnostic rules
///   - It does NOT modify DiagnosticService or ICorrelationRule
///   - It does NOT use machine learning, statistics libraries, or external APIs
///   - It does NOT store state between calls (pass the window in each time)
///
/// METRIC MAPPING:
///   LAN-side  = GatewayLatency, Jitter, PacketLoss (aggregate probe)
///   WAN-side  = InternetLatency only (no separate WAN packet loss exists in WirelessSnapshot)
/// </summary>
public sealed class NetworkCorrelationEngine : INetworkCorrelationEngine
{
    public IReadOnlyList<NetworkCorrelationResult> Analyze(NetworkMetricsWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Guard: insufficient data — return empty rather than speculative results
        if (window.SampleCount < CorrelationThresholds.MinSamplesRequired)
            return [];

        var results = new List<NetworkCorrelationResult>();

        TryAdd(results, AnalyzeWifiInstability(window));
        TryAdd(results, AnalyzeExternalNetworkIssue(window));
        TryAdd(results, AnalyzeNetworkInstability(window));
        TryAdd(results, AnalyzeSignalDegradation(window));
        TryAdd(results, AnalyzePossibleCongestion(window));

        // Sort by evidence score descending (highest confidence first)
        results.Sort((a, b) => b.EvidenceScore.CompareTo(a.EvidenceScore));

        return results.AsReadOnly();
    }

    // ── Correlation A: WifiInstability ────────────────────────────────────

    /// <summary>
    /// Detects when poor Wi-Fi RF conditions are causing LAN-side degradation
    /// while the internet path remains relatively stable.
    ///
    /// Pattern:
    ///   RSSI is below acceptable levels  (suggests weak RF link)
    ///   AND LAN jitter or packet loss is elevated  (effect of RF retransmissions)
    ///   AND internet latency is not similarly elevated  (rules out upstream cause)
    ///
    /// This correlation adds contextual evidence to atomic LOW_RSSI or HIGH_JITTER rules.
    /// It does NOT suppress them.
    /// </summary>
    private static NetworkCorrelationResult? AnalyzeWifiInstability(NetworkMetricsWindow w)
    {
        // All three LAN metrics and at least RSSI must be available
        if (!w.RssiAverageDbm.HasValue ||
            !w.LanJitterAverageMs.HasValue ||
            !w.LanPacketLossPercent.HasValue)
            return null;

        double rssi       = w.RssiAverageDbm.Value;
        double jitter     = w.LanJitterAverageMs.Value;
        double packetLoss = w.LanPacketLossPercent.Value;

        // Minimum condition: RSSI must be below warning threshold
        // Without this, we can't attribute LAN issues to Wi-Fi
        if (rssi >= CorrelationThresholds.RssiWarnThresholdDbm)
            return null;

        int score = 0;
        var metrics  = new List<string>();
        var evidence = new Dictionary<string, string>
        {
            { CorrelationEvidenceKeys.RssiAvg,    $"{rssi:F1} dBm" },
            { CorrelationEvidenceKeys.LanJitterAvg, $"{jitter:F1} ms" },
            { CorrelationEvidenceKeys.LanPacketLossAvg, $"{packetLoss:F1}%" },
            { CorrelationEvidenceKeys.SampleCount, w.SampleCount.ToString() }
        };

        // Indicator 1: RSSI below threshold (required — already enforced above)
        score += CorrelationThresholds.W_Wifi_LowRssi;
        metrics.Add(CorrelationEvidenceKeys.RssiAvg);

        // Indicator 2: Jitter is elevated
        if (jitter > CorrelationThresholds.JitterWarnMs)
        {
            score += CorrelationThresholds.W_Wifi_HighJitter;
            metrics.Add(CorrelationEvidenceKeys.LanJitterAvg);
        }

        // Indicator 3: Packet loss is elevated
        if (packetLoss > CorrelationThresholds.PacketLossWarnPercent)
        {
            score += CorrelationThresholds.W_Wifi_PacketLoss;
            metrics.Add(CorrelationEvidenceKeys.LanPacketLossAvg);
        }

        // Indicator 4: Packet loss is persistent (not just a spike)
        if (w.LanPacketLossPersistenceRatio.HasValue &&
            w.LanPacketLossPersistenceRatio.Value > CorrelationThresholds.PacketLossPersistenceThreshold)
        {
            score += CorrelationThresholds.W_Wifi_PersistentLoss;
            metrics.Add(CorrelationEvidenceKeys.LanPacketLossPersistence);
            evidence[CorrelationEvidenceKeys.LanPacketLossPersistence] =
                $"{w.LanPacketLossPersistenceRatio.Value * 100:F0}% of samples";
        }

        // Indicator 5: Internet is stable (confirms local Wi-Fi is the cause)
        // If internet is also degraded, WifiInstability is less certain (might be NetworkInstability)
        if (w.InternetLatencyAverageMs.HasValue &&
            w.InternetLatencyAverageMs.Value < CorrelationThresholds.InternetLatencyStableMs)
        {
            score += CorrelationThresholds.W_Wifi_InternetStable;
            metrics.Add(CorrelationEvidenceKeys.InternetLatencyAvg);
            evidence[CorrelationEvidenceKeys.InternetLatencyAvg] = $"{w.InternetLatencyAverageMs.Value:F1} ms";
        }

        if (score < CorrelationThresholds.MinReportableScore)
            return null;

        evidence[CorrelationEvidenceKeys.EvidenceScore] = score.ToString();

        var severity = score >= 70
            ? DiagnosticSeverity.Warning
            : DiagnosticSeverity.Info;

        return new NetworkCorrelationResult(
            correlationType:        CorrelationType.WifiInstability,
            evidenceScore:          score,
            severity:               severity,
            evidence:               evidence,
            contributingMetrics:    metrics,
            technicalDescription:   $"RF metrics suggest the Wi-Fi link is degrading LAN performance. " +
                                    $"RSSI avg {rssi:F1} dBm, jitter avg {jitter:F1} ms, packet loss avg {packetLoss:F1}%.",
            technicalRecommendation: "Move closer to the AP, reduce obstructions, or investigate AP placement. " +
                                     "Check if the client is roaming to a suboptimal BSSID.",
            metricsSnapshot:        w);
    }

    // ── Correlation B: ExternalNetworkIssue ──────────────────────────────

    /// <summary>
    /// Detects when the local network is stable while the external internet path is degraded.
    ///
    /// Pattern:
    ///   LAN metrics are healthy (gateway latency low, jitter acceptable, packet loss low)
    ///   AND internet latency is elevated or timed out
    ///
    /// NOTE on metrics:
    ///   WirelessSnapshot does NOT provide a separate WAN packet loss field.
    ///   The single PacketLoss field is an aggregate local probe.
    ///   Therefore, this correlation relies on InternetLatency and internet timeout ratio
    ///   as the sole internet-side indicators, NOT on a WAN-specific packet loss.
    /// </summary>
    private static NetworkCorrelationResult? AnalyzeExternalNetworkIssue(NetworkMetricsWindow w)
    {
        // Need LAN metrics to confirm "LAN is stable"
        if (!w.LanLatencyAverageMs.HasValue ||
            !w.LanJitterAverageMs.HasValue ||
            !w.LanPacketLossPercent.HasValue)
            return null;

        // Need at least one internet-side indicator
        bool hasInetLatency  = w.InternetLatencyAverageMs.HasValue;
        bool hasInetTimeouts = w.InternetTimeoutRatio.HasValue;

        if (!hasInetLatency && !hasInetTimeouts)
            return null;

        double lanLatency   = w.LanLatencyAverageMs.Value;
        double lanJitter    = w.LanJitterAverageMs.Value;
        double lanLoss      = w.LanPacketLossPercent.Value;

        // Core condition: LAN must be stable — if LAN is also bad, this is NetworkInstability
        bool lanIsStable = lanLatency < CorrelationThresholds.LanLatencyStableMs
                        && lanJitter  < CorrelationThresholds.JitterWarnMs
                        && lanLoss    < CorrelationThresholds.PacketLossWarnPercent;

        if (!lanIsStable)
            return null;

        // Requiring actual external degradation (latency elevation or timeouts)
        bool internetLatencyElevated = hasInetLatency && w.InternetLatencyAverageMs!.Value > CorrelationThresholds.InternetLatencyWarnMs;
        bool internetTimeoutsObserved = hasInetTimeouts && w.InternetTimeoutRatio!.Value > CorrelationThresholds.InternetTimeoutRatioWarn;

        if (!internetLatencyElevated && !internetTimeoutsObserved)
            return null;

        int score   = 0;
        var metrics = new List<string>();
        var evidence = new Dictionary<string, string>
        {
            { CorrelationEvidenceKeys.LanLatencyAvg,     $"{lanLatency:F1} ms" },
            { CorrelationEvidenceKeys.LanJitterAvg,      $"{lanJitter:F1} ms" },
            { CorrelationEvidenceKeys.LanPacketLossAvg,  $"{lanLoss:F1}%" },
            { CorrelationEvidenceKeys.SampleCount,       w.SampleCount.ToString() }
        };

        // Indicator 1: LAN is confirmed stable (prerequisite, awarded points)
        score += CorrelationThresholds.W_Ext_LanStable;
        metrics.Add(CorrelationEvidenceKeys.LanLatencyAvg);

        // Indicator 2: Internet latency is elevated
        if (hasInetLatency)
        {
            double inetLat = w.InternetLatencyAverageMs!.Value;
            evidence[CorrelationEvidenceKeys.InternetLatencyAvg] = $"{inetLat:F1} ms";

            if (inetLat > CorrelationThresholds.InternetLatencyWarnMs)
            {
                score += CorrelationThresholds.W_Ext_InternetHigh;
                metrics.Add(CorrelationEvidenceKeys.InternetLatencyAvg);

                // Extra points if very high (> 2× warning threshold)
                if (inetLat > CorrelationThresholds.InternetLatencyWarnMs * 2)
                {
                    score += CorrelationThresholds.W_Ext_InternetVeryHigh;
                }
            }
        }

        // Indicator 3: Internet probe timeouts (partial or full internet loss)
        if (hasInetTimeouts && w.InternetTimeoutRatio!.Value > CorrelationThresholds.InternetTimeoutRatioWarn)
        {
            score += CorrelationThresholds.W_Ext_InternetTimeouts;
            metrics.Add(CorrelationEvidenceKeys.InternetTimeoutRatio);
            evidence[CorrelationEvidenceKeys.InternetTimeoutRatio] =
                $"{w.InternetTimeoutRatio.Value * 100:F0}% of probes";
        }

        if (score < CorrelationThresholds.MinReportableScore)
            return null;

        evidence[CorrelationEvidenceKeys.EvidenceScore] = score.ToString();

        var severity = score >= 65
            ? DiagnosticSeverity.Warning
            : DiagnosticSeverity.Info;

        return new NetworkCorrelationResult(
            correlationType:        CorrelationType.ExternalNetworkIssue,
            evidenceScore:          score,
            severity:               severity,
            evidence:               evidence,
            contributingMetrics:    metrics,
            technicalDescription:   "Local network (LAN/Wi-Fi) metrics are stable while the external internet path shows degradation. " +
                                    $"LAN latency avg {lanLatency:F1} ms is healthy; internet latency is elevated or timing out.",
            technicalRecommendation: "The issue is upstream of this device. Check ISP status, router WAN port, and modem logs. " +
                                     "Contact the ISP if the problem persists.",
            metricsSnapshot:        w);
    }

    // ── Correlation C: NetworkInstability ────────────────────────────────

    /// <summary>
    /// Detects simultaneous degradation across both LAN and internet metrics.
    ///
    /// Pattern:
    ///   LAN latency elevated AND internet latency elevated AND jitter/loss present
    ///
    /// Anti-spike guard:
    ///   Requires that at least NetworkInstabilityPersistenceThreshold fraction of
    ///   the window shows degradation, not just a single bad sample.
    /// </summary>
    private static NetworkCorrelationResult? AnalyzeNetworkInstability(NetworkMetricsWindow w)
    {
        if (!w.LanLatencyAverageMs.HasValue   ||
            !w.InternetLatencyAverageMs.HasValue ||
            !w.LanJitterAverageMs.HasValue    ||
            !w.LanPacketLossPercent.HasValue)
            return null;

        double lanLat  = w.LanLatencyAverageMs.Value;
        double inetLat = w.InternetLatencyAverageMs.Value;
        double jitter  = w.LanJitterAverageMs.Value;
        double loss    = w.LanPacketLossPercent.Value;

        bool lanDegraded  = lanLat > CorrelationThresholds.LanLatencyWarnMs;
        bool inetDegraded = inetLat > CorrelationThresholds.InternetLatencyWarnMs;
        bool jitterHigh   = jitter > CorrelationThresholds.JitterWarnMs;
        bool lossHigh     = loss > CorrelationThresholds.PacketLossWarnPercent;

        // Both LAN and internet must be degraded — otherwise use more specific correlations
        if (!lanDegraded || !inetDegraded)
            return null;

        // Anti-spike guard: packet loss must be persistent across the window
        // Single-spike isolation: if loss is low and persistence is low, skip
        bool hasPersistentDegradation =
            w.LanPacketLossPersistenceRatio.HasValue &&
            w.LanPacketLossPersistenceRatio.Value >= CorrelationThresholds.NetworkInstabilityPersistenceThreshold;

        // Also accept if LAN + internet latency are both very elevated (even without loss persistence)
        bool severeLatencyBoth = lanLat > CorrelationThresholds.LanLatencyWarnMs * 2
                              && inetLat > CorrelationThresholds.InternetLatencyWarnMs * 1.5;

        if (!hasPersistentDegradation && !severeLatencyBoth)
            return null;

        int score   = 0;
        var metrics = new List<string>();
        var evidence = new Dictionary<string, string>
        {
            { CorrelationEvidenceKeys.LanLatencyAvg,      $"{lanLat:F1} ms" },
            { CorrelationEvidenceKeys.InternetLatencyAvg, $"{inetLat:F1} ms" },
            { CorrelationEvidenceKeys.LanJitterAvg,       $"{jitter:F1} ms" },
            { CorrelationEvidenceKeys.LanPacketLossAvg,   $"{loss:F1}%" },
            { CorrelationEvidenceKeys.SampleCount,        w.SampleCount.ToString() }
        };

        if (lanDegraded)  { score += CorrelationThresholds.W_Net_LanDegraded;      metrics.Add(CorrelationEvidenceKeys.LanLatencyAvg); }
        if (inetDegraded) { score += CorrelationThresholds.W_Net_InternetDegraded;  metrics.Add(CorrelationEvidenceKeys.InternetLatencyAvg); }
        if (jitterHigh)   { score += CorrelationThresholds.W_Net_JitterHigh;        metrics.Add(CorrelationEvidenceKeys.LanJitterAvg); }
        if (lossHigh)     { score += CorrelationThresholds.W_Net_PacketLoss;        metrics.Add(CorrelationEvidenceKeys.LanPacketLossAvg); }

        if (score < CorrelationThresholds.MinReportableScore)
            return null;

        evidence[CorrelationEvidenceKeys.EvidenceScore] = score.ToString();

        var severity = score >= 60
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        return new NetworkCorrelationResult(
            correlationType:        CorrelationType.NetworkInstability,
            evidenceScore:          score,
            severity:               severity,
            evidence:               evidence,
            contributingMetrics:    metrics,
            technicalDescription:   "Both LAN and internet path show simultaneous degradation. " +
                                    $"LAN latency {lanLat:F1} ms, internet latency {inetLat:F1} ms, " +
                                    $"jitter {jitter:F1} ms, packet loss {loss:F1}%.",
            technicalRecommendation: "Check router load, WAN connection health, and local network congestion. " +
                                     "Review all active connections and running services.",
            metricsSnapshot:        w);
    }

    // ── Correlation D: SignalDegradation (windowed trend) ─────────────────

    /// <summary>
    /// Detects a progressive, time-directional decline in RSSI across the window.
    ///
    /// Key distinction from the atomic LOW_RSSI rule:
    ///   LOW_RSSI fires when current RSSI is below a fixed threshold.
    ///   SignalDegradation (windowed) fires when RSSI is progressively declining,
    ///   regardless of whether it has crossed a threshold yet.
    ///
    /// Requirements:
    ///   1. RSSI trend (via linear regression) is below RssiTrendWarningDbmPerMinute
    ///   2. RSSI spread (max - min) is >= RssiMinSpreadDbm to confirm real movement
    ///      (not just measurement noise of ±2-3 dBm)
    ///
    /// This can co-exist with (not replace) the atomic SIGNAL_DEGRADATION ICorrelationRule.
    /// </summary>
    private static NetworkCorrelationResult? AnalyzeSignalDegradation(NetworkMetricsWindow w)
    {
        if (!w.RssiTrendDbmPerMinute.HasValue ||
            !w.RssiAverageDbm.HasValue        ||
            !w.RssiMinDbm.HasValue            ||
            !w.RssiMaxDbm.HasValue)
            return null;

        double trend  = w.RssiTrendDbmPerMinute.Value;
        double spread = w.RssiMaxDbm.Value - w.RssiMinDbm.Value; // positive = range of variation

        // Minimum conditions: trend must be declining AND there must be meaningful spread
        if (trend >= CorrelationThresholds.RssiTrendWarningDbmPerMinute)
            return null;

        if (spread < CorrelationThresholds.RssiMinSpreadDbm)
            return null;

        int score   = 0;
        var metrics = new List<string>();
        var evidence = new Dictionary<string, string>
        {
            { CorrelationEvidenceKeys.RssiAvg,    $"{w.RssiAverageDbm.Value:F1} dBm" },
            { CorrelationEvidenceKeys.RssiMin,    $"{w.RssiMinDbm.Value:F1} dBm" },
            { CorrelationEvidenceKeys.RssiMax,    $"{w.RssiMaxDbm.Value:F1} dBm" },
            { CorrelationEvidenceKeys.RssiTrend,  $"{trend:F2} dBm/min" },
            { CorrelationEvidenceKeys.SampleCount, w.SampleCount.ToString() }
        };

        // Points for confirmed declining trend
        score += CorrelationThresholds.W_Sig_TrendNegative;
        metrics.Add(CorrelationEvidenceKeys.RssiTrend);

        // Points for meaningful spread (not noise)
        if (spread >= CorrelationThresholds.RssiMinSpreadDbm)
        {
            score += CorrelationThresholds.W_Sig_Spread;
            metrics.Add(CorrelationEvidenceKeys.RssiMin);
        }

        // Additional points for strong/accelerating decline
        if (trend < CorrelationThresholds.RssiTrendWarningDbmPerMinute * 2)
        {
            score += CorrelationThresholds.W_Sig_TrendStrong;
        }

        // Additional points if current average is already below warning threshold
        if (w.RssiAverageDbm.Value < CorrelationThresholds.RssiWarnThresholdDbm)
        {
            score += CorrelationThresholds.W_Sig_LowCurrentRssi;
            metrics.Add(CorrelationEvidenceKeys.RssiAvg);
        }

        if (score < CorrelationThresholds.MinReportableScore)
            return null;

        evidence[CorrelationEvidenceKeys.EvidenceScore] = score.ToString();

        var severity = w.RssiAverageDbm.Value < CorrelationThresholds.RssiWarnThresholdDbm
            ? DiagnosticSeverity.Warning
            : DiagnosticSeverity.Info;

        return new NetworkCorrelationResult(
            correlationType:        CorrelationType.SignalDegradation,
            evidenceScore:          score,
            severity:               severity,
            evidence:               evidence,
            contributingMetrics:    metrics,
            technicalDescription:   $"RSSI is progressively declining at {trend:F2} dBm/min. " +
                                    $"Range over window: {w.RssiMaxDbm.Value:F0} dBm → {w.RssiMinDbm.Value:F0} dBm ({spread:F0} dBm total drop).",
            technicalRecommendation: "The device may be moving away from the AP, or the AP may be reducing transmit power. " +
                                     "Monitor for continued decline. Consider repositioning or enabling band steering to a stronger BSS.",
            metricsSnapshot:        w);
    }

    // ── Correlation E: PossibleCongestion ────────────────────────────────

    /// <summary>
    /// Detects possible network congestion/saturation when both LAN and internet
    /// latency are elevated with high jitter, suggesting traffic contention.
    ///
    /// Called "possible" because without traffic-level data (e.g., interface utilization),
    /// congestion cannot be distinguished from other causes of high latency.
    ///
    /// Requires a minimum EvidenceScore of Moderate to be reported, preventing
    /// this from firing on partial evidence alone.
    /// </summary>
    private static NetworkCorrelationResult? AnalyzePossibleCongestion(NetworkMetricsWindow w)
    {
        if (!w.LanLatencyAverageMs.HasValue    ||
            !w.InternetLatencyAverageMs.HasValue ||
            !w.LanJitterAverageMs.HasValue     ||
            !w.LanLatencyStdDevMs.HasValue)
            return null;

        double lanLat    = w.LanLatencyAverageMs.Value;
        double inetLat   = w.InternetLatencyAverageMs.Value;
        double jitter    = w.LanJitterAverageMs.Value;
        double lanStdDev = w.LanLatencyStdDevMs.Value;

        // All four must show some elevation for congestion to be plausible
        bool lanHigh     = lanLat   > CorrelationThresholds.LanLatencyWarnMs;
        bool inetHigh    = inetLat  > CorrelationThresholds.InternetLatencyWarnMs;
        bool jitterHigh  = jitter   > CorrelationThresholds.JitterWarnMs;
        bool burstyLan   = lanStdDev > CorrelationThresholds.CongestionLatencyStdDevMs;

        // Need at least 3 of the 4 indicators to avoid spurious matches
        int indicatorCount = (lanHigh ? 1 : 0) + (inetHigh ? 1 : 0) + (jitterHigh ? 1 : 0) + (burstyLan ? 1 : 0);
        if (indicatorCount < 3)
            return null;

        int score   = 0;
        var metrics = new List<string>();
        var evidence = new Dictionary<string, string>
        {
            { CorrelationEvidenceKeys.LanLatencyAvg,      $"{lanLat:F1} ms" },
            { CorrelationEvidenceKeys.LanLatencyStdDev,   $"{lanStdDev:F1} ms" },
            { CorrelationEvidenceKeys.InternetLatencyAvg, $"{inetLat:F1} ms" },
            { CorrelationEvidenceKeys.LanJitterAvg,       $"{jitter:F1} ms" },
            { CorrelationEvidenceKeys.SampleCount,        w.SampleCount.ToString() }
        };

        if (lanHigh)    { score += CorrelationThresholds.W_Cong_LanLatencyHigh;     metrics.Add(CorrelationEvidenceKeys.LanLatencyAvg); }
        if (inetHigh)   { score += CorrelationThresholds.W_Cong_InternetLatencyHigh; metrics.Add(CorrelationEvidenceKeys.InternetLatencyAvg); }
        if (jitterHigh) { score += CorrelationThresholds.W_Cong_JitterHigh;         metrics.Add(CorrelationEvidenceKeys.LanJitterAvg); }
        if (burstyLan)  { score += CorrelationThresholds.W_Cong_LatencyVariance;    metrics.Add(CorrelationEvidenceKeys.LanLatencyStdDev); }

        if (score < CorrelationThresholds.MinReportableScore)
            return null;

        evidence[CorrelationEvidenceKeys.EvidenceScore] = score.ToString();

        return new NetworkCorrelationResult(
            correlationType:        CorrelationType.PossibleCongestion,
            evidenceScore:          score,
            severity:               DiagnosticSeverity.Warning,
            evidence:               evidence,
            contributingMetrics:    metrics,
            technicalDescription:   $"Elevated and variable latency on both LAN ({lanLat:F1} ms, σ={lanStdDev:F1} ms) and internet ({inetLat:F1} ms) " +
                                    $"with high jitter ({jitter:F1} ms) suggests possible traffic congestion or bandwidth saturation.",
            technicalRecommendation: "Review active connections and traffic utilization. Check for background downloads, " +
                                     "video streaming, or other bandwidth-intensive tasks. Consider QoS configuration.",
            metricsSnapshot:        w);
    }

    private static void TryAdd(List<NetworkCorrelationResult> list, NetworkCorrelationResult? result)
    {
        if (result is not null && result.EvidenceStrength != EvidenceStrength.Weak)
            list.Add(result);
    }
}
