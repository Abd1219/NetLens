using NetLens.Domain.Entities;

namespace NetLens.Domain.Rules;

/// <summary>
/// Contract for a correlation rule that analyzes a WirelessSnapshot in the context
/// of the atomic diagnostic results already produced for that snapshot.
///
/// Correlation rules may:
///   - Combine multiple atomic results to detect a compound root cause.
///   - Suppress conflicting atomic results by returning a higher-confidence composite result.
///   - Return null if the conditions for the correlation are not met.
///
/// Correlation rules run AFTER all atomic rules, receiving both the snapshot
/// and the full list of atomic results for context.
/// </summary>
public interface ICorrelationRule
{
    /// <summary>Unique rule code. E.g., "SIGNAL_DEGRADATION".</summary>
    string RuleCode { get; }

    /// <summary>Human-readable name for logging and reporting.</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the correlation rule.
    /// </summary>
    /// <param name="snapshot">The current wireless snapshot.</param>
    /// <param name="atomicResults">All non-null results from the atomic rule pass.</param>
    /// <returns>
    /// A DiagnosticResult if the correlation pattern is detected; null otherwise.
    /// </returns>
    DiagnosticResult? Evaluate(WirelessSnapshot snapshot, IReadOnlyList<DiagnosticResult> atomicResults);
}
