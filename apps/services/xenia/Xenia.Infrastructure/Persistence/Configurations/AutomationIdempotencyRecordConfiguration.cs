using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationIdempotencyRecordConfiguration
    : IEntityTypeConfiguration<AutomationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<AutomationIdempotencyRecord> builder)
    {
        builder.ToTable("xn_automation_idempotency");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationIdempotencyRecord.AutomationKeyMaxLength)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(AutomationIdempotencyRecord.IdempotencyKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("uq_xn_automation_idempotency_tenant_key_idkey");

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey })
            .HasDatabaseName("ix_xn_automation_idempotency_tenant_key");

        builder.Property(e => e.RequestFingerprint)
            .HasColumnName("request_fingerprint")
            .HasMaxLength(AutomationIdempotencyRecord.RequestFingerprintMaxLength)
            .IsRequired();

        builder.Property(e => e.ExecutionId)
            .HasColumnName("execution_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_xn_automation_idempotency_expires");

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRequired()
            .IsConcurrencyToken();
    }
}
