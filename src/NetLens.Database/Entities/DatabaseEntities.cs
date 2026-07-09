namespace NetLens.Database.Entities;

/// <summary>
/// EF Core persistence entity for DiagnosticSession (stored representation).
/// Kept deliberately flat for query performance.
/// </summary>
public sealed class DiagnosticSessionRecord
{
    public Guid SessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? ClientName { get; set; }
    public string? SiteName { get; set; }
    public string? OperatorName { get; set; }

    public ICollection<TimelineEventRecord> TimelineEvents { get; set; } = [];
    public ICollection<WirelessSnapshotRecord> Snapshots { get; set; } = [];
}

/// <summary>
/// EF Core persistence entity for TimelineEvent.
/// Evidence is stored as JSON text for flexibility without schema migration overhead.
/// </summary>
public sealed class TimelineEventRecord
{
    public Guid EventId { get; set; }
    public Guid SessionId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }
}

/// <summary>
/// EF Core persistence entity for WirelessSnapshot.
/// Stores the key metrics needed for historical analysis and report generation.
/// High-frequency raw data is aggregated before persistence to conserve disk space.
/// </summary>
public sealed class WirelessSnapshotRecord
{
    public Guid SnapshotId { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }

    public string Ssid { get; set; } = string.Empty;
    public string Bssid { get; set; } = string.Empty;
    public int RssiDbm { get; set; }
    public int SignalQualityPercent { get; set; }
    public double TxRateMbps { get; set; }
    public double RxRateMbps { get; set; }
    public int Channel { get; set; }
    public int FrequencyMhz { get; set; }
    public string PhysicalType { get; set; } = string.Empty;

    public double GatewayLatencyMs { get; set; }
    public double DnsLatencyMs { get; set; }
    public double InternetLatencyMs { get; set; }
    public double PacketLossPercent { get; set; }
    public double JitterMs { get; set; }

    public string LocalIp { get; set; } = string.Empty;
    public string GatewayIp { get; set; } = string.Empty;
    public string DnsIp { get; set; } = string.Empty;

    public double CpuUsagePercent { get; set; }
    public double RamUsagePercent { get; set; }
}
