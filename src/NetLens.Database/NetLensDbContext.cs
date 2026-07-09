using Microsoft.EntityFrameworkCore;
using NetLens.Database.Entities;

namespace NetLens.Database;

/// <summary>
/// EF Core database context for the NetLens platform.
/// Uses SQLite for local, zero-configuration data persistence.
/// </summary>
public sealed class NetLensDbContext : DbContext
{
    public DbSet<DiagnosticSessionRecord> Sessions => Set<DiagnosticSessionRecord>();
    public DbSet<TimelineEventRecord> TimelineEvents => Set<TimelineEventRecord>();
    public DbSet<WirelessSnapshotRecord> Snapshots => Set<WirelessSnapshotRecord>();

    public NetLensDbContext(DbContextOptions<NetLensDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DiagnosticSessionRecord>(e =>
        {
            e.HasKey(s => s.SessionId);
            e.Property(s => s.SessionId).ValueGeneratedNever();
            e.HasMany(s => s.TimelineEvents)
             .WithOne()
             .HasForeignKey(t => t.SessionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Snapshots)
             .WithOne()
             .HasForeignKey(t => t.SessionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimelineEventRecord>(e =>
        {
            e.HasKey(t => t.EventId);
            e.Property(t => t.EventId).ValueGeneratedNever();
            e.Property(t => t.EvidenceJson).HasColumnType("TEXT");
        });

        modelBuilder.Entity<WirelessSnapshotRecord>(e =>
        {
            e.HasKey(s => s.SnapshotId);
            e.Property(s => s.SnapshotId).ValueGeneratedNever();
        });
    }
}
