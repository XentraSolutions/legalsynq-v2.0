using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>
/// BE-BIO-007: EF Core configuration for the RefreshTokenLedgerEntries table
/// (reuse-detection ledger).
/// </summary>
public class RefreshTokenLedgerEntryConfiguration : IEntityTypeConfiguration<RefreshTokenLedgerEntry>
{
    public void Configure(EntityTypeBuilder<RefreshTokenLedgerEntry> builder)
    {
        builder.ToTable("idt_RefreshTokenLedgerEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DeviceSessionId)
            .IsRequired();

        builder.Property(e => e.TokenFamilyId)
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.IssuedAtUtc)
            .IsRequired();

        builder.Property(e => e.RotatedAtUtc)
            .IsRequired(false);

        builder.Property(e => e.RotatedIntoLedgerEntryId)
            .HasColumnType("char(36)")
            .IsRequired(false);

        builder.HasIndex(e => e.TokenHash)
            .IsUnique();

        builder.HasIndex(e => e.TokenFamilyId);

        builder.HasOne(e => e.DeviceSession)
            .WithMany()
            .HasForeignKey(e => e.DeviceSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
