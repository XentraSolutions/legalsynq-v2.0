using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fund.Infrastructure.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Domain.Application>
{
    public void Configure(EntityTypeBuilder<Domain.Application> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();

        builder.Property(a => a.ApplicationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.ApplicantFirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ApplicantLastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(a => a.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.ApplicationNumber }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });
    }
}
