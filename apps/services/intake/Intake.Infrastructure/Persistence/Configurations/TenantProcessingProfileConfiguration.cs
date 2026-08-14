using Intake.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class TenantProcessingProfileConfiguration
    : IEntityTypeConfiguration<TenantProcessingProfile>
{
    public void Configure(EntityTypeBuilder<TenantProcessingProfile> builder)
    {
        builder.ToTable("TenantProcessingProfiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id).HasColumnType("char(36)");
        builder.Property(profile => profile.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(profile => profile.ProcessingProfileDefinitionId)
            .HasColumnType("char(36)")
            .IsRequired();
        builder.Property(profile => profile.IsEnabled).IsRequired();
        builder.Property(profile => profile.IsDefault).IsRequired();
        builder.Property(profile => profile.DefaultTenantKey).HasMaxLength(32);
        builder.Property(profile => profile.ConfigurationJson).HasColumnType("longtext").IsRequired();
        builder.Property(profile => profile.ConfigurationVersion).IsRequired();
        builder.Property(profile => profile.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(profile => profile.CreatedBy).HasColumnType("char(36)");
        builder.Property(profile => profile.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(profile => profile.UpdatedBy).HasColumnType("char(36)");

        builder.HasIndex(profile => new { profile.TenantId, profile.ProcessingProfileDefinitionId })
            .IsUnique();
        // MySQL permits multiple NULL values in a unique index. This gives
        // each tenant many non-default profiles but at most one default.
        builder.HasIndex(profile => profile.DefaultTenantKey).IsUnique();
        builder.HasIndex(profile => new { profile.TenantId, profile.IsDefault });
        builder.HasIndex(profile => new { profile.TenantId, profile.IsEnabled });
        builder.Property(profile => profile.ConfigurationVersion)
            .IsConcurrencyToken();

        builder.HasOne(profile => profile.ProcessingProfileDefinition)
            .WithMany(definition => definition.TenantAssignments)
            .HasForeignKey(profile => profile.ProcessingProfileDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}