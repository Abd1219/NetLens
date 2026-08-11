using NetLens.Domain.Entities;
using NetLens.Domain.Rules;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Abstraction for the core Diagnostic Service.
/// Coordinates atomic rule evaluation, correlation analysis, and conflict suppression against a snapshot,
/// producing a cohesive list of DiagnosticResult instances.
/// </summary>
public interface IDiagnosticService
{
    /// <summary>
    /// Evaluates all registered atomic rules and correlation rules against the snapshot,
    /// performs conflict suppression, and returns the final prioritized list of diagnostic results.
    /// </summary>
    IReadOnlyList<DiagnosticResult> AnalyzeSnapshot(WirelessSnapshot snapshot);
}
