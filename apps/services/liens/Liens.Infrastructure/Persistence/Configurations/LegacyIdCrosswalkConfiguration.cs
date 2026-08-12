using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LegacyIdCrosswalkConfiguration : IEntityTypeConfiguration<LegacyIdCrosswalk>
{
    public void Configure(EntityTypeBuilder<LegacyIdCrosswalk> builder)
    {
        builder.ToTable("liens_LegacyIdCrosswalks");
        builder.HasKey(crosswalk => crosswalk.Id);

        builder.Property(crosswalk => crosswalk.TenantId).IsRequired();
        builder.Property(crosswalk => crosswalk.SourceSystem).IsRequired().HasMaxLength(100);
        builder.Property(crosswalk => crosswalk.SourceTable).IsRequired().HasMaxLength(100);
        builder.Property(crosswalk => crosswalk.LegacyId).IsRequired().HasMaxLength(100);
        builder.Property(crosswalk => crosswalk.TargetEntity).IsRequired().HasMaxLength(100);
        builder.Property(crosswalk => crosswalk.TargetId).IsRequired();
        builder.Property(crosswalk => crosswalk.SourceHash).IsRequired().HasMaxLength(128);
        builder.Property(crosswalk => crosswalk.ImportRunId).IsRequired();
        builder.Property(crosswalk => crosswalk.CreatedAtUtc).IsRequired();

        builder.HasIndex(crosswalk => new { crosswalk.TenantId, crosswalk.SourceSystem, crosswalk.SourceTable, crosswalk.LegacyId })
            .IsUnique()
            .HasDatabaseName("UX_LegacyIdCrosswalk_Tenant_Source_Table_Key");

        builder.HasIndex(crosswalk => crosswalk.ImportRunId)
            .HasDatabaseName("IX_LegacyIdCrosswalks_ImportRunId");

        builder.HasOne<LegacyImportRun>()
            .WithMany()
            .HasForeignKey(crosswalk => crosswalk.ImportRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
