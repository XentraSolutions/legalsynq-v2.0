using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SellingIdempotencyRecordConfiguration : IEntityTypeConfiguration<SellingIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<SellingIdempotencyRecord> builder)
    {
        builder.ToTable("liens_SellingIdempotencyRecords");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).IsRequired();
        builder.Property(record => record.TenantId).IsRequired();
        builder.Property(record => record.SubjectType).IsRequired().HasMaxLength(32);
        builder.Property(record => record.SubjectId).IsRequired();
        builder.Property(record => record.Route).IsRequired().HasMaxLength(180);
        builder.Property(record => record.ResourceType).IsRequired().HasMaxLength(80);
        builder.Property(record => record.ResourceKey).IsRequired().HasMaxLength(180);
        builder.Property(record => record.IdempotencyKey).IsRequired().HasMaxLength(280);
        builder.Property(record => record.IdempotencyKeyHash).IsRequired().HasMaxLength(64);
        builder.Property(record => record.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(record => record.ProcessingState).IsRequired().HasMaxLength(20);
        builder.Property(record => record.ResponseStatusCode);
        builder.Property(record => record.ResponseContentType).HasMaxLength(100);
        builder.Property(record => record.ResponseBody).HasColumnType("longtext");
        builder.Property(record => record.CompletedAtUtc);
        builder.Property(record => record.CreatedByUserId).IsRequired();
        builder.Property(record => record.UpdatedByUserId);
        builder.Property(record => record.CreatedAtUtc).IsRequired();
        builder.Property(record => record.UpdatedAtUtc).IsRequired();

        builder.HasIndex(record => new
            {
                record.TenantId,
                record.SubjectType,
                record.SubjectId,
                record.Route,
                record.ResourceType,
                record.ResourceKey,
                record.IdempotencyKeyHash,
            })
            .IsUnique()
            .HasDatabaseName("UX_SellingIdem_Tenant_Subject_Route_Resource_Key");

        builder.HasIndex(record => new { record.TenantId, record.CreatedAtUtc })
            .HasDatabaseName("IX_SellingIdem_Tenant_CreatedAtUtc");
    }
}
