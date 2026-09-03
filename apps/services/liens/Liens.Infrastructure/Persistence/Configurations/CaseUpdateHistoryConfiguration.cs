using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class CaseUpdateHistoryConfiguration : IEntityTypeConfiguration<CaseUpdateHistory>
{
    public void Configure(EntityTypeBuilder<CaseUpdateHistory> builder)
    {
        builder.ToTable("liens_CaseUpdateHistory");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CaseId).IsRequired();
        builder.Property(item => item.Action).IsRequired().HasMaxLength(100);
        builder.Property(item => item.Description).IsRequired().HasColumnType("text");
        builder.Property(item => item.ActorUserId).IsRequired();
        builder.Property(item => item.OccurredAtUtc).IsRequired();
        builder.Property(item => item.CreatedByUserId).IsRequired();
        builder.Property(item => item.UpdatedByUserId);
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        // Intentionally no Case foreign key: history survives physical case deletion.
        builder.HasIndex(item => new { item.TenantId, item.CaseId, item.OccurredAtUtc })
            .HasDatabaseName("IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc");
    }
}
