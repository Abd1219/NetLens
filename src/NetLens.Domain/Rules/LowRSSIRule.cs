using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when RSSI falls below the acceptable threshold for a stable WiFi connection.
/// Low RSSI is a root cause of reduced PHY rate, packet loss, and roaming events.
///
/// Thresholds (industry standard for enterprise use):
///   Warning  : RSSI &lt;= -75 dBm
///   Critical : RSSI &lt;= -85 dBm
///
/// Confidence scales with how far below the threshold the measurement falls:
///   Marginal (within 3 dBm of threshold) → Medium
///   Clearly below threshold              → High
///   Critical threshold exceeded           → VeryHigh
/// </summary>
public sealed class LowRSSIRule : IDiagnosticRule
{
    private const int WarningThresholdDbm  = -75;
    private const int CriticalThresholdDbm = -85;

    public string RuleCode => "LOW_RSSI";
    public string Name     => "Low Signal Strength (RSSI)";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var rssi = snapshot.Rssi.Value;

        if (rssi > WarningThresholdDbm)
            return null;

        var severity = rssi <= CriticalThresholdDbm
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        var description = rssi <= CriticalThresholdDbm
            ? $"Signal strength is critically low at {snapshot.Rssi}. The connection is at risk of dropping."
            : $"Signal strength is below the acceptable threshold at {snapshot.Rssi}. Throughput and stability may be affected.";

        // Confidence: higher the further below the threshold
        var delta = WarningThresholdDbm - rssi; // positive = how many dBm below threshold
        var confidence = delta switch
        {
            <= 3  => DiagnosticConfidence.Medium,
            <= 10 => DiagnosticConfidence.High,
            _     => DiagnosticConfidence.VeryHigh
        };

        return new DiagnosticResult(
            ruleCode:       RuleCode,
            category:       DiagnosticCategory.RF,
            severity:       severity,
            title:          "Low Signal Strength",
            description:    description,
            recommendation: "Move the device closer to the Access Point or add an additional AP to improve coverage. " +
                            "Verify AP antenna orientation and check for physical obstructions.",
            confidence:     confidence,
            evidence: new Dictionary<string, string>
            {
                { EvidenceKeys.Rssi,              snapshot.Rssi.ToString() },
                { EvidenceKeys.SignalQuality,      snapshot.SignalQuality.ToString() },
                { EvidenceKeys.WarningThreshold,  $"{WarningThresholdDbm} dBm" },
                { EvidenceKeys.CriticalThreshold, $"{CriticalThresholdDbm} dBm" },
                { EvidenceKeys.Ssid,              snapshot.Ssid },
                { EvidenceKeys.Bssid,             snapshot.Bssid.Value }
            });
    }
}
