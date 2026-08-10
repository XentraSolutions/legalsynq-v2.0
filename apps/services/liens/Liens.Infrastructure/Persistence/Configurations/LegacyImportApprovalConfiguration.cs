using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LegacyImportApprovalConfiguration : IEntityTypeConfiguration<LegacyImportApproval>
{
    public void Configure(EntityTypeBuilder<LegacyImportApproval> builder)
    {
        builder.ToTable("liens_LegacyImportApprovals");
        builder.HasKey(approval => approval.Id);

        builder.Property(approval => approval.TenantId).IsRequired();
        builder.Property(approval => approval.OrgId).IsRequired();
        builder.Property(approval => approval.SourceSystem).IsRequired().HasMaxLength(100);
        builder.Property(approval => approval.SourceFingerprint).IsRequired().HasMaxLength(128);
        builder.Property(approval => approval.LegacyProgram).IsRequired().HasMaxLength(50);
        builder.Property(approval => approval.MappingVersion).IsRequired().HasMaxLength(100);
        builder.Property(approval => approval.MappingManifestHash).IsRequired().HasMaxLength(128);
        builder.Property(approval => approval.MappingApprovalReference).IsRequired().HasMaxLength(200);
        builder.Property(approval => approval.LienAmountSource).IsRequired().HasMaxLength(20);
        builder.Property(approval => approval.LegacyStatusOneTarget).IsRequired().HasMaxLength(50);
        builder.Property(approval => approval.LegacyStatusTwoTarget).IsRequired().HasMaxLength(50);
        builder.Property(approval => approval.MigrationUserId).IsRequired();
        builder.Property(approval => approval.ApprovedByUserId).IsRequired();
        builder.Property(approval => approval.Status).IsRequired().HasMaxLength(30);
        builder.Property(approval => approval.ApprovedAtUtc).IsRequired();

        builder.HasIndex(approval => new
            {
                approval.TenantId,
                approval.SourceSystem,
                approval.LegacyProgram,
                approval.SourceFingerprint,
                approval.Status
            })
            .HasDatabaseName("IX_LegacyImportApprovals_Tenant_Source_Program_Fingerprint_Status");

        builder.HasIndex(approval => approval.ConsumedByRunId)
            .HasDatabaseName("IX_LegacyImportApprovals_ConsumedByRunId");
    }
}
