using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Severity classification of a triggered diagnostic rule.
/// Values are ordered from least to most severe so CompareTo-based sorting works correctly.
/// Good &lt; Info &lt; Warning &lt; Critical
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>The evaluated metric is within optimal parameters.</summary>
    Good,

    /// <summary>Informational observation; no impact on connectivity.</summary>
    Info,

    /// <summary>Moderate degradation; may affect users under load.</summary>
    Warning,

    /// <summary>Severe problem; connectivity is at risk or lost.</summary>
    Critical
}

/// <summary>
/// Represents the result of evaluating a single IDiagnosticRule (or correlation rule) against a WirelessSnapshot.
/// Immutable and fully explainable — no black-box reasoning. Every result carries its evidence.
/// </summary>
public sealed record DiagnosticResult
{
    /// <summary>
    /// Unique identifier for this specific diagnostic result instance.
    /// </summary>
    public Guid DiagnosticId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Unique code identifying the rule that fired. E.g., "LOW_RSSI", "SIGNAL_DEGRADATION".
    /// </summary>
    public string RuleCode { get; init; }

    /// <summary>
    /// Category grouping: RF, Network, Connectivity, System, Correlation.
    /// </summary>
    public DiagnosticCategory Category { get; init; }

    /// <summary>
    /// Severity level: Good | Info | Warning | Critical.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Short human-readable title. E.g., "Low Signal Strength".
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Technical description explaining what was detected and why it matters.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Key metric values that triggered this result. Used in reports and UI detail views.
    /// Keys follow the EvidenceKeys constants.
    /// </summary>
    public IReadOnlyDictionary<string, string> Evidence { get; init; }

    /// <summary>
    /// Confidence level (0–100) in this diagnosis, determined deterministically from evidence quality.
    /// </summary>
    public DiagnosticConfidence Confidence { get; init; }

    /// <summary>
    /// Actionable recommendation for the technician.
    /// </summary>
    public string Recommendation { get; init; }

    /// <summary>
    /// UTC timestamp when this diagnostic result was produced.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public DiagnosticResult(
        string ruleCode,
        DiagnosticCategory category,
        DiagnosticSeverity severity,
        string title,
        string description,
        string recommendation,
        DiagnosticConfidence confidence,
        IReadOnlyDictionary<string, string>? evidence = null)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
            throw new ArgumentException("Rule code is required.", nameof(ruleCode));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        RuleCode       = ruleCode;
        Category       = category;
        Severity       = severity;
        Title          = title;
        Description    = description;
        Recommendation = recommendation;
        Confidence     = confidence;
        Evidence       = evidence ?? new Dictionary<string, string>();
    }
}
