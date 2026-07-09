using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Rules;

namespace NetLens.Application.Services;

/// <summary>
/// Evaluates all registered IDiagnosticRule implementations against a snapshot.
/// Results are sorted by severity (Critical first) to prioritize the most impactful findings.
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private readonly IReadOnlyList<IDiagnosticRule> _rules;

    // Using IEnumerable<IDiagnosticRule> allows DI to inject all registered rules automatically.
    public RuleEngine(IEnumerable<IDiagnosticRule> rules)
    {
        _rules = [.. rules];
    }

    public IReadOnlyList<DiagnosticResult> Evaluate(WirelessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var results = new List<DiagnosticResult>();

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(snapshot);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        // Sort by severity: Critical first, then Warning, then Info
        results.Sort((a, b) => b.Severity.CompareTo(a.Severity));

        return results.AsReadOnly();
    }
}
