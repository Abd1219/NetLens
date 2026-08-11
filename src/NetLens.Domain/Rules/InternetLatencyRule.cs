using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when end-to-end internet latency is abnormally high.
/// This measures the round-trip time beyond the local gateway to an external reference host.
///
/// Unlike GatewayLatencyRule (which diagnoses local network issues),
/// this rule targets ISP or upstream congestion.
///
/// Confidence is modulated by the gateway latency context:
///   - If gateway latency is also high, ISP diagnosis has lower confidence
///     (local network might be the bottleneck).
///   - If gateway is healthy, ISP congestion is the more probable cause.
///
/// Thresholds:
///   Warning  : &gt;= 80 ms
///   Critical : &gt;= 200 ms or Timeout
/// </summary>
public sealed class InternetLatencyRule : IDiagnosticRule
{
    private const double WarningThresholdMs  = 80.0;
    private const double CriticalThresholdMs = 200.0;

    public string RuleCode => "HIGH_INTERNET_LATENCY";
    public string Name     => "High Internet Latency";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var internet = snapshot.InternetLatency;
        var gateway  = snapshot.GatewayLatency;

        if (internet.IsTimeout)
        {
            // If gateway is also unreachable, this is redundant with GatewayLatencyRule.
            // Emit with lower confidence so the gateway diagnosis takes precedence.
            if (gateway.IsTimeout)
            {
                return new DiagnosticResult(
                    ruleCode:       RuleCode,
                    category:       DiagnosticCategory.Connectivity,
                    severity:       DiagnosticSeverity.Critical,
                    title:          "Internet Unreachable",
                    description:    "No response from the internet reference host. This is consistent with a complete local connectivity loss (gateway is also unreachable).",
                    recommendation: "Restore local connectivity first. Check gateway and ISP link status.",
                    confidence:     DiagnosticConfidence.Low, // gateway rule covers this better
                    evidence: new Dictionary<string, string>
                    {
                        { EvidenceKeys.InternetLatency, "Timeout" },
                        { EvidenceKeys.GatewayLatency,  "Timeout" }
                    });
            }

            return new DiagnosticResult(
                ruleCode:       RuleCode,
                category:       DiagnosticCategory.Connectivity,
                severity:       DiagnosticSeverity.Critical,
                title:          "Internet Unreachable",
                description:    "No response from the internet reference host. The local gateway is reachable, suggesting an ISP or upstream routing issue.",
                recommendation: "Check the ISP service status. Verify the modem/router WAN link. Contact the ISP if the issue persists.",
                confidence:     DiagnosticConfidence.High,
                evidence: new Dictionary<string, string>
                {
                    { EvidenceKeys.InternetLatency, "Timeout" },
                    { EvidenceKeys.GatewayLatency,  gateway.ToString() }
                });
        }

        if (internet.Milliseconds < WarningThresholdMs)
            return null;

        var severity = internet.Milliseconds >= CriticalThresholdMs
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        // If gateway latency is also high, the bottleneck might be local — reduce confidence.
        bool gatewayAlsoHigh = !gateway.IsTimeout && gateway.Milliseconds >= 20.0;
        var confidence = (severity == DiagnosticSeverity.Critical, gatewayAlsoHigh) switch
        {
            (true,  false) => DiagnosticConfidence.High,
            (true,  true)  => DiagnosticConfidence.Medium,
            (false, false) => DiagnosticConfidence.Medium,
            (false, true)  => DiagnosticConfidence.Low
        };

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Connectivity,
            severity:       severity,
            title:          "High Internet Latency",
            description:    $"Internet round-trip latency is {internet}. " +
                            (gatewayAlsoHigh
                                ? "Local gateway latency is also elevated, so the congestion may be local or upstream."
                                : "The local gateway is healthy, suggesting ISP or upstream congestion."),
            recommendation: "Check the ISP service status. If only affecting certain services, check the CDN or server latency. " +
                            "Consider running a traceroute to identify the congested hop.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.InternetLatency,  internet.ToString() },
                { EvidenceKeys.GatewayLatency,   gateway.ToString() },
                { EvidenceKeys.WarningThreshold, $"{WarningThresholdMs} ms" }
            });
    }
}
