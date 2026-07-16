using NetLens.Domain.Entities;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Abstraction for persisting and querying DiagnosticSession aggregates.
/// Application only deals in domain types — never in database-layer entities.
/// </summary>
public interface ISessionRepository
{
    Task SaveSessionAsync(DiagnosticSession session, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionSummary>> GetRecentSessionsAsync(int count, CancellationToken cancellationToken);
    Task<DiagnosticSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken);
}

/// <summary>
/// A lightweight read-model returned by the repository for session list display.
/// Avoids loading full aggregate graphs for the history view.
/// </summary>
public sealed record SessionSummary(
    Guid SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? ClientName,
    string? SiteName,
    int SnapshotCount,
    int EventCount);

