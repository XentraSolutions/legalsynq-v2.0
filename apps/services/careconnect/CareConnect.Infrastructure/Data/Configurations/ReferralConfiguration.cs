using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable("Referrals");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.ProviderId).IsRequired();
        builder.Property(r => r.ClientFirstName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ClientLastName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ClientDob);
        builder.Property(r => r.ClientPhone).IsRequired().HasMaxLength(50);
        builder.Property(r => r.ClientEmail).IsRequired().HasMaxLength(320);
        builder.Property(r => r.CaseNumber).HasMaxLength(100);
        builder.Property(r => r.RequestedService).IsRequired().HasMaxLength(500);
        builder.Property(r => r.Urgency).IsRequired().HasMaxLength(20);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(20);
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();
        builder.Property(r => r.CreatedByUserId);
        builder.Property(r => r.UpdatedByUserId);

        builder.HasIndex(r => new { r.TenantId, r.Status });
        builder.HasIndex(r => new { r.TenantId, r.ProviderId });
        builder.HasIndex(r => new { r.TenantId, r.CreatedAtUtc });

        builder.HasOne(r => r.Provider)
               .WithMany()
               .HasForeignKey(r => r.ProviderId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
