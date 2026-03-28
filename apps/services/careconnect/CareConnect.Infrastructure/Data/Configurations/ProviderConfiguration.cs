using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("Providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.OrganizationName).HasMaxLength(300);
        builder.Property(p => p.Email).IsRequired().HasMaxLength(320);
        builder.Property(p => p.Phone).IsRequired().HasMaxLength(50);
        builder.Property(p => p.AddressLine1).IsRequired().HasMaxLength(300);
        builder.Property(p => p.City).IsRequired().HasMaxLength(100);
        builder.Property(p => p.State).IsRequired().HasMaxLength(100);
        builder.Property(p => p.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.AcceptingReferrals).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.Property(p => p.CreatedByUserId);
        builder.Property(p => p.UpdatedByUserId);

        builder.HasIndex(p => new { p.TenantId, p.Email }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Name });

        builder.HasMany(p => p.ProviderCategories)
               .WithOne(pc => pc.Provider)
               .HasForeignKey(pc => pc.ProviderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
