using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when network jitter exceeds thresholds that affect real-time communications
/// such as VoIP, video conferencing, and online gaming.
///
/// Thresholds:
///   Warning  : &gt;= 15 ms
///   Critical : &gt;= 50 ms
/// </summary>
public sealed class HighJitterRule : IDiagnosticRule
{
    private const double WarningThresholdMs  = 15.0;
    private const double CriticalThresholdMs = 50.0;

    public string RuleCode => "HIGH_JITTER";
    public string Name     => "High Network Jitter";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var jitter = snapshot.Jitter.Milliseconds;

        if (jitter < WarningThresholdMs)
            return null;

        var severity = jitter >= CriticalThresholdMs
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        var confidence = jitter >= CriticalThresholdMs
            ? DiagnosticConfidence.High
            : DiagnosticConfidence.Medium;

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Network,
            severity:       severity,
            title:          "High Network Jitter",
            description:    $"Network jitter is {snapshot.Jitter}, which will cause audio and video quality issues in real-time communication applications.",
            recommendation: "High jitter often correlates with channel interference or an overloaded AP. " +
                            "Check for co-channel interference. Prioritize real-time traffic with QoS on the router.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.Jitter,      snapshot.Jitter.ToString() },
                { "Category",              snapshot.Jitter.Category.ToString() },
                { EvidenceKeys.PacketLoss, snapshot.PacketLoss.ToString() },
                { EvidenceKeys.Channel,    snapshot.Channel.ToString() }
            });
    }
}
