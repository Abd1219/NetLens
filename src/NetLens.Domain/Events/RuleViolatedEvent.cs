using NetLens.Domain.Rules;

namespace NetLens.Domain.Events;

/// <summary>
/// Published when the Rule Engine determines a diagnostic rule has been violated.
/// Consumed by the Dashboard, Timeline recorder, and Recommendation engine.
/// </summary>
public sealed record RuleViolatedEvent(
    Guid SessionId,
    DiagnosticResult Result) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
