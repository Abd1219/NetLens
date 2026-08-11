using CommunityToolkit.Mvvm.ComponentModel;
using NetLens.Domain.Rules;
using NetLens.UI.Services;

namespace NetLens.UI.Models;

/// <summary>
/// UI Display wrapper around <see cref="DiagnosticResult"/>.
/// Dynamically updates localized presentation strings (Title, Description, Recommendation, SeverityText)
/// when <see cref="LocalizationService.LanguageChanged"/> fires without re-evaluating diagnostic rules.
/// Technical metrics, values, IPs, BSSID, SSID, and rule codes remain strictly intact.
/// </summary>
public partial class DiagnosticResultDisplay : ObservableObject
{
    private readonly LocalizationService _loc;

    public DiagnosticResult UnderlyingResult { get; }

    public string RuleCode => UnderlyingResult.RuleCode;
    public DiagnosticSeverity Severity => UnderlyingResult.Severity;
    public DiagnosticCategory Category => UnderlyingResult.Category;
    public DiagnosticConfidence Confidence => UnderlyingResult.Confidence;
    public IReadOnlyDictionary<string, string> Evidence => UnderlyingResult.Evidence;
    public DateTimeOffset Timestamp => UnderlyingResult.Timestamp;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _recommendation = string.Empty;
    [ObservableProperty] private string _severityText = string.Empty;

    public DiagnosticResultDisplay(DiagnosticResult result, LocalizationService loc)
    {
        UnderlyingResult = result;
        _loc = loc;

        _loc.LanguageChanged += RefreshLocalization;
        RefreshLocalization();
    }

    public void RefreshLocalization()
    {
        // Localize severity label
        SeverityText = Severity switch
        {
            DiagnosticSeverity.Good => _loc.GetString("Severity_Good"),
            DiagnosticSeverity.Info => _loc.GetString("Severity_Info"),
            DiagnosticSeverity.Warning => _loc.GetString("Severity_Warning"),
            DiagnosticSeverity.Critical => _loc.GetString("Severity_Critical"),
            _ => Severity.ToString()
        };

        // Title / Summary lookup by rule code
        var titleKey = $"Rule_{RuleCode}_Title";
        var localizedTitle = _loc.GetString(titleKey);
        Title = localizedTitle != titleKey ? localizedTitle : UnderlyingResult.Title;
        Summary = Title; // Maps to {Binding Summary} in XAML

        // Description lookup
        var descKey = $"Rule_{RuleCode}_Description";
        var localizedDesc = _loc.GetString(descKey);
        Description = localizedDesc != descKey ? localizedDesc : UnderlyingResult.Description;

        // Recommendation lookup
        var recKey = $"Rule_{RuleCode}_Recommendation";
        var localizedRec = _loc.GetString(recKey);
        Recommendation = localizedRec != recKey ? localizedRec : UnderlyingResult.Recommendation;
    }
}
