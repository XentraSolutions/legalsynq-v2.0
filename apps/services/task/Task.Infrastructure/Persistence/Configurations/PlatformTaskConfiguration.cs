using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class PlatformTaskConfiguration : IEntityTypeConfiguration<PlatformTask>
{
    public void Configure(EntityTypeBuilder<PlatformTask> builder)
    {
        builder.ToTable("tasks_Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .HasMaxLength(4000);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue("OPEN");

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("MEDIUM");

        builder.Property(t => t.Scope)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("GENERAL");

        builder.Property(t => t.AssignedUserId);

        builder.Property(t => t.SourceProductCode).HasMaxLength(50);
        builder.Property(t => t.SourceEntityType).HasMaxLength(100);
        builder.Property(t => t.SourceEntityId);

        builder.Property(t => t.CurrentStageId);

        builder.Property(t => t.DueAt);
        builder.Property(t => t.CompletedAt);
        builder.Property(t => t.ClosedByUserId);

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.Status })
            .HasDatabaseName("IX_Tasks_TenantId_Status");

        builder.HasIndex(t => new { t.TenantId, t.AssignedUserId })
            .HasDatabaseName("IX_Tasks_TenantId_AssignedUserId");

        builder.HasIndex(t => new { t.TenantId, t.Scope, t.SourceProductCode })
            .HasDatabaseName("IX_Tasks_TenantId_Scope_Product");

        builder.HasIndex(t => new { t.TenantId, t.CreatedAtUtc })
            .HasDatabaseName("IX_Tasks_TenantId_CreatedAt");

        builder.HasIndex(t => new { t.TenantId, t.CurrentStageId })
            .HasDatabaseName("IX_Tasks_TenantId_StageId");
    }
}
