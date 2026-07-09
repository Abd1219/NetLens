using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Domain.Rules;

/// <summary>
/// Fires when RSSI falls below the acceptable threshold for a stable WiFi connection.
/// Low RSSI is a root cause of reduced PHY rate, packet loss, and roaming events.
/// </summary>
public sealed class LowRSSIRule : IDiagnosticRule
{
    // Industry threshold: below -75 dBm is considered poor for enterprise use.
    private const int WarningThresholdDbm = -75;
    private const int CriticalThresholdDbm = -85;

    public string RuleCode => "LOW_RSSI";
    public string Name => "Low Signal Strength (RSSI)";

    public DiagnosticResult? Evaluate(WirelessSnapshot snapshot)
    {
        var rssi = snapshot.Rssi.Value;

        if (rssi > WarningThresholdDbm)
            return null; // Within acceptable range, no violation.

        var severity = rssi <= CriticalThresholdDbm
            ? DiagnosticSeverity.Critical
            : DiagnosticSeverity.Warning;

        var summary = rssi <= CriticalThresholdDbm
            ? $"Signal strength is critically low at {snapshot.Rssi}. Connection is at risk of dropping."
            : $"Signal strength is below acceptable threshold at {snapshot.Rssi}.";

        return new DiagnosticResult(
            RuleCode,
            summary,
            "Move the device closer to the Access Point or add an additional AP to improve coverage. " +
            "Verify AP antenna orientation and check for physical obstructions.",
            severity,
            new Dictionary<string, string>
            {
                { "RSSI", snapshot.Rssi.ToString() },
                { "WarningThreshold", $"{WarningThresholdDbm} dBm" },
                { "CriticalThreshold", $"{CriticalThresholdDbm} dBm" },
                { "SignalQuality", snapshot.SignalQuality.ToString() },
                { "SSID", snapshot.Ssid },
                { "BSSID", snapshot.Bssid.Value }
            });
    }
}
