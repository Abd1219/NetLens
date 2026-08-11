using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Domain.Rules;

/// <summary>
/// Detects probable channel interference by correlating:
///   - Good or adequate RSSI (LOW_RSSI did NOT fire, or RSSI > -70 dBm)
///   - Low PHY rate despite good signal (LOW_PHY_RATE fired)
///   - High jitter (HIGH_JITTER fired) or high packet loss
/// </summary>
public sealed class PossibleInterferenceRule : ICorrelationRule
{
    public string RuleCode => "POSSIBLE_INTERFERENCE";
    public string Name     => "Possible Channel Interference";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot, IReadOnlyList<DiagnosticResult> atomicResults)
    {
        bool hasLowRssi    = atomicResults.Any(r => r.RuleCode == "LOW_RSSI");
        bool hasLowPhyRate = atomicResults.Any(r => r.RuleCode == "LOW_PHY_RATE");
        bool hasHighJitter = atomicResults.Any(r => r.RuleCode == "HIGH_JITTER");
        bool hasPacketLoss = atomicResults.Any(r => r.RuleCode == "HIGH_PACKET_LOSS");

        if (hasLowRssi)
            return null;

        if (!hasLowPhyRate)
            return null;

        if (!hasHighJitter && !hasPacketLoss)
            return null;

        if (snapshot.Rssi.Value <= -70)
            return null;

        int confirming = (hasHighJitter ? 1 : 0) + (hasPacketLoss ? 1 : 0);
        var confidence = confirming >= 2
            ? DiagnosticConfidence.High
            : DiagnosticConfidence.Medium;

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Correlation,
            severity:       DiagnosticSeverity.Warning,
            title:          "Possible Channel Interference",
            description:    $"Signal strength is adequate ({snapshot.Rssi}) but PHY rate is lower than expected for this band " +
                            $"({snapshot.TxRate} Tx), and jitter/packet loss is elevated. This pattern is consistent with " +
                            "co-channel or adjacent-channel interference from nearby networks or devices.",
            recommendation: "Use a WiFi scanner to identify congested channels. Switch to a less-occupied channel or enable " +
                            "channel auto-selection on the AP. On 5 GHz, consider enabling DFS channels. " +
                            "Check for 2.4 GHz interference sources such as microwave ovens, Bluetooth, or other wireless devices.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.Rssi,         snapshot.Rssi.ToString() },
                { EvidenceKeys.SignalQuality, snapshot.SignalQuality.ToString() },
                { EvidenceKeys.TxRate,        snapshot.TxRate.ToString() },
                { EvidenceKeys.RxRate,        snapshot.RxRate.ToString() },
                { EvidenceKeys.Jitter,        snapshot.Jitter.ToString() },
                { EvidenceKeys.PacketLoss,    snapshot.PacketLoss.ToString() },
                { EvidenceKeys.Channel,       snapshot.Channel.ToString() },
                { EvidenceKeys.Band,          snapshot.Band.ToDisplayString() }
            });
    }
}
