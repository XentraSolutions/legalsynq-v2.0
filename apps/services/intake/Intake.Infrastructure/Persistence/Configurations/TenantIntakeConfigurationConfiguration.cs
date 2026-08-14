using Intake.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class TenantIntakeConfigurationConfiguration
    : IEntityTypeConfiguration<TenantIntakeConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantIntakeConfiguration> builder)
    {
        builder.ToTable("TenantIntakeConfigurations");
        builder.HasKey(configuration => configuration.Id);

        builder.Property(configuration => configuration.Id).HasColumnType("char(36)");
        builder.Property(configuration => configuration.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(configuration => configuration.OrgId).HasColumnType("char(36)");
        builder.Property(configuration => configuration.DefaultProcessingProfileCode).HasMaxLength(64);
        builder.Property(configuration => configuration.IsEnabled).IsRequired();
        builder.Property(configuration => configuration.RequireHumanReviewByDefault).IsRequired();
        builder.Property(configuration => configuration.AutoProcessingEnabled).IsRequired();
        builder.Property(configuration => configuration.ConfigurationVersion).IsRequired();
        builder.Property(configuration => configuration.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(configuration => configuration.CreatedBy).HasColumnType("char(36)");
        builder.Property(configuration => configuration.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(configuration => configuration.UpdatedBy).HasColumnType("char(36)");

        builder.HasIndex(configuration => configuration.TenantId).IsUnique();
        builder.HasIndex(configuration => configuration.DefaultProcessingProfileCode);
        builder.Property(configuration => configuration.ConfigurationVersion)
            .IsConcurrencyToken();
    }
}