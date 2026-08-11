using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when latency to the default gateway is abnormally high or the gateway is unreachable.
/// Gateway latency spikes indicate local network congestion, router overload, or bufferbloat.
///
/// Thresholds:
///   Warning  : &gt;= 20 ms
///   Critical : &gt;= 100 ms or Timeout
/// </summary>
public sealed class GatewayLatencyRule : IDiagnosticRule
{
    private const double WarningThresholdMs  = 20.0;
    private const double CriticalThresholdMs = 100.0;

    public string RuleCode => "HIGH_GATEWAY_LATENCY";
    public string Name     => "High Gateway Latency";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var latency = snapshot.GatewayLatency;

        if (latency.IsTimeout)
        {
            return new DiagnosticResult(
                ruleCode:       RuleCode,
                category:       DiagnosticCategory.Network,
                severity:       DiagnosticSeverity.Critical,
                title:          "Gateway Unreachable",
                description:    "The default gateway is not responding to ICMP probes. Local connectivity may be lost.",
                recommendation: "Verify the router is online. Check for DHCP configuration issues. " +
                                "Try releasing and renewing the IP address.",
                confidence:     DiagnosticConfidence.VeryHigh,
                evidence: new Dictionary<string, string>
                {
                    { EvidenceKeys.GatewayLatency, "Timeout" },
                    { EvidenceKeys.GatewayIp,      snapshot.GatewayIp.Value }
                });
        }

        if (latency.Milliseconds < WarningThresholdMs)
            return null;

        var severity = latency.Milliseconds >= CriticalThresholdMs
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        var confidence = latency.Milliseconds >= CriticalThresholdMs
            ? DiagnosticConfidence.High
            : DiagnosticConfidence.Medium;

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Network,
            severity:       severity,
            title:          "High Gateway Latency",
            description:    $"Gateway round-trip latency is {latency}, which may indicate local network congestion or router overload.",
            recommendation: "Check the number of active connections on the router. Verify QoS settings. " +
                            "Look for bandwidth-heavy processes on the local network.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.GatewayLatency,   latency.ToString() },
                { EvidenceKeys.GatewayIp,        snapshot.GatewayIp.Value },
                { EvidenceKeys.WarningThreshold, $"{WarningThresholdMs} ms" }
            });
    }
}
