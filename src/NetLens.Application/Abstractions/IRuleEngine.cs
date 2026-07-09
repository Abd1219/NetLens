using NetLens.Domain.Entities;
using NetLens.Domain.Rules;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Orchestrates evaluation of all registered rules against a snapshot.
/// Returns all violated rules sorted by severity.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Evaluates all registered rules against a WirelessSnapshot.
    /// Returns only the rules that fired (non-null results).
    /// </summary>
    IReadOnlyList<DiagnosticResult> Evaluate(WirelessSnapshot snapshot);
}
