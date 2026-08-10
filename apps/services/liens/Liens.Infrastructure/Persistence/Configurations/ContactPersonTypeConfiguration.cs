using Liens.Domain;
using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class ContactPersonTypeConfiguration : IEntityTypeConfiguration<ContactPersonType>
{
    public void Configure(EntityTypeBuilder<ContactPersonType> builder)
    {
        builder.ToTable("liens_ContactPersonTypes");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CompanyTypeId).IsRequired();
        builder.Property(value => value.Code).IsRequired().HasMaxLength(100);
        builder.Property(value => value.Name).IsRequired().HasMaxLength(150);
        builder.Property(value => value.SortOrder).IsRequired();
        builder.Property(value => value.IsActive).IsRequired();
        builder.Property(value => value.CreatedAtUtc).IsRequired();
        builder.Property(value => value.UpdatedAtUtc).IsRequired();
        builder.Property(value => value.CreatedByUserId).IsRequired();
        builder.Property(value => value.UpdatedByUserId);
        builder.HasOne(value => value.CompanyType)
            .WithMany()
            .HasForeignKey(value => value.CompanyTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.CompanyTypeId, value.Code })
            .IsUnique()
            .HasDatabaseName("UX_ContactPersonTypes_CompanyTypeId_Code");
        builder.HasIndex(value => new { value.CompanyTypeId, value.IsActive, value.SortOrder })
            .HasDatabaseName("IX_ContactPersonTypes_CompanyTypeId_IsActive_SortOrder");

        builder.HasData(CompanyDirectoryReferenceData.ContactPersonTypes.Select(value => new
        {
            value.Id,
            value.CompanyTypeId,
            value.Code,
            value.Name,
            value.SortOrder,
            IsActive = true,
            CreatedAtUtc = CompanyDirectoryReferenceData.SeededAtUtc,
            UpdatedAtUtc = CompanyDirectoryReferenceData.SeededAtUtc,
            CreatedByUserId = (Guid?)CompanyDirectoryReferenceData.SystemUserId,
            UpdatedByUserId = (Guid?)CompanyDirectoryReferenceData.SystemUserId,
        }));
    }
}
