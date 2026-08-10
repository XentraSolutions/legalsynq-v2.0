using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SellingPartyBackfillCheckpointConfiguration : IEntityTypeConfiguration<SellingPartyBackfillCheckpoint>
{
    public void Configure(EntityTypeBuilder<SellingPartyBackfillCheckpoint> builder)
    {
        builder.ToTable("liens_SellingPartyBackfillCheckpoints");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.Workflow).IsRequired().HasMaxLength(80);
        builder.Property(value => value.Status).IsRequired().HasMaxLength(30);
        builder.Property(value => value.LastError).HasMaxLength(2000);
        builder.Property(value => value.CreatedAtUtc).IsRequired();
        builder.Property(value => value.UpdatedAtUtc).IsRequired();
        builder.Property(value => value.CreatedByUserId).IsRequired();
        builder.Property(value => value.UpdatedByUserId);
        builder.HasIndex(value => new { value.TenantId, value.Workflow }).IsUnique()
            .HasDatabaseName("UX_SellingPartyBackfillCheckpoints_Tenant_Workflow");
    }
}
