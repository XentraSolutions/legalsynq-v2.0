using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SellingPartyBackfillQuarantineConfiguration : IEntityTypeConfiguration<SellingPartyBackfillQuarantine>
{
    public void Configure(EntityTypeBuilder<SellingPartyBackfillQuarantine> builder)
    {
        builder.ToTable("liens_SellingPartyBackfillQuarantines");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.Namespace).IsRequired().HasMaxLength(50);
        builder.Property(value => value.WorkflowProvenance).IsRequired().HasMaxLength(80);
        builder.Property(value => value.ExternalId).IsRequired();
        builder.Property(value => value.ReasonCode).IsRequired().HasMaxLength(100);
        builder.Property(value => value.Details).IsRequired().HasMaxLength(4000);
        builder.Property(value => value.CreatedAtUtc).IsRequired();
        builder.Property(value => value.UpdatedAtUtc).IsRequired();
        builder.Property(value => value.CreatedByUserId).IsRequired();
        builder.Property(value => value.UpdatedByUserId);
        builder.HasIndex(value => new
            { value.TenantId, value.Namespace, value.WorkflowProvenance, value.ExternalId, value.ReasonCode })
            .IsUnique().HasDatabaseName("UX_SellingPartyBackfillQuarantines_SourceReason");
    }
}
