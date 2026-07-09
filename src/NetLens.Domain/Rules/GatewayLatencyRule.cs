using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when latency to the default gateway is abnormally high.
/// Gateway latency spikes indicate local network congestion, router overload, or bufferbloat.
/// </summary>
public sealed class GatewayLatencyRule : IDiagnosticRule
{
    private const double WarningThresholdMs = 20.0;
    private const double CriticalThresholdMs = 100.0;

    public string RuleCode => "HIGH_GATEWAY_LATENCY";
    public string Name => "High Gateway Latency";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var latency = snapshot.GatewayLatency;

        if (latency.IsTimeout)
        {
            return new DiagnosticResult(
                RuleCode,
                "Gateway is not responding to ICMP probes. Local connectivity may be lost.",
                "Verify the router is online. Check for DHCP configuration issues. " +
                "Try releasing and renewing the IP address.",
                DiagnosticSeverity.Critical,
                new Dictionary<string, string>
                {
                    { "GatewayLatency", "Timeout" },
                    { "GatewayIP", snapshot.GatewayIp.Value }
                });
        }

        if (latency.Milliseconds < WarningThresholdMs)
            return null;

        var severity = latency.Milliseconds >= CriticalThresholdMs
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        return new DiagnosticResult(
            RuleCode,
            $"Gateway round-trip latency is {latency}, which may indicate local network congestion or router overload.",
            "Check the number of active connections on the router. Verify QoS settings. " +
            "Look for bandwidth-heavy processes on the local network.",
            severity,
            new Dictionary<string, string>
            {
                { "GatewayLatency", latency.ToString() },
                { "GatewayIP", snapshot.GatewayIp.Value },
                { "WarningThreshold", $"{WarningThresholdMs} ms" }
            });
    }
}
