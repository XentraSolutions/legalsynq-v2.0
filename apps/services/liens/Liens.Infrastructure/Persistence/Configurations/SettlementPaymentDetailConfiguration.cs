using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SettlementPaymentDetailConfiguration : IEntityTypeConfiguration<SettlementPaymentDetail>
{
    public void Configure(EntityTypeBuilder<SettlementPaymentDetail> builder)
    {
        builder.ToTable("liens_SettlementPaymentDetails");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.CaseId).IsRequired();
        builder.Property(p => p.LienId).IsRequired();
        builder.Property(p => p.PaymentNumber).IsRequired();
        builder.Property(p => p.Amount).IsRequired().HasPrecision(18, 4);
        builder.Property(p => p.PaymentDate);
        builder.Property(p => p.Payee).HasMaxLength(500);
        builder.Property(p => p.CheckNumber).HasMaxLength(100);
        builder.Property(p => p.Note).HasMaxLength(1000);
        builder.Property(p => p.IsDeleted).IsRequired();
        builder.Property(p => p.CreatedByUserId).IsRequired();
        builder.Property(p => p.UpdatedByUserId);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.CaseId });
        builder.HasIndex(p => new { p.TenantId, p.LienId });
    }
}
