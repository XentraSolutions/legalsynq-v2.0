using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public sealed class TenantRegistrationConfiguration : IEntityTypeConfiguration<TenantRegistration>
{
    public void Configure(EntityTypeBuilder<TenantRegistration> b)
    {
        b.ToTable("tenant_Registrations");
        b.HasKey(x => x.Id);
        b.Property(x => x.TenantName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TenantCode).HasMaxLength(63).IsRequired();
        b.Property(x => x.OrganizationType).HasMaxLength(80).IsRequired();
        b.Property(x => x.StreetAddress).HasMaxLength(500);
        b.Property(x => x.AddressLine1).HasMaxLength(200);
        b.Property(x => x.AddressCity).HasMaxLength(100);
        b.Property(x => x.AddressState).HasMaxLength(100);
        b.Property(x => x.AddressPostalCode).HasMaxLength(20);
        b.Property(x => x.AdminFirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.AdminLastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.AdminEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.RegistrationStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ProvisioningStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ProvisioningHostname).HasMaxLength(253);
        b.Property(x => x.ProvisioningError).HasMaxLength(2000);
        b.Property(x => x.ProvisioningFailureStage).HasMaxLength(64);
        b.Property(x => x.DecisionReason).HasMaxLength(1000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.RegistrationStatus);
        b.HasIndex(x => x.ProvisioningStatus);
        b.HasIndex(x => x.TenantCode);
        b.HasIndex(x => x.AdminEmail);
        b.HasIndex(x => x.CreatedAtUtc);
    }
}
