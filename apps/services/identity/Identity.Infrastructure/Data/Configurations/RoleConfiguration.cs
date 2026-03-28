using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.IsSystemRole)
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique();

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.Roles)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new
            {
                Id = SeedIds.RolePlatformAdmin,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "PlatformAdmin",
                Description = (string?)"Full platform administration access",
                IsSystemRole = true,
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RoleTenantAdmin,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "TenantAdmin",
                Description = (string?)"Tenant-level administration access",
                IsSystemRole = true,
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            },
            new
            {
                Id = SeedIds.RoleStandardUser,
                TenantId = SeedIds.TenantLegalSynq,
                Name = "StandardUser",
                Description = (string?)"Standard user access",
                IsSystemRole = true,
                CreatedAtUtc = SeedIds.SeededAt,
                UpdatedAtUtc = SeedIds.SeededAt
            }
        );
    }
}
