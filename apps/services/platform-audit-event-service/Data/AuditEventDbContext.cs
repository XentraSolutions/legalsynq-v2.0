using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Data;

/// <summary>
/// EF Core DbContext placeholder for durable audit event persistence.
/// Currently wired with InMemory provider for development scaffolding.
/// Replace connection string and provider registration in Program.cs
/// when migrating to a production database (SQL Server, PostgreSQL, etc.).
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
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Source).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Outcome).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TenantId).HasMaxLength(100);
            entity.Property(e => e.ActorId).HasMaxLength(200);
            entity.Property(e => e.ActorLabel).HasMaxLength(300);
            entity.Property(e => e.TargetType).HasMaxLength(200);
            entity.Property(e => e.TargetId).HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.IntegrityHash).HasMaxLength(64);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => new { e.TenantId, e.OccurredAtUtc });
        });
    }
}
