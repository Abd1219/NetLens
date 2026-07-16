namespace NetLens.Domain.Entities;

using NetLens.Domain.Model;

/// <summary>
/// The Aggregate Root for a single network diagnostic session.
/// Coordinates the lifecycle of metric collection, timeline events, and health scoring.
/// </summary>
public sealed class DiagnosticSession
{
    private readonly List<TimelineEvent> _timeline = [];
    private readonly List<WirelessSnapshot> _ledger = [];

    public Guid SessionId { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? EndedAt { get; private set; }
    public DiagnosticSessionState State { get; private set; }

    public string? ClientName { get; private set; }
    public string? SiteName { get; private set; }
    public string? OperatorName { get; private set; }

    public IReadOnlyList<TimelineEvent> Timeline => _timeline.AsReadOnly();
    public IReadOnlyList<WirelessSnapshot> Ledger => _ledger.AsReadOnly();

    public WirelessSnapshot? LatestSnapshot => _ledger.Count > 0
        ? _ledger[^1]
        : null;

    public DiagnosticSession()
    {
        SessionId = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
        State = DiagnosticSessionState.Initializing;
    }

    public DiagnosticSession(Guid sessionId, DateTimeOffset startedAt, DateTimeOffset? endedAt, DiagnosticSessionState state)
    {
        SessionId = sessionId;
        StartedAt = startedAt;
        EndedAt = endedAt;
        State = state;
    }

    public void Start()
    {
        if (State != DiagnosticSessionState.Initializing)
            throw new InvalidOperationException("Session can only be started from Initializing state.");

        State = DiagnosticSessionState.Monitoring;
        AddTimelineEvent(new TimelineEvent(
            DateTimeOffset.UtcNow,
            "SESSION_STARTED",
            "Diagnostic session started.",
            "DiagnosticSession",
            TimelineEventSeverity.Info));
    }

    public void RecordSnapshot(WirelessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ledger.Add(snapshot);
    }

    public void AddTimelineEvent(TimelineEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _timeline.Add(@event);
    }

    public void SetClientInfo(string clientName, string siteName, string operatorName)
    {
        ClientName = clientName;
        SiteName = siteName;
        OperatorName = operatorName;
    }

    public void End()
    {
        if (State == DiagnosticSessionState.Ended)
            throw new InvalidOperationException("Session has already ended.");

        State = DiagnosticSessionState.Ended;
        EndedAt = DateTimeOffset.UtcNow;
        AddTimelineEvent(new TimelineEvent(
            EndedAt.Value,
            "SESSION_ENDED",
            $"Diagnostic session ended. Duration: {EndedAt.Value - StartedAt:hh\\:mm\\:ss}",
            "DiagnosticSession",
            TimelineEventSeverity.Info,
            new Dictionary<string, string>
            {
                { "SnapshotCount", _ledger.Count.ToString() },
                { "EventCount", _timeline.Count.ToString() }
            }));
    }
}

public enum DiagnosticSessionState
{
    Initializing,
    Monitoring,
    Diagnosing,
    Ended
}
