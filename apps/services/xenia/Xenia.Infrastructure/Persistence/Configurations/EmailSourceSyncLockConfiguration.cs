using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailSourceSyncLockConfiguration : IEntityTypeConfiguration<EmailSourceSyncLock>
{
    public void Configure(EntityTypeBuilder<EmailSourceSyncLock> builder)
    {
        builder.ToTable("xn_email_source_sync_locks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)");

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(x => x.EmailSourceId)
            .HasColumnName("email_source_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(x => x.LeaseOwnerId)
            .HasColumnName("lease_owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AcquiredAt)
            .HasColumnName("acquired_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(x => x.RenewedAt)
            .HasColumnName("renewed_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsRowVersion()
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.EmailSourceId })
            .IsUnique()
            .HasDatabaseName("ux_email_source_sync_locks_source");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_email_source_sync_locks_expires_at");
    }
}
