using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class BatchUploadDetailConfiguration : IEntityTypeConfiguration<BatchUploadDetail>
{
    public void Configure(EntityTypeBuilder<BatchUploadDetail> builder)
    {
        builder.ToTable("liens_BatchUploadDetails");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.BatchUploadId).IsRequired();
        builder.Property(x => x.RowNumber).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(4000);
        builder.Property(x => x.DataJson).IsRequired().HasColumnType("longtext");
        builder.Property(x => x.RecordStatus).IsRequired().HasMaxLength(1);
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.UpdatedByUserId);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.BatchUploadId, x.RecordStatus, x.RowNumber })
            .HasDatabaseName("IX_BatchUploadDetails_BatchUploadId_RecordStatus_RowNumber");
    }
}
