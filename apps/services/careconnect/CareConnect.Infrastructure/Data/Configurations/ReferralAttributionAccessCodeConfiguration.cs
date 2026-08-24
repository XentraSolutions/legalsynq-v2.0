using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralAttributionAccessCodeConfiguration : IEntityTypeConfiguration<ReferralAttributionAccessCode>
{
    public void Configure(EntityTypeBuilder<ReferralAttributionAccessCode> builder)
    {
        builder.ToTable("cc_ReferralAttributionAccessCodes");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ReferralAttributionId).IsRequired();
        builder.Property(a => a.CodeHash).IsRequired().HasMaxLength(64);
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.AccessStartAtUtc);
        builder.Property(a => a.AccessEndAtUtc);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        builder.HasIndex(a => a.CodeHash)
            .IsUnique()
            .HasDatabaseName("UX_ReferralAttributionAccessCodes_CodeHash");
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_ReferralAttributionAccessCodes_TenantId");
        builder.HasIndex(a => a.ReferralAttributionId)
            .HasDatabaseName("IX_ReferralAttributionAccessCodes_ReferralAttributionId");

        builder.HasOne(a => a.ReferralAttribution)
               .WithMany()
               .HasForeignKey(a => a.ReferralAttributionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
