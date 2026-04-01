using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class TenantGroupConfiguration : IEntityTypeConfiguration<TenantGroup>
{
    public void Configure(EntityTypeBuilder<TenantGroup> builder)
    {
        builder.ToTable("TenantGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.TenantId).IsRequired();
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Description).HasMaxLength(1000);
        builder.Property(g => g.IsActive).IsRequired();
        builder.Property(g => g.CreatedAtUtc).IsRequired();
        builder.Property(g => g.UpdatedAtUtc).IsRequired();
        builder.Property(g => g.CreatedByUserId);

        builder.HasIndex(g => g.TenantId);
        builder.HasIndex(g => new { g.TenantId, g.Name }).IsUnique();

        builder.HasOne(g => g.Tenant)
            .WithMany()
            .HasForeignKey(g => g.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
