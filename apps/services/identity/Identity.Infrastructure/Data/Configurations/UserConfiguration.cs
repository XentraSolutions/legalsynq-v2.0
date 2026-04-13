using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        builder.Property(u => u.UpdatedAtUtc)
            .IsRequired();

        builder.Property(u => u.AvatarDocumentId)
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        // UIX-003-03: security / session fields
        builder.Property(u => u.IsLocked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.LockedAtUtc)
            .IsRequired(false);

        builder.Property(u => u.LockedByAdminId)
            .HasColumnType("char(36)")
            .IsRequired(false);

        builder.Property(u => u.LastLoginAtUtc)
            .IsRequired(false);

        builder.Property(u => u.SessionVersion)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.AccessVersion)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
