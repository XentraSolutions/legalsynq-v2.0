using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.HasData(new
        {
            Id = SeedIds.TenantLegalSynq,
            Name = "LegalSynq Internal",
            Code = "LEGALSYNQ",
            IsActive = true,
            CreatedAtUtc = SeedIds.SeededAt,
            UpdatedAtUtc = SeedIds.SeededAt
        });
    }
}
