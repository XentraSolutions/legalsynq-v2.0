using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class ManualMedicalCodeConfiguration : IEntityTypeConfiguration<ManualMedicalCode>
{
    public void Configure(EntityTypeBuilder<ManualMedicalCode> builder)
    {
        builder.ToTable("liens_ManualMedicalCodes");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).IsRequired();
        builder.Property(m => m.TenantId).IsRequired();

        builder.Property(m => m.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Description)
            .HasMaxLength(255);

        builder.Property(m => m.FacilityType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("ASC");

        builder.Property(m => m.Cost).HasPrecision(18, 2);
        builder.Property(m => m.Copay).HasPrecision(18, 2);
        builder.Property(m => m.FacilityTotal).HasPrecision(18, 2);
        builder.Property(m => m.PhysicianTotal).HasPrecision(18, 2);
        builder.Property(m => m.Total).HasPrecision(18, 2);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasMaxLength(5)
            .HasDefaultValue("A");

        builder.Property(m => m.CreatedByUserId).IsRequired();
        builder.Property(m => m.UpdatedByUserId);
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.UpdatedAtUtc).IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.Code })
            .HasDatabaseName("IX_ManualMedicalCodes_TenantId_Code");

        builder.HasIndex(m => new { m.TenantId, m.Status })
            .HasDatabaseName("IX_ManualMedicalCodes_TenantId_Status");
    }
}
