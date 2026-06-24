using Commerce.Domain.Billing;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Payments;
using Commerce.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.InvoiceId);
        b.Property(x => x.SubscriptionId);
        b.Property(x => x.Provider).HasConversion<int>().IsRequired();
        b.Property(x => x.ProviderPaymentId).HasMaxLength(128);
        b.Property(x => x.ProviderCustomerId).HasMaxLength(128);
        b.Property(x => x.AmountMinor).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.PaidAtUtc);
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.Property(x => x.FailureMessage).HasMaxLength(500);
        b.Property(x => x.Method).HasMaxLength(32);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.RecordedByLabel).HasMaxLength(200);
        b.Property(x => x.TransactionReference).HasMaxLength(128);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.BillingAccountId).HasDatabaseName("ix_payments_billing_account_id");
        b.HasIndex(x => x.SubscriptionId).HasDatabaseName("ix_payments_subscription_id");
        b.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_payments_invoice_id");
        // PostgreSQL treats NULL values as distinct in a unique index,
        // so this provides "unique when both columns are present"
        // without requiring an explicit partial index filter.
        b.HasIndex(x => new { x.Provider, x.ProviderPaymentId })
            .IsUnique()
            .HasDatabaseName("ux_payments_provider_provider_payment_id");

        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> b)
    {
        b.ToTable("payment_attempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.PaymentId);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.SubscriptionId);
        b.Property(x => x.Provider).HasConversion<int>().IsRequired();
        b.Property(x => x.ProviderEventId).HasMaxLength(128);
        b.Property(x => x.AttemptedAtUtc).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.ErrorMessage).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.PaymentId).HasDatabaseName("ix_payment_attempts_payment_id");
        b.HasIndex(x => x.BillingAccountId).HasDatabaseName("ix_payment_attempts_billing_account_id");
        // Provider event idempotency: at most one attempt per (Provider, ProviderEventId).
        // MySQL/InnoDB treats NULL values as distinct in UNIQUE indexes, so attempts with
        // no provider event id (legacy/internal triggers) are not constrained.
        b.HasIndex(x => new { x.Provider, x.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("ux_payment_attempts_provider_event_id");

        b.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
