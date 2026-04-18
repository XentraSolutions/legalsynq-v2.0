using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienTaskTemplateConfiguration : IEntityTypeConfiguration<LienTaskTemplate>
{
    public void Configure(EntityTypeBuilder<LienTaskTemplate> builder)
    {
        builder.ToTable("liens_TaskTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.DefaultTitle)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.DefaultDescription)
            .HasMaxLength(4000);

        builder.Property(t => t.DefaultPriority)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.DefaultDueOffsetDays);

        builder.Property(t => t.DefaultRoleId)
            .HasMaxLength(200);

        builder.Property(t => t.ContextType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.ApplicableWorkflowStageId);

        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.Version).IsRequired();
        builder.Property(t => t.LastUpdatedAt).IsRequired();
        builder.Property(t => t.LastUpdatedByUserId);

        builder.Property(t => t.LastUpdatedByName)
            .HasMaxLength(200);

        builder.Property(t => t.LastUpdatedSource)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.CreatedByUserId);
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.ContextType })
            .HasDatabaseName("IX_TaskTemplates_TenantId_ContextType");

        builder.HasIndex(t => new { t.TenantId, t.IsActive })
            .HasDatabaseName("IX_TaskTemplates_TenantId_IsActive");
    }
}
