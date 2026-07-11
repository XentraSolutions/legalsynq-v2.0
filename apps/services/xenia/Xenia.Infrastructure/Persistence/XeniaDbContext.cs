using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Xenia.Domain.Adapters;
using Xenia.Domain.Common;
using Xenia.Domain.Configuration;
using Xenia.Domain.Email;
using Xenia.Domain.Modules;

namespace Xenia.Infrastructure.Persistence;

/// <summary>
/// Root EF Core DbContext for the Xenia service.
///
/// SaveChanges is intercepted to stamp <see cref="IAuditableEntity"/> timestamps
/// automatically: CreatedAtUtc on insert, UpdatedAtUtc on insert and update.
///
/// Each service in the platform owns its own DbContext — there is no shared
/// Xenia schema access from other services.
/// </summary>
public sealed class XeniaDbContext : DbContext
{
    public XeniaDbContext(DbContextOptions<XeniaDbContext> options) : base(options) { }

    public DbSet<XeniaModule> Modules => Set<XeniaModule>();
    public DbSet<XeniaTenantModule> TenantModules => Set<XeniaTenantModule>();
    public DbSet<PlatformAdapter> PlatformAdapters => Set<PlatformAdapter>();
    public DbSet<XeniaConfigurationEntry> ConfigurationEntries => Set<XeniaConfigurationEntry>();
    public DbSet<XeniaTenantSettings> TenantSettings => Set<XeniaTenantSettings>();

    // ── Email module ──────────────────────────────────────────────────────────
    public DbSet<EmailSource> EmailSources => Set<EmailSource>();
    public DbSet<EmailProviderSettings> EmailProviderSettings => Set<EmailProviderSettings>();
    public DbSet<EmailValidationHistory> EmailValidationHistory => Set<EmailValidationHistory>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();

    // ── Email ingestion engine ────────────────────────────────────────────────
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailMessageRecipient> EmailMessageRecipients => Set<EmailMessageRecipient>();
    public DbSet<EmailAttachmentReference> EmailAttachmentReferences => Set<EmailAttachmentReference>();
    public DbSet<EmailSyncState> EmailSyncStates => Set<EmailSyncState>();
    public DbSet<EmailIngestionRun> EmailIngestionRuns => Set<EmailIngestionRun>();
    public DbSet<EmailSourceSyncLock> EmailSourceSyncLocks => Set<EmailSourceSyncLock>();

    // ── Email operations domain ───────────────────────────────────────────────
    public DbSet<EmailOperationalAlert> EmailOperationalAlerts => Set<EmailOperationalAlert>();
    public DbSet<EmailOperationalSettings> EmailOperationalSettings => Set<EmailOperationalSettings>();
    public DbSet<EmailRetentionRun> EmailRetentionRuns => Set<EmailRetentionRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(XeniaDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampAuditTimestamps()
    {
        var utcNow = DateTime.UtcNow;
        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditableEntity auditable) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.SetCreatedAt(utcNow);
                    auditable.SetUpdatedAt(utcNow);
                    break;
                case EntityState.Modified:
                    auditable.SetUpdatedAt(utcNow);
                    entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                    break;
            }
        }
    }
}
