namespace NetLens.Domain.Entities;

using NetLens.Domain.Events;
using NetLens.Domain.Model;

/// <summary>
/// Severity levels for timeline events.
/// </summary>
public enum TimelineEventSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Represents a significant, timestamped event in the network diagnostic timeline.
/// These are the key diagnostic moments stored in the session flight recorder.
/// </summary>
public sealed class TimelineEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; }
    public string EventCode { get; }
    public string Description { get; }
    public string Origin { get; }
    public TimelineEventSeverity Severity { get; }

    /// <summary>
    /// Structured evidence dictionary. E.g., { "RSSI": "-82 dBm", "Threshold": "-75 dBm" }
    /// </summary>
    public IReadOnlyDictionary<string, string> Evidence { get; }

    public TimelineEvent(
        DateTimeOffset occurredAt,
        string eventCode,
        string description,
        string origin,
        TimelineEventSeverity severity,
        IReadOnlyDictionary<string, string>? evidence = null)
    {
        if (string.IsNullOrWhiteSpace(eventCode))
            throw new ArgumentException("Event code is required.", nameof(eventCode));

        OccurredAt = occurredAt;
        EventCode = eventCode;
        Description = description;
        Origin = origin;
        Severity = severity;
        Evidence = evidence ?? new Dictionary<string, string>();
    }
}
