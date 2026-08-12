using Liens.Domain;
using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class CompanyTypeConfiguration : IEntityTypeConfiguration<CompanyType>
{
    public void Configure(EntityTypeBuilder<CompanyType> builder)
    {
        builder.ToTable("liens_CompanyTypes");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Code).IsRequired().HasMaxLength(50);
        builder.Property(value => value.Name).IsRequired().HasMaxLength(100);
        builder.Property(value => value.SortOrder).IsRequired();
        builder.Property(value => value.IsActive).IsRequired();
        builder.Property(value => value.CreatedAtUtc).IsRequired();
        builder.Property(value => value.UpdatedAtUtc).IsRequired();
        builder.Property(value => value.CreatedByUserId).IsRequired();
        builder.Property(value => value.UpdatedByUserId);
        builder.HasIndex(value => value.Code).IsUnique().HasDatabaseName("UX_CompanyTypes_Code");

        builder.HasData(CompanyDirectoryReferenceData.CompanyTypes.Select(value => new
        {
            value.Id,
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
