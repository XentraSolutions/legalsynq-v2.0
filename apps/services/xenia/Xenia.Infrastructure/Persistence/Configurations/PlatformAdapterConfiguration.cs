using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Adapters;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class PlatformAdapterConfiguration : IEntityTypeConfiguration<PlatformAdapter>
{
    public void Configure(EntityTypeBuilder<PlatformAdapter> builder)
    {
        builder.ToTable("xn_platform_adapters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.AdapterKey)
            .HasColumnName("adapter_key")
            .HasMaxLength(PlatformAdapter.KeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => e.AdapterKey)
            .IsUnique()
            .HasDatabaseName("ix_xn_platform_adapters_key");

        builder.Property(e => e.AdapterType)
            .HasColumnName("adapter_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(PlatformAdapter.NameMaxLength)
            .IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .HasMaxLength(PlatformAdapter.VersionMaxLength)
            .IsRequired();

        builder.Property(e => e.ConfigurationStatus)
            .HasColumnName("configuration_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.AvailabilityStatus)
            .HasColumnName("availability_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.HealthStatus)
            .HasColumnName("health_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.LastHealthCheckAt)
            .HasColumnName("last_health_check_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.DiagnosticMessage)
            .HasColumnName("diagnostic_message")
            .HasMaxLength(PlatformAdapter.DiagnosticMaxLength);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();
    }
}
