using Commerce.Domain.Billing;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("invoices");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.SubscriptionId);
        b.Property(x => x.InvoiceNumber).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.InvoiceNumber).IsUnique()
            .HasDatabaseName("ux_invoices_invoice_number");
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.SubtotalAmountMinor).IsRequired();
        b.Property(x => x.DiscountAmountMinor).IsRequired();
        b.Property(x => x.TaxAmountMinor).IsRequired();
        b.Property(x => x.TotalAmountMinor).IsRequired();
        b.Property(x => x.AmountPaidMinor).IsRequired();
        b.Property(x => x.AmountDueMinor).IsRequired();
        b.Property(x => x.IssueDateUtc).IsRequired();
        b.Property(x => x.DueDateUtc);
        b.Property(x => x.PaidAtUtc);
        b.Property(x => x.VoidedAtUtc);
        b.Property(x => x.Provider).HasConversion<int?>();
        b.Property(x => x.ProviderInvoiceId).HasMaxLength(128);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.BillingAccountId).HasDatabaseName("ix_invoices_billing_account_id");
        b.HasIndex(x => x.SubscriptionId).HasDatabaseName("ix_invoices_subscription_id");
        b.HasIndex(x => new { x.Status, x.DueDateUtc }).HasDatabaseName("ix_invoices_status_due");
        b.HasIndex(x => new { x.Provider, x.ProviderInvoiceId })
            .HasDatabaseName("ix_invoices_provider_provider_invoice_id");

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

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("invoice_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.InvoiceId).IsRequired();
        b.Property(x => x.SubscriptionItemId);
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.Quantity).IsRequired();
        b.Property(x => x.UnitAmountMinor).IsRequired();
        b.Property(x => x.LineAmountMinor).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.ServicePeriodStartUtc);
        b.Property(x => x.ServicePeriodEndUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_invoice_lines_invoice_id");
        b.HasIndex(x => x.SubscriptionItemId).HasDatabaseName("ix_invoice_lines_subscription_item_id");

        b.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<SubscriptionItem>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InvoiceBrandingConfiguration : IEntityTypeConfiguration<InvoiceBranding>
{
    public void Configure(EntityTypeBuilder<InvoiceBranding> b)
    {
        b.ToTable("invoice_branding");
        b.HasKey(x => x.Id);
        b.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        b.Property(x => x.LogoUrl).HasColumnType("mediumtext");
        b.Property(x => x.AccentColorHex).HasMaxLength(7).IsRequired();
        b.Property(x => x.AddressLine1).HasMaxLength(200);
        b.Property(x => x.AddressLine2).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(120);
        b.Property(x => x.StateRegion).HasMaxLength(120);
        b.Property(x => x.PostalCode).HasMaxLength(40);
        b.Property(x => x.Country).HasMaxLength(2);
        b.Property(x => x.ContactEmail).HasMaxLength(320);
        b.Property(x => x.ContactPhone).HasMaxLength(64);
        b.Property(x => x.Website).HasMaxLength(1000);
        b.Property(x => x.FooterText).HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
