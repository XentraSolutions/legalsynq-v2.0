using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LienStatusHistoryConfiguration : IEntityTypeConfiguration<LienStatusHistory>
{
    public void Configure(EntityTypeBuilder<LienStatusHistory> builder)
    {
        builder.ToTable("liens_LienStatusHistory");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CaseId);
        builder.Property(item => item.LienId).IsRequired();
        builder.Property(item => item.Description).IsRequired().HasMaxLength(500);
        builder.Property(item => item.ChangedByUserId).IsRequired();
        builder.Property(item => item.ChangedAtUtc).IsRequired();
        builder.Property(item => item.CreatedByUserId).IsRequired();
        builder.Property(item => item.UpdatedByUserId);
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => new { item.TenantId, item.CaseId, item.ChangedAtUtc })
            .HasDatabaseName("IX_LienStatusHistory_TenantId_CaseId_ChangedAtUtc");
    }
}
