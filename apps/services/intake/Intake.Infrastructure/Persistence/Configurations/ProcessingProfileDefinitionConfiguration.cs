using Intake.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class ProcessingProfileDefinitionConfiguration
    : IEntityTypeConfiguration<ProcessingProfileDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessingProfileDefinition> builder)
    {
        builder.ToTable("ProcessingProfileDefinitions");
        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.Id).HasColumnType("char(36)");
        builder.Property(definition => definition.Code).HasMaxLength(64).IsRequired();
        builder.Property(definition => definition.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(definition => definition.Description).HasMaxLength(1000);
        builder.Property(definition => definition.Version).IsRequired();
        builder.Property(definition => definition.IsActive).IsRequired();
        builder.Property(definition => definition.IsSystemDefined).IsRequired();
        builder.Property(definition => definition.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(definition => definition.UpdatedAt).HasPrecision(6).IsRequired();

        builder.HasIndex(definition => definition.Code).IsUnique();

        builder.HasData(new ProcessingProfileDefinition
        {
            Id = ProcessingProfileDefinitionIds.LienIntakeV1,
            Code = "LIEN_INTAKE_V1",
            DisplayName = "Lien Intake V1",
            Description = "Conservative configuration contract for future lien-intake processing.",
            Version = 1,
            IsActive = true,
            IsSystemDefined = true,
            CreatedAt = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero),
        });
    }
}