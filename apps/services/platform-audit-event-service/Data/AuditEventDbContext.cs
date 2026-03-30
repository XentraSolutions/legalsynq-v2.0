using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Data;

/// <summary>
/// EF Core DbContext for the Platform Audit/Event Service.
///
/// Provider support:
///   - InMemory  (dev/test, registered via UseInMemoryDatabase)
///   - MySQL 8.x (production, registered via UseMySql / Pomelo)
///
/// Schema design principles:
///   - Append-only: no UPDATE or DELETE operations are exposed by the service layer.
///   - All string columns have explicit MaxLength mappings matching validator rules.
///   - Composite and covering indexes tuned for the primary query patterns:
///     (TenantId, OccurredAtUtc), (Source, EventType), (Category, Severity, Outcome).
///   - Id column is stored as char(36) (GUID string) for portability across providers.
/// </summary>
public sealed class AuditEventDbContext : DbContext
{
    public AuditEventDbContext(DbContextOptions<AuditEventDbContext> options)
        : base(options) { }

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(e => e.Id);

            // ── Core identity columns ────────────────────────────────────────
            entity.Property(e => e.Id)
                .IsRequired()
                .HasColumnType("char(36)")
                .ValueGeneratedNever();

            // ── Required string columns ───────────────────────────────────────
            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Severity)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("INFO");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.Outcome)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("SUCCESS");

            // ── Optional columns ──────────────────────────────────────────────
            entity.Property(e => e.TenantId).HasMaxLength(100);
            entity.Property(e => e.ActorId).HasMaxLength(200);
            entity.Property(e => e.ActorLabel).HasMaxLength(300);
            entity.Property(e => e.TargetType).HasMaxLength(200);
            entity.Property(e => e.TargetId).HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.CorrelationId).HasMaxLength(200);

            // Metadata stored as JSON text (no JSON column type assumed for portability)
            entity.Property(e => e.Metadata)
                .HasColumnType("text");

            // HMAC-SHA256 = 64 hex chars
            entity.Property(e => e.IntegrityHash).HasMaxLength(64);

            // ── Timestamps ────────────────────────────────────────────────────
            entity.Property(e => e.OccurredAtUtc).IsRequired();
            entity.Property(e => e.IngestedAtUtc).IsRequired();

            // ── Indexes ───────────────────────────────────────────────────────

            // Primary lookup: tenant + time range (most frequent query pattern)
            entity.HasIndex(e => new { e.TenantId, e.OccurredAtUtc })
                .HasDatabaseName("IX_AuditEvents_TenantId_OccurredAt");

            // Event type lookups (source system feeds, per-event-type dashboards)
            entity.HasIndex(e => new { e.Source, e.EventType })
                .HasDatabaseName("IX_AuditEvents_Source_EventType");

            // Category / severity / outcome filtering (security dashboards, reports)
            entity.HasIndex(e => new { e.Category, e.Severity, e.Outcome })
                .HasDatabaseName("IX_AuditEvents_Category_Severity_Outcome");

            // Actor audit trail
            entity.HasIndex(e => e.ActorId)
                .HasDatabaseName("IX_AuditEvents_ActorId");

            // Target resource lookup
            entity.HasIndex(e => new { e.TargetType, e.TargetId })
                .HasDatabaseName("IX_AuditEvents_TargetType_TargetId");

            // Correlation / distributed trace
            entity.HasIndex(e => e.CorrelationId)
                .HasDatabaseName("IX_AuditEvents_CorrelationId");

            // Global time-based ordering (retention job, export)
            entity.HasIndex(e => e.IngestedAtUtc)
                .HasDatabaseName("IX_AuditEvents_IngestedAt");
        });
    }
}
