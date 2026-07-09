using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Contract for a single, independently testable diagnostic rule.
/// Each rule has one responsibility: analyze a snapshot and determine if a violation exists.
/// </summary>
public interface IDiagnosticRule
{
    /// <summary>
    /// Unique code identifying this rule. E.g., "LOW_RSSI".
    /// </summary>
    string RuleCode { get; }

    /// <summary>
    /// Human-readable name for this rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the rule against the provided snapshot.
    /// Returns a DiagnosticResult if the rule fires, or null if the snapshot is within acceptable thresholds.
    /// </summary>
    DiagnosticResult? Evaluate(WirelessSnapshot snapshot);
}
