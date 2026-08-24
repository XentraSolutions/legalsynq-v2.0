using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class BatchUploadConfiguration : IEntityTypeConfiguration<BatchUpload>
{
    public void Configure(EntityTypeBuilder<BatchUpload> builder)
    {
        builder.ToTable("liens_BatchUploads");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CaseId);
        builder.Property(x => x.TemplateId);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Template).IsRequired().HasMaxLength(100);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.BatchDate).HasMaxLength(50);
        builder.Property(x => x.Rows).IsRequired();
        builder.Property(x => x.DataContext).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(1);
        builder.Property(x => x.ProcessStatus).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.UpdatedByUserId);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasMany(x => x.Details)
            .WithOne()
            .HasForeignKey(d => d.BatchUploadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_BatchUploads_TenantId_Status_CreatedAt");
    }
}
