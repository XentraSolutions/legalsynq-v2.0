using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LegacyImportRunConfiguration : IEntityTypeConfiguration<LegacyImportRun>
{
    public void Configure(EntityTypeBuilder<LegacyImportRun> builder)
    {
        builder.ToTable("liens_LegacyImportRuns");
        builder.HasKey(run => run.Id);

        builder.Property(run => run.ApprovalId);
        builder.Property(run => run.TenantId).IsRequired();
        builder.Property(run => run.OrgId).IsRequired();
        builder.Property(run => run.SourceSystem).IsRequired().HasMaxLength(100);
        builder.Property(run => run.SourceFingerprint).IsRequired().HasMaxLength(128);
        builder.Property(run => run.LegacyProgram).IsRequired().HasMaxLength(50);
        builder.Property(run => run.MappingVersion).IsRequired().HasMaxLength(100);
        builder.Property(run => run.MappingManifestHash).IsRequired().HasMaxLength(128);
        builder.Property(run => run.MappingApprovalReference).IsRequired().HasMaxLength(200);
        builder.Property(run => run.Status).IsRequired().HasMaxLength(30);
        builder.Property(run => run.StartedAtUtc).IsRequired();
        builder.Property(run => run.CreatedByUserId).IsRequired();
        builder.Property(run => run.SummaryJson).HasColumnType("longtext");
        builder.Property(run => run.ErrorSummary).HasMaxLength(2000);

        builder.HasIndex(run => new { run.TenantId, run.SourceSystem, run.LegacyProgram, run.StartedAtUtc })
            .HasDatabaseName("IX_LegacyImportRuns_Tenant_Source_Program_Started");

        builder.HasIndex(run => run.ApprovalId)
            .HasDatabaseName("IX_LegacyImportRuns_ApprovalId");

        builder.HasOne<LegacyImportApproval>()
            .WithMany()
            .HasForeignKey(run => run.ApprovalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
