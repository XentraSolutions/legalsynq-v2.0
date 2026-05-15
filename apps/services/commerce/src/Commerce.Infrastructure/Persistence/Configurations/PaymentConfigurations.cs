using Commerce.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class PaymentProviderCustomerConfiguration : IEntityTypeConfiguration<PaymentProviderCustomer>
{
    public void Configure(EntityTypeBuilder<PaymentProviderCustomer> b)
    {
        b.ToTable("payment_provider_customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.Provider).HasConversion<int>().IsRequired();
        b.Property(x => x.ProviderCustomerId).HasMaxLength(128).IsRequired();
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.BillingAccountId, x.Provider }).IsUnique()
            .HasDatabaseName("ux_payment_provider_customers_account_provider");
        b.HasIndex(x => new { x.Provider, x.ProviderCustomerId }).IsUnique()
            .HasDatabaseName("ux_payment_provider_customers_provider_pcid");

        b.HasOne<Commerce.Domain.Billing.BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentProviderSubscriptionConfiguration : IEntityTypeConfiguration<PaymentProviderSubscription>
{
    public void Configure(EntityTypeBuilder<PaymentProviderSubscription> b)
    {
        b.ToTable("payment_provider_subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.SubscriptionId).IsRequired();
        b.Property(x => x.Provider).HasConversion<int>().IsRequired();
        b.Property(x => x.ProviderSubscriptionId).HasMaxLength(128);
        b.Property(x => x.ProviderCheckoutSessionId).HasMaxLength(128);
        b.Property(x => x.ProviderCustomerId).HasMaxLength(128);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.SubscriptionId, x.Provider }).IsUnique()
            .HasDatabaseName("ux_payment_provider_subs_sub_provider");
        // Unique on non-null values; MySQL allows multiple NULLs in a
        // unique index, so deterministic webhook lookup is preserved.
        b.HasIndex(x => new { x.Provider, x.ProviderSubscriptionId }).IsUnique()
            .HasDatabaseName("ux_payment_provider_subs_provider_psid");
        b.HasIndex(x => new { x.Provider, x.ProviderCheckoutSessionId }).IsUnique()
            .HasDatabaseName("ux_payment_provider_subs_provider_csid");

        b.HasOne<Commerce.Domain.Subscriptions.Subscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentMethodReferenceConfiguration : IEntityTypeConfiguration<PaymentMethodReference>
{
    public void Configure(EntityTypeBuilder<PaymentMethodReference> b)
    {
        b.ToTable("payment_method_references");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.Provider).HasConversion<int>().IsRequired();
        b.Property(x => x.ProviderPaymentMethodId).HasMaxLength(128).IsRequired();
        b.Property(x => x.ProviderCustomerId).HasMaxLength(128);
        b.Property(x => x.Brand).HasMaxLength(32);
        b.Property(x => x.Last4).HasMaxLength(4);
        b.Property(x => x.ExpMonth);
        b.Property(x => x.ExpYear);
        b.Property(x => x.IsDefault).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.Provider, x.ProviderPaymentMethodId }).IsUnique()
            .HasDatabaseName("ux_payment_method_refs_provider_pmid");
        b.HasIndex(x => new { x.BillingAccountId, x.Provider })
            .HasDatabaseName("ix_payment_method_refs_account_provider");

        b.HasOne<Commerce.Domain.Billing.BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentProviderEventLogConfiguration : IEntityTypeConfiguration<PaymentProviderEventLog>
{
    public void Configure(EntityTypeBuilder<PaymentProviderEventLog> b)
    {
        b.ToTable("payment_provider_event_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasConversion<int>().IsRequired();
        b.Property(x => x.ProviderEventId).HasMaxLength(128).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("longtext").IsRequired();
        b.Property(x => x.ProcessingStatus).HasConversion<int>().IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);
        b.Property(x => x.ProcessedAtUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique()
            .HasDatabaseName("ux_payment_provider_event_logs_provider_eventid");
        b.HasIndex(x => new { x.Provider, x.CreatedAtUtc })
            .HasDatabaseName("ix_payment_provider_event_logs_provider_created");
        b.HasIndex(x => x.ProcessingStatus)
            .HasDatabaseName("ix_payment_provider_event_logs_status");
    }
}
