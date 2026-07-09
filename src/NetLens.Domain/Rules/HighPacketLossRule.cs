using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when packet loss rate exceeds acceptable thresholds.
/// High packet loss causes TCP retransmissions, VoIP call drops, and poor application performance.
/// </summary>
public sealed class HighPacketLossRule : IDiagnosticRule
{
    private const double WarningThresholdPct = 3.0;
    private const double CriticalThresholdPct = 10.0;

    public string RuleCode => "HIGH_PACKET_LOSS";
    public string Name => "High Packet Loss";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var loss = snapshot.PacketLoss.Percentage;

        if (loss < WarningThresholdPct)
            return null;

        var severity = loss >= CriticalThresholdPct
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        return new DiagnosticResult(
            RuleCode,
            $"Packet loss is {snapshot.PacketLoss} which degrades application reliability and real-time communication.",
            "Check for RF interference on the current channel. Evaluate switching to a less congested " +
            "channel or 5 GHz band. Verify the wireless driver is up to date.",
            severity,
            new Dictionary<string, string>
            {
                { "PacketLoss", snapshot.PacketLoss.ToString() },
                { "Category", snapshot.PacketLoss.Category.ToString() },
                { "WarningThreshold", $"{WarningThresholdPct}%" },
                { "CriticalThreshold", $"{CriticalThresholdPct}%" },
                { "GatewayLatency", snapshot.GatewayLatency.ToString() }
            });
    }
}
