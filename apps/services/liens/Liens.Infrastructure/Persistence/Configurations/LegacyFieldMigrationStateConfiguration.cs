using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LegacyFieldMigrationStateConfiguration : IEntityTypeConfiguration<LegacyFieldMigrationState>
{
    public void Configure(EntityTypeBuilder<LegacyFieldMigrationState> builder)
    {
        builder.ToTable("liens_LegacyFieldMigrationStates");
        builder.HasKey(state => state.Id);

        builder.Property(state => state.TenantId).IsRequired();
        builder.Property(state => state.SourceSystem).IsRequired().HasMaxLength(100);
        builder.Property(state => state.SourceTable).IsRequired().HasMaxLength(100);
        builder.Property(state => state.LegacyId).IsRequired().HasMaxLength(100);
        builder.Property(state => state.MappingVersion).IsRequired().HasMaxLength(100);
        builder.Property(state => state.FieldGroup).IsRequired().HasMaxLength(100);
        builder.Property(state => state.TargetEntity).IsRequired().HasMaxLength(100);
        builder.Property(state => state.TargetId).IsRequired();
        builder.Property(state => state.SourceHash).IsRequired().HasMaxLength(128);
        builder.Property(state => state.TargetPreimageHash).HasMaxLength(128);
        builder.Property(state => state.AppliedValueHash).HasMaxLength(128);
        builder.Property(state => state.Status).IsRequired().HasMaxLength(30);
        builder.Property(state => state.ImportRunId).IsRequired();
        builder.Property(state => state.CreatedAtUtc).IsRequired();

        builder.HasIndex(state => new
            {
                state.TenantId,
                state.SourceSystem,
                state.SourceTable,
                state.LegacyId,
                state.MappingVersion,
                state.FieldGroup,
            })
            .IsUnique()
            .HasDatabaseName("UX_LegacyFieldMigrationStates_Source_FieldGroup");

        builder.HasIndex(state => state.ImportRunId)
            .HasDatabaseName("IX_LegacyFieldMigrationStates_ImportRunId");

        builder.HasOne<LegacyImportRun>()
            .WithMany()
            .HasForeignKey(state => state.ImportRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LegacyFieldMigrationStates_ImportRun");
    }
}
