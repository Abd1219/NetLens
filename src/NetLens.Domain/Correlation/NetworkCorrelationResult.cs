using NetLens.Domain.Rules;

namespace NetLens.Domain.Correlation;

/// <summary>
/// Represents the output of NetworkCorrelationEngine for a single detected pattern.
///
/// IMPORTANT — EvidenceScore semantics:
///   EvidenceScore (0–100) is NOT a probability.
///   It represents the cumulative strength of evidence supporting this correlation,
///   calculated deterministically from thresholds and weights defined in CorrelationThresholds.
///   A score of 80 means "multiple strong indicators align"; it does NOT mean
///   "80% chance this diagnosis is correct."
///
/// EvidenceStrength bands:
///   0–29  → Weak        (suppressed; not returned to callers)
///   30–59 → Moderate    (returned; may indicate a developing issue)
///   60–79 → Strong      (clear multi-metric evidence)
///   80–100→ VeryStrong  (multiple thresholds exceeded with persistence)
///
/// Design for future ML compatibility:
///   ContributingMetrics lists which metrics were significant,
///   enabling future feature extraction pipelines.
///   MetricsSnapshot preserves the raw aggregated values for offline analysis.
/// </summary>
public sealed record NetworkCorrelationResult
{
    /// <summary>The category of correlation pattern detected.</summary>
    public CorrelationType CorrelationType { get; init; }

    /// <summary>
    /// Evidence strength score (0–100), deterministic.
    /// Represents cumulative weight of supporting indicators.
    /// See class documentation for interpretation.
    /// </summary>
    public int EvidenceScore { get; init; }

    /// <summary>Banded interpretation of EvidenceScore.</summary>
    public EvidenceStrength EvidenceStrength { get; init; }

    /// <summary>
    /// Severity classification reusing the existing domain enum.
    /// Determined by the combination of correlation type and evidence score.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Key-value pairs of the specific metric values that triggered this correlation.
    /// Keys follow CorrelationEvidenceKeys constants.
    /// Values are always formatted technical strings (e.g., "-76 dBm", "42.3 ms").
    /// Never localized here — localization is the UI layer's responsibility.
    /// </summary>
    public IReadOnlyDictionary<string, string> Evidence { get; init; }

    /// <summary>
    /// Names of the metrics that contributed points to the EvidenceScore.
    /// Suitable for future ML feature extraction.
    /// </summary>
    public IReadOnlyList<string> ContributingMetrics { get; init; }

    /// <summary>
    /// Short technical description in English for logging purposes.
    /// The UI must NOT display this directly; instead use CorrelationType to look up
    /// localized strings from the resource file.
    /// </summary>
    public string TechnicalDescription { get; init; }

    /// <summary>
    /// Short technical recommendation in English for logging purposes.
    /// UI must localize from CorrelationType.
    /// </summary>
    public string TechnicalRecommendation { get; init; }

    /// <summary>
    /// The summarized metrics window used as input for this analysis.
    /// Preserved for future ML feature extraction and offline debugging.
    /// </summary>
    public NetworkMetricsWindow MetricsSnapshot { get; init; }

    /// <summary>UTC timestamp when this correlation result was produced.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public NetworkCorrelationResult(
        CorrelationType correlationType,
        int evidenceScore,
        DiagnosticSeverity severity,
        IReadOnlyDictionary<string, string> evidence,
        IReadOnlyList<string> contributingMetrics,
        string technicalDescription,
        string technicalRecommendation,
        NetworkMetricsWindow metricsSnapshot)
    {
        CorrelationType        = correlationType;
        EvidenceScore          = Math.Clamp(evidenceScore, 0, 100);
        EvidenceStrength       = ClassifyScore(EvidenceScore);
        Severity               = severity;
        Evidence               = evidence;
        ContributingMetrics    = contributingMetrics;
        TechnicalDescription   = technicalDescription;
        TechnicalRecommendation = technicalRecommendation;
        MetricsSnapshot        = metricsSnapshot;
    }

    private static EvidenceStrength ClassifyScore(int score) => score switch
    {
        >= 80 => EvidenceStrength.VeryStrong,
        >= 60 => EvidenceStrength.Strong,
        >= 30 => EvidenceStrength.Moderate,
        _     => EvidenceStrength.Weak
    };
}

/// <summary>
/// Banded interpretation of an EvidenceScore.
/// Displayed in the UI to give the technician a quick qualitative read.
/// </summary>
public enum EvidenceStrength
{
    /// <summary>Score 0–29. Suppressed by the engine; not reported.</summary>
    Weak,

    /// <summary>Score 30–59. Developing pattern; worth monitoring.</summary>
    Moderate,

    /// <summary>Score 60–79. Clear multi-metric evidence of the pattern.</summary>
    Strong,

    /// <summary>Score 80–100. Multiple thresholds exceeded with persistence.</summary>
    VeryStrong
}
