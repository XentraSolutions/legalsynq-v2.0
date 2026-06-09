using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class DIYReportConfigConfiguration : IEntityTypeConfiguration<DIYReportConfig>
{
    public void Configure(EntityTypeBuilder<DIYReportConfig> builder)
    {
        builder.ToTable("liens_DIYReportConfigs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ConfigJson).HasColumnType("longtext").IsRequired();
        builder.Property(r => r.IsDeleted).IsRequired();
        builder.Property(r => r.CreatedByUserId).IsRequired();
        builder.Property(r => r.UpdatedByUserId);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.UserId });
    }
}
