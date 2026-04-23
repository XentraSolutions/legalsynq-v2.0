using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Domain.Tenant>
{
    public void Configure(EntityTypeBuilder<Domain.Tenant> builder)
    {
        builder.ToTable("tenant_Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.LegalName)
            .HasMaxLength(300);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Subdomain)
            .HasMaxLength(63);

        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasFilter("`Subdomain` IS NOT NULL");

        builder.Property(t => t.LogoDocumentId);

        builder.Property(t => t.LogoWhiteDocumentId);

        builder.Property(t => t.TimeZone)
            .HasMaxLength(100);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .IsRequired();
    }
}
