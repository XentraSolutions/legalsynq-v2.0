using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienReductionConfiguration : IEntityTypeConfiguration<LienReduction>
{
    public void Configure(EntityTypeBuilder<LienReduction> builder)
    {
        builder.ToTable("liens_LienReductions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.CaseId).IsRequired();
        builder.Property(r => r.LienId).IsRequired();
        builder.Property(r => r.ReductionDate).IsRequired();
        builder.Property(r => r.Amount).IsRequired().HasPrecision(18, 4);
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.IsDeleted).IsRequired();
        builder.Property(r => r.CreatedByUserId).IsRequired();
        builder.Property(r => r.UpdatedByUserId);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.CaseId });
        builder.HasIndex(r => new { r.TenantId, r.LienId });
    }
}
