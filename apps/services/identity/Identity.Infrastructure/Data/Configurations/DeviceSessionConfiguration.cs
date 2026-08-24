using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>
/// BE-BIO-010: EF Core configuration for the DeviceSessions table.
/// </summary>
public class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
{
    public void Configure(EntityTypeBuilder<DeviceSession> builder)
    {
        builder.ToTable("idt_DeviceSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.RefreshTokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(s => s.TokenFamilyId)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.Property(s => s.LastUsedAtUtc)
            .IsRequired();

        builder.Property(s => s.AbsoluteExpiresAtUtc)
            .IsRequired();

        builder.Property(s => s.InactivityExpiresAtUtc)
            .IsRequired();

        builder.Property(s => s.RevokedAtUtc)
            .IsRequired(false);

        builder.Property(s => s.RevokedReason)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(s => s.Platform)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.AppVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(s => s.OsVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(s => s.DeviceDisplayName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.BiometricEnabled)
            .IsRequired();

        builder.Property(s => s.LastPrimaryAuthenticationAtUtc)
            .IsRequired();

        builder.Property(s => s.RiskState)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.RowVersion)
            .IsRequired();

        builder.HasIndex(s => s.RefreshTokenHash)
            .IsUnique();

        builder.HasIndex(s => s.UserId);

        builder.HasIndex(s => s.TokenFamilyId);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
