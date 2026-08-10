using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("liens_Companies");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.OrgId).IsRequired();
        builder.Property(value => value.LinkedTenantId);
        builder.Property(value => value.CompanyTypeId).IsRequired();
        builder.Property(value => value.Name).IsRequired().HasMaxLength(200);
        builder.Property(value => value.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(value => value.AddressLine1).HasMaxLength(300);
        builder.Property(value => value.City).HasMaxLength(100);
        builder.Property(value => value.State).HasMaxLength(100);
        builder.Property(value => value.PostalCode).HasMaxLength(20);
        builder.Property(value => value.Phone).HasMaxLength(30);
        builder.Property(value => value.Email).HasMaxLength(320);
        builder.Property(value => value.IsActive).IsRequired();
        builder.Property(value => value.CreatedAtUtc).IsRequired();
        builder.Property(value => value.UpdatedAtUtc).IsRequired();
        builder.Property(value => value.CreatedByUserId).IsRequired();
        builder.Property(value => value.UpdatedByUserId);
        builder.HasOne(value => value.CompanyType)
            .WithMany()
            .HasForeignKey(value => value.CompanyTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.TenantId, value.OrgId, value.CompanyTypeId, value.NormalizedName })
            .IsUnique()
            .HasDatabaseName("UX_Companies_TenantId_OrgId_CompanyTypeId_NormalizedName");
        builder.HasIndex(value => new { value.TenantId, value.OrgId, value.CompanyTypeId, value.IsActive })
            .HasDatabaseName("IX_Companies_TenantId_OrgId_CompanyTypeId_IsActive");
        builder.HasIndex(value => value.LinkedTenantId)
            .HasDatabaseName("IX_Companies_LinkedTenantId");
    }
}
