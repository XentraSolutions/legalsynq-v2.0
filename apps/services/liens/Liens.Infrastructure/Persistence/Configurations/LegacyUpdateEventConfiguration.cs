using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LegacyUpdateEventConfiguration : IEntityTypeConfiguration<LegacyUpdateEvent>
{
    public void Configure(EntityTypeBuilder<LegacyUpdateEvent> builder)
    {
        builder.ToTable("liens_LegacyUpdateEvents", table =>
        {
            table.HasCheckConstraint(
                "CK_LegacyUpdateEvents_Scope",
                "`Scope` IN ('Case', 'Lien')");
            table.HasCheckConstraint(
                "CK_LegacyUpdateEvents_ScopeLien",
                "(`Scope` = 'Case' AND `LienId` IS NULL) OR (`Scope` = 'Lien' AND `LienId` IS NOT NULL)");
        });

        builder.HasKey(updateEvent => updateEvent.Id);
        builder.Property(updateEvent => updateEvent.TenantId).IsRequired();
        builder.Property(updateEvent => updateEvent.OrgId).IsRequired();
        builder.Property(updateEvent => updateEvent.CaseId).IsRequired();
        builder.Property(updateEvent => updateEvent.LienId);
        builder.Property(updateEvent => updateEvent.Scope).IsRequired().HasMaxLength(20);
        builder.Property(updateEvent => updateEvent.Action).IsRequired().HasMaxLength(255);
        builder.Property(updateEvent => updateEvent.Description).HasColumnType("text");
        builder.Property(updateEvent => updateEvent.ActorDisplayName).HasMaxLength(255);
        builder.Property(updateEvent => updateEvent.OccurredAtUtc).IsRequired();
        builder.Property(updateEvent => updateEvent.ImportedAtUtc).IsRequired();
        builder.Property(updateEvent => updateEvent.ImportRunId).IsRequired();
        builder.Property(updateEvent => updateEvent.SourceSystem).IsRequired().HasMaxLength(100);
        builder.Property(updateEvent => updateEvent.SourceTable).IsRequired().HasMaxLength(100);
        builder.Property(updateEvent => updateEvent.LegacyId).IsRequired().HasMaxLength(100);
        builder.Property(updateEvent => updateEvent.LegacySequence).IsRequired();

        builder.HasIndex(updateEvent => new
            {
                updateEvent.TenantId,
                updateEvent.SourceSystem,
                updateEvent.SourceTable,
                updateEvent.LegacyId,
            })
            .IsUnique()
            .HasDatabaseName("UX_LegacyUpdateEvents_Tenant_Source_Table_Key");

        builder.HasIndex(updateEvent => new
            {
                updateEvent.TenantId,
                updateEvent.CaseId,
                updateEvent.Scope,
                updateEvent.OccurredAtUtc,
                updateEvent.LegacySequence,
            })
            .IsDescending(false, false, false, true, true)
            .HasDatabaseName("IX_LegacyUpdateEvents_CaseTimeline");

        builder.HasIndex(updateEvent => new
            {
                updateEvent.TenantId,
                updateEvent.LienId,
                updateEvent.OccurredAtUtc,
                updateEvent.LegacySequence,
            })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("IX_LegacyUpdateEvents_LienTimeline");

        builder.HasIndex(updateEvent => updateEvent.ImportRunId)
            .HasDatabaseName("IX_LegacyUpdateEvents_ImportRunId");

        builder.HasOne<LegacyImportRun>()
            .WithMany()
            .HasForeignKey(updateEvent => updateEvent.ImportRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
