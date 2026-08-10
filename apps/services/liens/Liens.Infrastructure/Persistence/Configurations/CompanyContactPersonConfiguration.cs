using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class CompanyContactPersonConfiguration : IEntityTypeConfiguration<CompanyContactPerson>
{
    public void Configure(EntityTypeBuilder<CompanyContactPerson> builder)
    {
        builder.ToTable("liens_CompanyContactPersons");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.CompanyId).IsRequired();
        builder.Property(value => value.ContactPersonTypeId).IsRequired();
        builder.Property(value => value.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(value => value.LastName).IsRequired().HasMaxLength(100);
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
        builder.HasOne(value => value.Company)
            .WithMany()
            .HasForeignKey(value => value.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ContactPersonType)
            .WithMany()
            .HasForeignKey(value => value.ContactPersonTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.TenantId, value.CompanyId, value.IsActive, value.LastName, value.FirstName })
            .HasDatabaseName("IX_CompanyContactPersons_TenantId_CompanyId_IsActive_Name");
        builder.HasIndex(value => new { value.CompanyId, value.ContactPersonTypeId })
            .HasDatabaseName("IX_CompanyContactPersons_CompanyId_ContactPersonTypeId");
    }
}
