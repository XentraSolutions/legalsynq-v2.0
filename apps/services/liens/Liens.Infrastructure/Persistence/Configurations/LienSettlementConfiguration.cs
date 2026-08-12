using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienSettlementConfiguration : IEntityTypeConfiguration<LienSettlement>
{
    public void Configure(EntityTypeBuilder<LienSettlement> builder)
    {
        builder.ToTable("liens_LienSettlements");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.CaseId).IsRequired();
        builder.Property(s => s.LienId).IsRequired();
        builder.Property(s => s.PaymentNumber).IsRequired();
        builder.Property(s => s.Amount).IsRequired().HasPrecision(18, 4);
        builder.Property(s => s.SettlementDate).HasColumnType("date");
        builder.Property(s => s.Status).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Note).HasMaxLength(1000);
        builder.Property(s => s.IsDeleted).IsRequired();
        builder.Property(s => s.CreatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedByUserId);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.CaseId });
        builder.HasIndex(s => new { s.TenantId, s.LienId });
        builder.HasIndex(s => new { s.TenantId, s.SettlementDate })
            .HasDatabaseName("IX_LienSettlements_TenantId_SettlementDate");
    }
}
