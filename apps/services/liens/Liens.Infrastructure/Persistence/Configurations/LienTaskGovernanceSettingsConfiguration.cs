using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienTaskGovernanceSettingsConfiguration : IEntityTypeConfiguration<LienTaskGovernanceSettings>
{
    public void Configure(EntityTypeBuilder<LienTaskGovernanceSettings> builder)
    {
        builder.ToTable("liens_TaskGovernanceSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();

        builder.Property(s => s.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.RequireAssigneeOnCreate).IsRequired();
        builder.Property(s => s.RequireCaseLinkOnCreate).IsRequired();
        builder.Property(s => s.AllowMultipleAssignees).IsRequired();
        builder.Property(s => s.RequireWorkflowStageOnCreate).IsRequired();

        builder.Property(s => s.DefaultStartStageMode)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(s => s.ExplicitStartStageId);

        builder.Property(s => s.Version).IsRequired();
        builder.Property(s => s.LastUpdatedAt).IsRequired();
        builder.Property(s => s.LastUpdatedByUserId);

        builder.Property(s => s.LastUpdatedByName)
            .HasMaxLength(200);

        builder.Property(s => s.LastUpdatedSource)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.CreatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedByUserId);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.ProductCode })
            .IsUnique()
            .HasDatabaseName("UX_TaskGovernance_TenantId_ProductCode");
    }
}
