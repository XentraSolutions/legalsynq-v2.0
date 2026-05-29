using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class FacilityContactPersonConfiguration : IEntityTypeConfiguration<FacilityContactPerson>
{
    public void Configure(EntityTypeBuilder<FacilityContactPerson> builder)
    {
        builder.ToTable("liens_FacilityContactPersons");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.FacilityId).IsRequired();

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Position)
            .HasMaxLength(150);

        builder.Property(p => p.Email)
            .HasMaxLength(320);

        builder.Property(p => p.Phone)
            .HasMaxLength(30);

        builder.Property(p => p.IsActive).IsRequired();

        builder.Property(p => p.CreatedByUserId).IsRequired();
        builder.Property(p => p.UpdatedByUserId);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();

        builder.HasOne(p => p.Facility)
            .WithMany()
            .HasForeignKey(p => p.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TenantId, p.FacilityId });
    }
}
