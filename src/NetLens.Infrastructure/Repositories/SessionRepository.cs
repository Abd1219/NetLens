using Microsoft.EntityFrameworkCore;
using NetLens.Application.Abstractions;
using NetLens.Database;
using NetLens.Database.Entities;
using NetLens.Domain.Entities;
using NetLens.Domain.Model;

namespace NetLens.Infrastructure.Repositories;

/// <summary>
/// Implements persistence of DiagnosticSession aggregates to the SQLite database.
/// Returns lightweight SessionSummary read-models for list queries.
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly NetLensDbContext _context;

    public SessionRepository(NetLensDbContext context)
    {
        _context = context;
    }


    public async Task SaveSessionAsync(DiagnosticSession session, CancellationToken cancellationToken)
    {
        var record = new DiagnosticSessionRecord
        {
            SessionId = session.SessionId,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            ClientName = session.ClientName,
            SiteName = session.SiteName,
            OperatorName = session.OperatorName,
            TimelineEvents = session.Timeline.Select(e => new TimelineEventRecord
            {
                EventId = e.EventId,
                SessionId = session.SessionId,
                OccurredAt = e.OccurredAt,
                EventCode = e.EventCode,
                Description = e.Description,
                Origin = e.Origin,
                Severity = e.Severity.ToString(),
                EvidenceJson = System.Text.Json.JsonSerializer.Serialize(e.Evidence)
            }).ToList(),
            Snapshots = session.Ledger.Select(MapSnapshot).ToList()
        };

        var existing = await _context.Sessions
            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, cancellationToken);

        if (existing is null)
            _context.Sessions.Add(record);
        else
            _context.Entry(existing).CurrentValues.SetValues(record);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetRecentSessionsAsync(
        int count, CancellationToken cancellationToken)
    {
        var records = await _context.Sessions
            .Include(s => s.TimelineEvents)
            .Include(s => s.Snapshots)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return records.Select(r => new SessionSummary(
            r.SessionId,
            r.StartedAt,
            r.EndedAt,
            r.ClientName,
            r.SiteName,
            r.Snapshots.Count,
            r.TimelineEvents.Count)).ToList().AsReadOnly();
    }

    private static WirelessSnapshotRecord MapSnapshot(WirelessSnapshot s) => new()
    {
        SnapshotId = Guid.NewGuid(),
        CapturedAt = s.CapturedAt,
        Ssid = s.Ssid,
        Bssid = s.Bssid.Value,
        RssiDbm = s.Rssi.Value,
        SignalQualityPercent = s.SignalQuality.Percentage,
        TxRateMbps = s.TxRate.Value,
        RxRateMbps = s.RxRate.Value,
        Channel = s.Channel.Number,
        FrequencyMhz = s.Frequency.ValueMhz,
        PhysicalType = s.PhysicalType,
        GatewayLatencyMs = s.GatewayLatency.IsTimeout ? -1 : s.GatewayLatency.Milliseconds,
        DnsLatencyMs = s.DnsLatency.IsTimeout ? -1 : s.DnsLatency.Milliseconds,
        InternetLatencyMs = s.InternetLatency.IsTimeout ? -1 : s.InternetLatency.Milliseconds,
        PacketLossPercent = s.PacketLoss.Percentage,
        JitterMs = s.Jitter.Milliseconds,
        LocalIp = s.LocalIp.Value,
        GatewayIp = s.GatewayIp.Value,
        DnsIp = s.DnsIp.Value,
        CpuUsagePercent = s.CpuUsagePercent,
        RamUsagePercent = s.RamUsagePercent
    };
}
