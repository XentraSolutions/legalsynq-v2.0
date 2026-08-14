using Intake.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class TenantIntakeSourceConfiguration
    : IEntityTypeConfiguration<TenantIntakeSource>
{
    public void Configure(EntityTypeBuilder<TenantIntakeSource> builder)
    {
        builder.ToTable("TenantIntakeSources");
        builder.HasKey(source => source.Id);

        builder.Property(source => source.Id).HasColumnType("char(36)");
        builder.Property(source => source.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(source => source.OrgId).HasColumnType("char(36)");
        builder.Property(source => source.SourceType).HasMaxLength(32).IsRequired();
        builder.Property(source => source.EmailAddress).HasMaxLength(320).IsRequired();
        builder.Property(source => source.NormalizedEmailAddress)
            .HasMaxLength(320)
            .UseCollation("utf8mb4_bin")
            .IsRequired();
        builder.Property(source => source.Provider).HasMaxLength(64).IsRequired();
        builder.Property(source => source.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(source => source.ProcessingProfileCode).HasMaxLength(64).IsRequired();
        builder.Property(source => source.IsActive).IsRequired();
        builder.Property(source => source.IsDefault).IsRequired();
        builder.Property(source => source.DefaultTenantPurposeKey).HasMaxLength(128);
        builder.Property(source => source.ConnectorConfigurationJson)
            .HasColumnType("longtext")
            .IsRequired();
        builder.Property(source => source.CredentialReference).HasMaxLength(320);
        builder.Property(source => source.ValidationStatus).HasMaxLength(32).IsRequired();
        builder.Property(source => source.LastValidatedAt).HasPrecision(6);
        builder.Property(source => source.LastValidationMessage).HasMaxLength(512);
        builder.Property(source => source.ConfigurationVersion).IsRequired().IsConcurrencyToken();
        builder.Property(source => source.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(source => source.CreatedBy).HasColumnType("char(36)");
        builder.Property(source => source.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(source => source.UpdatedBy).HasColumnType("char(36)");

        builder.HasIndex(source => source.NormalizedEmailAddress).IsUnique();
        builder.HasIndex(source => source.TenantId);
        builder.HasIndex(source => new { source.TenantId, source.Purpose });
        builder.HasIndex(source => new { source.TenantId, source.ProcessingProfileCode });
        builder.HasIndex(source => new
        {
            source.NormalizedEmailAddress,
            source.IsActive,
        });
        // MySQL permits multiple NULL values in a unique index. This gives
        // each tenant/purpose many non-default sources but at most one default.
        builder.HasIndex(source => source.DefaultTenantPurposeKey).IsUnique();
    }
}