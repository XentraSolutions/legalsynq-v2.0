using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.TenantId).IsRequired();

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.DisplayName)
            .HasMaxLength(300);

        builder.Property(o => o.OrgType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.IsActive).IsRequired();
        builder.Property(o => o.CreatedAtUtc).IsRequired();
        builder.Property(o => o.UpdatedAtUtc).IsRequired();
        builder.Property(o => o.CreatedByUserId);
        builder.Property(o => o.UpdatedByUserId);

        builder.HasIndex(o => new { o.TenantId, o.Name }).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.OrgType });

        builder.HasOne(o => o.Tenant)
            .WithMany(t => t.Organizations)
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new
        {
            Id = SeedIds.OrgLegalSynq,
            TenantId = SeedIds.TenantLegalSynq,
            Name = "LegalSynq Platform",
            DisplayName = (string?)"LegalSynq Internal",
            OrgType = Domain.OrgType.Internal,
            IsActive = true,
            CreatedAtUtc = SeedIds.SeededAt,
            UpdatedAtUtc = SeedIds.SeededAt,
            CreatedByUserId = (Guid?)null,
            UpdatedByUserId = (Guid?)null
        });
    }
}
