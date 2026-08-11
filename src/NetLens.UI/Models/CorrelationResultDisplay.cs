using CommunityToolkit.Mvvm.ComponentModel;
using NetLens.Domain.Correlation;
using NetLens.Domain.Rules;
using NetLens.UI.Services;
using System.Text;

namespace NetLens.UI.Models;

/// <summary>
/// UI Display wrapper around <see cref="NetworkCorrelationResult"/>.
/// Dynamically updates localized presentation strings when language changes,
/// without modifying technical metrics, values, or scores.
/// </summary>
public partial class CorrelationResultDisplay : ObservableObject
{
    private readonly LocalizationService _loc;

    public NetworkCorrelationResult UnderlyingResult { get; }

    public CorrelationType CorrelationType => UnderlyingResult.CorrelationType;
    public int EvidenceScore => UnderlyingResult.EvidenceScore;
    public EvidenceStrength EvidenceStrength => UnderlyingResult.EvidenceStrength;
    public DiagnosticSeverity Severity => UnderlyingResult.Severity;
    public IReadOnlyDictionary<string, string> Evidence => UnderlyingResult.Evidence;
    public IReadOnlyList<string> ContributingMetrics => UnderlyingResult.ContributingMetrics;
    public DateTimeOffset Timestamp => UnderlyingResult.Timestamp;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _recommendation = string.Empty;
    [ObservableProperty] private string _severityText = string.Empty;
    [ObservableProperty] private string _strengthText = string.Empty;
    [ObservableProperty] private string _evidenceScoreLabel = string.Empty;
    [ObservableProperty] private string _contributingMetricsLabel = string.Empty;

    // Bullet-point evidence list for easy display
    [ObservableProperty] private List<string> _evidenceItems = [];

    public CorrelationResultDisplay(NetworkCorrelationResult result, LocalizationService loc)
    {
        UnderlyingResult = result;
        _loc = loc;

        _loc.LanguageChanged += RefreshLocalization;
        RefreshLocalization();
    }

    public void RefreshLocalization()
    {
        // 1. Localize severity label
        SeverityText = Severity switch
        {
            DiagnosticSeverity.Good => _loc.GetString("Severity_Good"),
            DiagnosticSeverity.Info => _loc.GetString("Severity_Info"),
            DiagnosticSeverity.Warning => _loc.GetString("Severity_Warning"),
            DiagnosticSeverity.Critical => _loc.GetString("Severity_Critical"),
            _ => Severity.ToString()
        };

        // 2. Localize strength label
        StrengthText = EvidenceStrength switch
        {
            EvidenceStrength.Moderate => _loc.GetString("EvidenceStrength_Moderate"),
            EvidenceStrength.Strong => _loc.GetString("EvidenceStrength_Strong"),
            EvidenceStrength.VeryStrong => _loc.GetString("EvidenceStrength_VeryStrong"),
            _ => EvidenceStrength.ToString()
        };

        // 3. Labels
        EvidenceScoreLabel = _loc.GetString("Corr_EvidenceScore");
        ContributingMetricsLabel = _loc.GetString("Corr_ContributingMetrics");

        // 4. Lookups by correlation type
        var typeStr = CorrelationType.ToString();
        var titleKey = $"Corr_{typeStr}_Title";
        var descKey = $"Corr_{typeStr}_Desc";
        var recKey = $"Corr_{typeStr}_Rec";

        var localizedTitle = _loc.GetString(titleKey);
        Title = localizedTitle != titleKey ? localizedTitle : localizedTitle;

        var localizedDesc = _loc.GetString(descKey);
        Description = localizedDesc != descKey ? localizedDesc : UnderlyingResult.TechnicalDescription;

        var localizedRec = _loc.GetString(recKey);
        Recommendation = localizedRec != recKey ? localizedRec : UnderlyingResult.TechnicalRecommendation;

        // 5. Populate formatted evidence items (technical strings: don't translate values like "-76 dBm")
        var items = new List<string>();
        foreach (var kvp in Evidence)
        {
            // Skip SampleCount, WindowDuration, EvidenceScore from list
            if (kvp.Key == CorrelationEvidenceKeys.SampleCount ||
                kvp.Key == CorrelationEvidenceKeys.WindowDuration ||
                kvp.Key == CorrelationEvidenceKeys.EvidenceScore)
            {
                continue;
            }

            // Humanize the key name slightly if not mapped, or keep it standard
            var readableKey = kvp.Key switch
            {
                CorrelationEvidenceKeys.RssiAvg => "RSSI Average",
                CorrelationEvidenceKeys.RssiMin => "RSSI Min",
                CorrelationEvidenceKeys.RssiMax => "RSSI Max",
                CorrelationEvidenceKeys.RssiTrend => "RSSI Trend",
                CorrelationEvidenceKeys.LanLatencyAvg => "LAN Latency Avg",
                CorrelationEvidenceKeys.LanLatencyStdDev => "LAN Latency StdDev",
                CorrelationEvidenceKeys.LanJitterAvg => "LAN Jitter Avg",
                CorrelationEvidenceKeys.LanPacketLossAvg => "LAN Packet Loss",
                CorrelationEvidenceKeys.LanPacketLossPersistence => "LAN Loss Persistence",
                CorrelationEvidenceKeys.InternetLatencyAvg => "Internet Latency Avg",
                CorrelationEvidenceKeys.InternetTimeoutRatio => "Internet Timeout Ratio",
                CorrelationEvidenceKeys.PhyRateAvg => "PHY Rate Average",
                CorrelationEvidenceKeys.PhyRateMin => "PHY Rate Min",
                CorrelationEvidenceKeys.GatewayLatencyAvg => "Gateway Latency Avg",
                CorrelationEvidenceKeys.DnsLatencyAvg => "DNS Latency Avg",
                CorrelationEvidenceKeys.DnsTimeoutRatio => "DNS Timeout Ratio",
                _ => kvp.Key
            };

            items.Add($"• {readableKey}: {kvp.Value}");
        }
        EvidenceItems = items;
    }
}
