using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when network jitter exceeds thresholds that would affect real-time communications
/// such as VoIP, video conferencing, and online gaming.
/// </summary>
public sealed class HighJitterRule : IDiagnosticRule
{
    private const double WarningThresholdMs = 15.0;
    private const double CriticalThresholdMs = 50.0;

    public string RuleCode => "HIGH_JITTER";
    public string Name => "High Network Jitter";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var jitter = snapshot.Jitter.Milliseconds;

        if (jitter < WarningThresholdMs)
            return null;

        var severity = jitter >= CriticalThresholdMs
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        return new DiagnosticResult(
            RuleCode,
            $"Network jitter is {snapshot.Jitter}, which will cause audio and video quality issues in real-time communication applications.",
            "High jitter often correlates with channel interference or an overloaded AP. " +
            "Check for co-channel interference. Prioritize real-time traffic with QoS on the router.",
            severity,
            new Dictionary<string, string>
            {
                { "Jitter", snapshot.Jitter.ToString() },
                { "Category", snapshot.Jitter.Category.ToString() },
                { "PacketLoss", snapshot.PacketLoss.ToString() },
                { "Channel", snapshot.Channel.ToString() }
            });
    }
}
