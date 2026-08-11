using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Detects partial connectivity failure where local gateway is reachable but DNS resolution fails.
/// </summary>
public sealed class ConnectivityPartialRule : ICorrelationRule
{
    public string RuleCode => "CONNECTIVITY_PARTIAL";
    public string Name     => "Partial Connectivity Loss (DNS Unreachable)";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot, IReadOnlyList<DiagnosticResult> atomicResults)
    {
        bool gatewayTimeout = snapshot.GatewayLatency.IsTimeout;
        bool dnsTimeout     = snapshot.DnsLatency.IsTimeout;

        if (gatewayTimeout || !dnsTimeout)
            return null;

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Correlation,
            severity:       DiagnosticSeverity.Critical,
            title:          "Partial Connectivity Failure (DNS Down)",
            description:    $"Local gateway ({snapshot.GatewayIp}) is responsive ({snapshot.GatewayLatency}), but DNS server ({snapshot.DnsIp}) is completely unreachable. Local Wi-Fi connection is established, but web browsing and domain resolution will fail.",
            recommendation: "Verify DNS server configuration on the router or network adapter. Test switching to an alternative public DNS provider (e.g. 1.1.1.1 or 8.8.8.8) to restore internet browsing.",
            confidence:     DiagnosticConfidence.VeryHigh,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.GatewayIp,       snapshot.GatewayIp.Value },
                { EvidenceKeys.GatewayLatency,  snapshot.GatewayLatency.ToString() },
                { EvidenceKeys.DnsServer,       snapshot.DnsIp.Value },
                { EvidenceKeys.DnsLatency,       "Timeout" }
            });
    }
}
