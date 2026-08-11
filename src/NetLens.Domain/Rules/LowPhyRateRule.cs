using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Domain.Rules;

/// <summary>
/// Evaluates whether the PHY (Physical Layer) link rate is unexpectedly low given
/// the available context: WiFi band, PHY type, RSSI, and signal quality.
///
/// DESIGN PRINCIPLE: A low PHY rate in isolation is NOT necessarily a problem.
/// A 2.4 GHz 802.11n connection at 72 Mbps with excellent RSSI is working as expected.
/// This rule only fires when the rate is inconsistent with the available context,
/// or when multiple indicators together suggest an abnormal degradation.
///
/// Confidence is intentionally capped unless multiple contextual indicators support the diagnosis.
///
/// Band-specific thresholds (Tx AND Rx):
///   2.4 GHz:  Critical &lt; 12 Mbps  | Warning &lt; 24 Mbps   (802.11n baseline)
///   5 GHz:    Critical &lt; 54 Mbps  | Warning &lt; 100 Mbps  (802.11ac/ax baseline)
///   6 GHz:    Critical &lt; 100 Mbps | Warning &lt; 200 Mbps  (802.11ax baseline)
///   Unknown:  Critical &lt; 12 Mbps only, with Insufficient confidence
/// </summary>
public sealed class LowPhyRateRule : IDiagnosticRule
{
    public string RuleCode => "LOW_PHY_RATE";
    public string Name     => "Low PHY Link Rate";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        // Determine effective rate (use the lower of Tx/Rx as the bottleneck indicator)
        var txMbps = snapshot.TxRate.Value;
        var rxMbps = snapshot.RxRate.Value;
        var effectiveMbps = Math.Min(txMbps, rxMbps);

        // Get band-specific thresholds
        var (criticalThreshold, warningThreshold) = GetThresholds(snapshot.Band);

        // If band is Unknown and we have no reliable context, only diagnose critical cases
        // and do so with Insufficient confidence to avoid false positives.
        if (snapshot.Band == WifiBand.Unknown)
        {
            if (effectiveMbps >= 12)
                return null;

            return new DiagnosticResult(
                ruleCode:       RuleCode,
                category:       DiagnosticCategory.RF,
                severity:       DiagnosticSeverity.Warning,
                title:          "Low PHY Rate (Band Unknown)",
                description:    $"PHY rate is {snapshot.TxRate} Tx / {snapshot.RxRate} Rx. The WiFi band could not be determined, so this assessment has low confidence.",
                recommendation: "Verify the adapter reports frequency data. A very low link rate may indicate driver issues or a legacy AP.",
                confidence:     DiagnosticConfidence.Insufficient,
                evidence: BuildEvidence(snapshot, criticalThreshold, warningThreshold));
        }

        // Below warning threshold — check if it is contextually plausible
        if (effectiveMbps >= warningThreshold)
            return null;

        var severity = effectiveMbps < criticalThreshold
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        // Context check: if RSSI is already poor, the low PHY rate is explained by signal issues.
        // In that case, reduce confidence — LowRSSIRule or SignalDegradationRule is the primary diagnosis.
        bool rssiExplains = snapshot.Rssi.Value <= -70;
        if (rssiExplains)
        {
            // Only emit a low-confidence informational result; the primary cause is RSSI.
            return new DiagnosticResult(
                ruleCode:       RuleCode,
                category:       DiagnosticCategory.RF,
                severity:       DiagnosticSeverity.Info,
                title:          "Reduced PHY Rate (Explained by Low RSSI)",
                description:    $"PHY rate is {snapshot.TxRate} Tx / {snapshot.RxRate} Rx, which is consistent with the current RSSI of {snapshot.Rssi}. " +
                                "The reduced rate is a consequence of weak signal, not an independent problem.",
                recommendation: "Improve signal strength (move closer to the AP, reduce obstructions) to recover full link rate.",
                confidence:     DiagnosticConfidence.Low,
                evidence: BuildEvidence(snapshot, criticalThreshold, warningThreshold));
        }

        // Good RSSI + low PHY rate = more significant, may indicate interference or negotiation problem
        var confidence = effectiveMbps < criticalThreshold
            ? DiagnosticConfidence.High
            : DiagnosticConfidence.Medium;

        var description = $"PHY rate is {snapshot.TxRate} Tx / {snapshot.RxRate} Rx on {snapshot.Band.ToDisplayString()}, " +
                          $"which is below the expected minimum for this band despite acceptable signal strength ({snapshot.Rssi}).";

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.RF,
            severity:       severity,
            title:          "Low PHY Link Rate",
            description:    description,
            recommendation: "Check for channel width negotiation issues. Verify the AP supports the expected 802.11 standard. " +
                            "Look for interference on the current channel. Updating the wireless driver may also resolve rate negotiation problems.",
            confidence:     confidence,
            evidence: BuildEvidence(snapshot, criticalThreshold, warningThreshold));
    }

    private static (double Critical, double Warning) GetThresholds(WifiBand band) => band switch
    {
        WifiBand.Band2_4GHz => (12.0,  24.0),
        WifiBand.Band5GHz   => (54.0,  100.0),
        WifiBand.Band6GHz   => (100.0, 200.0),
        _                   => (12.0,  double.MaxValue) // Unknown: only critical threshold applies
    };

    private static Dictionary<string, string> BuildEvidence(
        WirelessSnapshot snapshot, double criticalThreshold, double warningThreshold) =>
        new()
        {
            { EvidenceKeys.TxRate,            snapshot.TxRate.ToString() },
            { EvidenceKeys.RxRate,            snapshot.RxRate.ToString() },
            { EvidenceKeys.Band,              snapshot.Band.ToDisplayString() },
            { EvidenceKeys.PhysicalType,      snapshot.PhysicalType },
            { EvidenceKeys.Rssi,              snapshot.Rssi.ToString() },
            { EvidenceKeys.SignalQuality,     snapshot.SignalQuality.ToString() },
            { EvidenceKeys.WarningThreshold,  warningThreshold == double.MaxValue ? "N/A" : $"{warningThreshold:N0} Mbps" },
            { EvidenceKeys.CriticalThreshold, $"{criticalThreshold:N0} Mbps" }
        };
}
