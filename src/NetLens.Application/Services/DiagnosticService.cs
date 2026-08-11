using Microsoft.Extensions.Logging;
using NetLens.Application.Abstractions;
using NetLens.Domain.Entities;
using NetLens.Domain.Events;
using NetLens.Domain.Rules;

namespace NetLens.Application.Services;

/// <summary>
/// Core application service driving the Diagnostic Engine pipeline:
///   1. Atomic Rules Evaluation (via IRuleEngine)
///   2. Correlation Rules Evaluation (via ICorrelationRule)
///   3. Conflict Suppression (replaces redundant atomic alerts with composite root-cause diagnosis)
///   4. DiagnosticCompletedEvent Publication
/// </summary>
public sealed class DiagnosticService : IDiagnosticService, IEventHandler<TelemetryCollectedEvent>
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IReadOnlyList<ICorrelationRule> _correlationRules;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DiagnosticService> _logger;

    public DiagnosticService(
        IRuleEngine ruleEngine,
        IEnumerable<ICorrelationRule> correlationRules,
        IEventBus eventBus,
        ILogger<DiagnosticService> logger)
    {
        _ruleEngine = ruleEngine;
        _correlationRules = [.. correlationRules];
        _eventBus = eventBus;
        _logger = logger;

        _eventBus.Subscribe(this);
    }

    public async Task HandleAsync(TelemetryCollectedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Snapshot is null)
            return;

        var results = AnalyzeSnapshot(@event.Snapshot);

        _logger.LogInformation(
            "Diagnostic Engine evaluated session {SessionId}: {ResultCount} active diagnostic finding(s).",
            @event.SessionId,
            results.Count);

        await _eventBus.PublishAsync(
            new DiagnosticCompletedEvent(@event.SessionId, @event.Snapshot, results),
            cancellationToken);
    }

    public IReadOnlyList<DiagnosticResult> AnalyzeSnapshot(WirelessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Step 1: Atomic Rules Evaluation
        var atomicResults = _ruleEngine.Evaluate(snapshot);

        // Step 2: Correlation Rules Evaluation
        var correlationResults = new List<DiagnosticResult>();
        foreach (var correlationRule in _correlationRules)
        {
            var result = correlationRule.Evaluate(snapshot, atomicResults);
            if (result is not null)
            {
                correlationResults.Add(result);
            }
        }

        // Step 3: Combine and Apply Conflict Suppression
        var finalResults = ApplyConflictSuppression(atomicResults, correlationResults);

        // Step 4: Sort by Severity (Critical first) then Confidence
        finalResults.Sort((a, b) =>
        {
            int severityCompare = b.Severity.CompareTo(a.Severity);
            if (severityCompare != 0)
                return severityCompare;

            return b.Confidence.Value.CompareTo(a.Confidence.Value);
        });

        return finalResults.AsReadOnly();
    }

    private static List<DiagnosticResult> ApplyConflictSuppression(
        IReadOnlyList<DiagnosticResult> atomic,
        IReadOnlyList<DiagnosticResult> correlation)
    {
        var suppressedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Suppression Rule 1: Full Connectivity Loss suppresses individual Gateway, DNS, and Internet rules
        if (correlation.Any(c => string.Equals(c.RuleCode, "CONNECTIVITY_FULL_LOSS", StringComparison.OrdinalIgnoreCase)))
        {
            suppressedCodes.Add("HIGH_GATEWAY_LATENCY");
            suppressedCodes.Add("DNS_SLOW");
            suppressedCodes.Add("HIGH_INTERNET_LATENCY");
        }

        // Suppression Rule 2: Partial Connectivity Failure suppresses DNS Slow
        if (correlation.Any(c => string.Equals(c.RuleCode, "CONNECTIVITY_PARTIAL", StringComparison.OrdinalIgnoreCase)))
        {
            suppressedCodes.Add("DNS_SLOW");
        }

        // Suppression Rule 3: Composite Signal Degradation suppresses atomic Low RSSI and Low PHY Rate
        if (correlation.Any(c => string.Equals(c.RuleCode, "SIGNAL_DEGRADATION", StringComparison.OrdinalIgnoreCase)))
        {
            suppressedCodes.Add("LOW_RSSI");
            suppressedCodes.Add("LOW_PHY_RATE");
        }

        // Suppression Rule 4: Possible Interference suppresses atomic Low PHY Rate
        if (correlation.Any(c => string.Equals(c.RuleCode, "POSSIBLE_INTERFERENCE", StringComparison.OrdinalIgnoreCase)))
        {
            suppressedCodes.Add("LOW_PHY_RATE");
        }

        var output = new List<DiagnosticResult>();

        // Add correlation results first
        output.AddRange(correlation);

        // Add non-suppressed atomic results
        foreach (var item in atomic)
        {
            if (!suppressedCodes.Contains(item.RuleCode))
            {
                output.Add(item);
            }
        }

        return output;
    }
}
