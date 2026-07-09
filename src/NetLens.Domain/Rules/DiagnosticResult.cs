using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Severity classification of a triggered diagnostic rule.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Represents the result of evaluating a single IDiagnosticRule against a WirelessSnapshot.
/// Immutable and fully explainable — no black-box reasoning.
/// </summary>
public sealed record DiagnosticResult
{
    /// <summary>
    /// Unique code identifying the rule that fired. E.g., "LOW_RSSI", "HIGH_PACKET_LOSS".
    /// </summary>
    public string RuleCode { get; init; }

    /// <summary>
    /// Human-readable summary of what was detected.
    /// </summary>
    public string Summary { get; init; }

    /// <summary>
    /// Actionable recommendation for the technician.
    /// </summary>
    public string Recommendation { get; init; }

    /// <summary>
    /// Severity level of the violation.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Key metric values that triggered this result, for report inclusion.
    /// </summary>
    public IReadOnlyDictionary<string, string> Evidence { get; init; }

    public DateTimeOffset DetectedAt { get; } = DateTimeOffset.UtcNow;

    public DiagnosticResult(
        string ruleCode,
        string summary,
        string recommendation,
        DiagnosticSeverity severity,
        IReadOnlyDictionary<string, string>? evidence = null)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
            throw new ArgumentException("Rule code is required.", nameof(ruleCode));

        RuleCode = ruleCode;
        Summary = summary;
        Recommendation = recommendation;
        Severity = severity;
        Evidence = evidence ?? new Dictionary<string, string>();
    }
}
