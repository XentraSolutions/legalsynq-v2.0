using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralAttributionConfiguration : IEntityTypeConfiguration<ReferralAttribution>
{
    public void Configure(EntityTypeBuilder<ReferralAttribution> builder)
    {
        builder.ToTable("cc_ReferralAttributions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.LastName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.DisplayOrder);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        // FullName is a computed convenience accessor, never a column.
        builder.Ignore(a => a.FullName);

        // (TenantId, Code) are both non-nullable, so unlike TenantCapability/TenantSetting's
        // nullable-scoped keys, this uniqueness is enforceable directly as a DB constraint.
        // Still double-checked at the service layer for a clean error before the DB round-trip.
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_ReferralAttributions_TenantId");
        builder.HasIndex(a => new { a.TenantId, a.IsActive })
            .HasDatabaseName("IX_ReferralAttributions_TenantId_IsActive");
        builder.HasIndex(a => new { a.TenantId, a.Code })
            .IsUnique()
            .HasDatabaseName("UX_ReferralAttributions_TenantId_Code");
    }
}
