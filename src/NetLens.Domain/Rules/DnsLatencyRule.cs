using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when DNS resolution latency is abnormally high or DNS is unresponsive.
/// Slow DNS causes significant user-perceived latency for all web application traffic.
///
/// Thresholds:
///   Warning  : &gt;= 50 ms
///   Critical : &gt;= 200 ms or Timeout
/// </summary>
public sealed class DnsLatencyRule : IDiagnosticRule
{
    private const double WarningThresholdMs  = 50.0;
    private const double CriticalThresholdMs = 200.0;

    public string RuleCode => "DNS_SLOW";
    public string Name     => "Slow DNS Resolution";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var latency = snapshot.DnsLatency;

        if (latency.IsTimeout)
        {
            return new DiagnosticResult(
                ruleCode:       RuleCode,
                category:       DiagnosticCategory.Connectivity,
                severity:       DiagnosticSeverity.Critical,
                title:          "DNS Server Unreachable",
                description:    "The DNS server is not responding. All domain name resolution will fail, affecting all web traffic.",
                recommendation: "Verify the configured DNS server is reachable. Consider switching to a public DNS server " +
                                "such as 8.8.8.8 (Google) or 1.1.1.1 (Cloudflare) as a diagnostic step.",
                confidence:     DiagnosticConfidence.VeryHigh,
                evidence: new Dictionary<string, string>
                {
                    { EvidenceKeys.DnsServer,  snapshot.DnsIp.Value },
                    { "Status",               "Timeout" }
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
            category:       DiagnosticCategory.Connectivity,
            severity:       severity,
            title:          "Slow DNS Resolution",
            description:    $"DNS resolution latency is {latency}. This causes noticeable delays when opening web pages and connecting to services.",
            recommendation: "Check if the current DNS server is overloaded. Test with an alternative DNS server. " +
                            "Verify the ISP's DNS infrastructure health.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.DnsLatency,       latency.ToString() },
                { EvidenceKeys.DnsServer,        snapshot.DnsIp.Value },
                { EvidenceKeys.GatewayLatency,   snapshot.GatewayLatency.ToString() }
            });
    }
}
