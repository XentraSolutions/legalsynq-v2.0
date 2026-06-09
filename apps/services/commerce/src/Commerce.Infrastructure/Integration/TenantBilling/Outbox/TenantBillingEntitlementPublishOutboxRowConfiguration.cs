using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Integration.TenantBilling.Outbox;

/// <summary>
/// TB-INT-04 — EF configuration for the Commerce-side entitlement
/// publish outbox table.
/// </summary>
internal sealed class TenantBillingEntitlementPublishOutboxRowConfiguration
    : IEntityTypeConfiguration<TenantBillingEntitlementPublishOutboxRow>
{
    public void Configure(EntityTypeBuilder<TenantBillingEntitlementPublishOutboxRow> b)
    {
        b.ToTable("tenant_billing_entitlement_publish_outbox");
        b.HasKey(x => x.Id);

        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.TriggerSource).HasMaxLength(120).IsRequired();

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();
        b.Property(x => x.Attempts).IsRequired();
        b.Property(x => x.MaxAttempts).IsRequired();

        b.Property(x => x.NextAttemptAtUtc).IsRequired();
        b.Property(x => x.LastAttemptAtUtc);
        b.Property(x => x.PublishedAtUtc);

        b.Property(x => x.LastOutcome).HasMaxLength(32);
        b.Property(x => x.LastReason).HasMaxLength(120);
        b.Property(x => x.LastHttpStatus);
        b.Property(x => x.LastErrorSummary).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).HasMaxLength(128);

        b.Property(x => x.LockedAtUtc);
        b.Property(x => x.LockId);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.Status)
            .HasDatabaseName("ix_tb_entitlement_outbox_status");
        b.HasIndex(x => x.NextAttemptAtUtc)
            .HasDatabaseName("ix_tb_entitlement_outbox_next_attempt");
        b.HasIndex(x => x.BillingAccountId)
            .HasDatabaseName("ix_tb_entitlement_outbox_billing_account");
        b.HasIndex(x => x.TriggerSource)
            .HasDatabaseName("ix_tb_entitlement_outbox_trigger_source");
        b.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("ix_tb_entitlement_outbox_created_at");
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc })
            .HasDatabaseName("ix_tb_entitlement_outbox_status_next_attempt");
    }
}
