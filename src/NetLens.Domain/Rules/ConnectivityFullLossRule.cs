using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Domain.Rules;

/// <summary>
/// Detects total network connectivity loss (Gateway + DNS both timeout).
/// </summary>
public sealed class ConnectivityFullLossRule : ICorrelationRule
{
    public string RuleCode => "CONNECTIVITY_FULL_LOSS";
    public string Name     => "Full Local Connectivity Loss";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot, IReadOnlyList<DiagnosticResult> atomicResults)
    {
        bool gatewayTimeout = snapshot.GatewayLatency.IsTimeout;
        bool dnsTimeout     = snapshot.DnsLatency.IsTimeout;

        if (!gatewayTimeout || !dnsTimeout)
            return null;

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.Correlation,
            severity:       DiagnosticSeverity.Critical,
            title:          "Total Network Connectivity Loss",
            description:    $"Neither local gateway ({snapshot.GatewayIp}) nor DNS server ({snapshot.DnsIp}) are responding to network probes. The wireless link may be associated, but network layer communication is broken.",
            recommendation: "Verify local network infrastructure. Check if default gateway or router has power and active LAN link. Re-associate with Wi-Fi AP or verify IP address/DHCP lease configuration.",
            confidence:     DiagnosticConfidence.Certain,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.GatewayIp,       snapshot.GatewayIp.Value },
                { EvidenceKeys.GatewayLatency,  "Timeout" },
                { EvidenceKeys.DnsServer,       snapshot.DnsIp.Value },
                { EvidenceKeys.DnsLatency,       "Timeout" },
                { EvidenceKeys.ConnectionState, snapshot.ConnectionState.ToDisplayString() }
            });
    }
}
