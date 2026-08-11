using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when packet loss rate exceeds acceptable thresholds.
/// High packet loss causes TCP retransmissions, VoIP call drops, and poor application performance.
///
/// Thresholds:
///   Warning  : &gt;= 3%
///   Critical : &gt;= 10%
/// </summary>
public sealed class HighPacketLossRule : IDiagnosticRule
{
    private const double WarningThresholdPct  = 3.0;
    private const double CriticalThresholdPct = 10.0;

    public string RuleCode => "HIGH_PACKET_LOSS";
    public string Name     => "High Packet Loss";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var loss = snapshot.PacketLoss.Percentage;

        if (loss < WarningThresholdPct)
            return null;

        var severity = loss >= CriticalThresholdPct
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        var confidence = loss >= CriticalThresholdPct
            ? DiagnosticConfidence.VeryHigh
            : DiagnosticConfidence.High;

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Network,
            severity:       severity,
            title:          "High Packet Loss",
            description:    $"Packet loss is {snapshot.PacketLoss}, which degrades application reliability and real-time communication.",
            recommendation: "Check for RF interference on the current channel. Evaluate switching to a less congested " +
                            "channel or 5 GHz band. Verify the wireless driver is up to date.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.PacketLoss,        snapshot.PacketLoss.ToString() },
                { "Category",                     snapshot.PacketLoss.Category.ToString() },
                { EvidenceKeys.WarningThreshold,  $"{WarningThresholdPct}%" },
                { EvidenceKeys.CriticalThreshold, $"{CriticalThresholdPct}%" },
                { EvidenceKeys.GatewayLatency,    snapshot.GatewayLatency.ToString() }
            });
    }
}
