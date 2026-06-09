using Commerce.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class BillingAccountConfiguration : IEntityTypeConfiguration<BillingAccount>
{
    public void Configure(EntityTypeBuilder<BillingAccount> b)
    {
        b.ToTable("billing_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.AccountNumber).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.AccountNumber).IsUnique().HasDatabaseName("ux_billing_accounts_account_number");
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(400);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.DefaultCurrency).HasMaxLength(3).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class BillingAccountExternalRefConfiguration : IEntityTypeConfiguration<BillingAccountExternalRef>
{
    public void Configure(EntityTypeBuilder<BillingAccountExternalRef> b)
    {
        b.ToTable("billing_account_external_refs");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.HostPlatformKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.ExternalTenantId).HasMaxLength(128).IsRequired();
        b.Property(x => x.ExternalCustomerRef).HasMaxLength(128);
        b.Property(x => x.IsPrimary).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.HostPlatformKey, x.ExternalTenantId })
            .IsUnique()
            .HasDatabaseName("ux_billing_external_refs_host_tenant");
        b.HasIndex(x => x.BillingAccountId)
            .HasDatabaseName("ix_billing_external_refs_account_id");

        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillingContactConfiguration : IEntityTypeConfiguration<BillingContact>
{
    public void Configure(EntityTypeBuilder<BillingContact> b)
    {
        b.ToTable("billing_account_contacts");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.ContactType).HasConversion<int>().IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(64);
        b.Property(x => x.IsPrimary).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.BillingAccountId)
            .HasDatabaseName("ix_billing_contacts_account_id");
        b.HasIndex(x => new { x.BillingAccountId, x.ContactType })
            .HasDatabaseName("ix_billing_contacts_account_type");

        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillingProfileConfiguration : IEntityTypeConfiguration<BillingProfile>
{
    public void Configure(EntityTypeBuilder<BillingProfile> b)
    {
        b.ToTable("billing_account_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.HasIndex(x => x.BillingAccountId)
            .IsUnique()
            .HasDatabaseName("ux_billing_profiles_account_id");
        b.Property(x => x.AddressLine1).HasMaxLength(200);
        b.Property(x => x.AddressLine2).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(120);
        b.Property(x => x.StateRegion).HasMaxLength(120);
        b.Property(x => x.PostalCode).HasMaxLength(40);
        b.Property(x => x.Country).HasMaxLength(2);
        b.Property(x => x.TaxId).HasMaxLength(64);
        b.Property(x => x.TaxExempt).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillingAccountAuditEventConfiguration : IEntityTypeConfiguration<BillingAccountAuditEvent>
{
    public void Configure(EntityTypeBuilder<BillingAccountAuditEvent> b)
    {
        b.ToTable("billing_account_audit_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.ActorType).HasConversion<int>().IsRequired();
        b.Property(x => x.ActorId).HasMaxLength(128);
        b.Property(x => x.MetadataJson).HasColumnType("text");
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.BillingAccountId, x.CreatedAtUtc })
            .HasDatabaseName("ix_billing_audit_events_account_created");

        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
