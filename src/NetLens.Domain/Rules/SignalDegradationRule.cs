using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Domain.Rules;

/// <summary>
/// Detects physical signal degradation by correlating:
///   - Low RSSI (LOW_RSSI fired)
///   - Low PHY rate (LOW_PHY_RATE fired or effective rate is low)
///   - Packet loss (HIGH_PACKET_LOSS fired or loss > 0)
///
/// This composite diagnosis is more informative than three separate atomic results
/// because it identifies a single root cause: poor RF conditions leading to
/// rate adaptation and packet loss.
/// </summary>
public sealed class SignalDegradationRule : ICorrelationRule
{
    public string RuleCode => "SIGNAL_DEGRADATION";
    public string Name     => "Signal Degradation";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot, IReadOnlyList<DiagnosticResult> atomicResults)
    {
        bool hasLowRssi       = atomicResults.Any(r => r.RuleCode == "LOW_RSSI");
        bool hasLowPhyRate    = atomicResults.Any(r => r.RuleCode == "LOW_PHY_RATE");
        bool hasPacketLoss    = atomicResults.Any(r => r.RuleCode == "HIGH_PACKET_LOSS")
                                || snapshot.PacketLoss.Percentage > 0;

        if (!hasLowRssi)
            return null;

        int confirmingFactors = (hasLowPhyRate ? 1 : 0) + (hasPacketLoss ? 1 : 0);
        if (confirmingFactors == 0)
            return null;

        var rssiResult   = atomicResults.FirstOrDefault(r => r.RuleCode == "LOW_RSSI");
        bool rssiCritical = rssiResult?.Severity == DiagnosticSeverity.Critical;

        var severity = rssiCritical ? DiagnosticSeverity.Critical : DiagnosticSeverity.Warning;

        var confidence = confirmingFactors >= 2
            ? DiagnosticConfidence.VeryHigh
            : DiagnosticConfidence.High;

        var description = $"Multiple RF indicators point to physical signal degradation. " +
                          $"RSSI is {snapshot.Rssi}" +
                          (hasLowPhyRate ? $", PHY rate is {snapshot.TxRate} Tx / {snapshot.RxRate} Rx" : "") +
                          (hasPacketLoss ? $", packet loss is {snapshot.PacketLoss}" : "") +
                          ". Poor RF conditions cause the adapter to fall back to lower MCS indices, reducing throughput and increasing packet loss.";

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Correlation,
            severity:       severity,
            title:          "Signal Degradation",
            description:    description,
            recommendation: "Improve RF coverage: move closer to the AP, eliminate physical obstructions, or deploy an additional access point. " +
                            "Check AP transmit power settings. Consider roaming to a closer BSSID.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.Rssi,         snapshot.Rssi.ToString() },
                { EvidenceKeys.SignalQuality, snapshot.SignalQuality.ToString() },
                { EvidenceKeys.TxRate,        snapshot.TxRate.ToString() },
                { EvidenceKeys.RxRate,        snapshot.RxRate.ToString() },
                { EvidenceKeys.PacketLoss,    snapshot.PacketLoss.ToString() },
                { EvidenceKeys.Band,          snapshot.Band.ToDisplayString() }
            });
    }
}
