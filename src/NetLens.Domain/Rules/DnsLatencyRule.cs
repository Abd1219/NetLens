using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when DNS resolution latency is abnormally high or DNS is unresponsive.
/// Slow DNS causes significant user-perceived latency for all web application traffic.
/// </summary>
public sealed class DnsLatencyRule : IDiagnosticRule
{
    private const double WarningThresholdMs = 50.0;
    private const double CriticalThresholdMs = 200.0;

    public string RuleCode => "DNS_SLOW";
    public string Name => "Slow DNS Resolution";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var latency = snapshot.DnsLatency;

        if (latency.IsTimeout)
        {
            return new DiagnosticResult(
                RuleCode,
                "DNS server is not responding. All domain name resolution will fail, affecting all web traffic.",
                "Verify the configured DNS server is reachable. Consider switching to a public DNS server " +
                "such as 8.8.8.8 (Google) or 1.1.1.1 (Cloudflare) as a diagnostic step.",
                DiagnosticSeverity.Critical,
                new Dictionary<string, string>
                {
                    { "DnsServer", snapshot.DnsIp.Value },
                    { "Status", "Timeout" }
                });
        }

        if (latency.Milliseconds < WarningThresholdMs)
            return null;

        var severity = latency.Milliseconds >= CriticalThresholdMs
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        return new DiagnosticResult(
            RuleCode,
            $"DNS resolution latency is {latency}. This will cause noticeable delays when opening web pages and connecting to services.",
            "Check if the current DNS server is overloaded. Test with an alternative DNS server. " +
            "Verify the ISP's DNS infrastructure health.",
            severity,
            new Dictionary<string, string>
            {
                { "DnsLatency", latency.ToString() },
                { "DnsServer", snapshot.DnsIp.Value },
                { "GatewayLatency", snapshot.GatewayLatency.ToString() }
            });
    }
}
