using Intake.Domain.Snapshot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class ApprovedSnapshotSchemaDefinitionConfiguration
    : IEntityTypeConfiguration<ApprovedSnapshotSchemaDefinition>
{
    public void Configure(EntityTypeBuilder<ApprovedSnapshotSchemaDefinition> builder)
    {
        builder.ToTable("ApprovedSnapshotSchemaDefinitions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType("char(36)");
        builder.Property(item => item.Code).HasMaxLength(96).IsRequired();
        builder.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.Version).IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(item => item.UpdatedAt).HasPrecision(6).IsRequired();
        builder.HasIndex(item => new { item.Code, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.Code, item.IsActive });
        builder.HasData(new ApprovedSnapshotSchemaDefinition
        {
            Id = new Guid("019b0b22-7f4d-7e25-9c6c-3b2f6f5a4d11"),
            Code = ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1,
            DisplayName = "Lien Intake Approved Snapshot V1",
            Description = "Product-neutral approved projection contract for downstream adapters.",
            Version = 1,
            IsActive = true,
            IsSystemDefined = true,
            CreatedAt = new DateTimeOffset(new DateTime(2026, 8, 14), TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(new DateTime(2026, 8, 14), TimeSpan.Zero),
        });
    }
}

public sealed class ApprovedIntakeSnapshotConfiguration
    : IEntityTypeConfiguration<ApprovedIntakeSnapshot>
{
    public void Configure(EntityTypeBuilder<ApprovedIntakeSnapshot> builder)
    {
        builder.ToTable("ApprovedIntakeSnapshots");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.Id })
            .HasName("AK_ApprovedIntakeSnapshots_TenantId_Id");
        builder.Property(item => item.Id).HasColumnType("char(36)");
        builder.Property(item => item.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.ArtifactId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.ReviewId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.PolicyEvaluationId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.ClassificationId).HasColumnType("char(36)");
        builder.Property(item => item.ArtifactExtractionId).HasColumnType("char(36)");
        builder.Property(item => item.ArtifactNormalizationId).HasColumnType("char(36)");
        builder.Property(item => item.ArtifactMatchRunId).HasColumnType("char(36)");
        builder.Property(item => item.ProcessingProfileCode).HasMaxLength(96).IsRequired();
        builder.Property(item => item.SchemaCode).HasMaxLength(96).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("longtext").IsRequired();
        builder.Property(item => item.SnapshotHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ExecutionKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ActiveCurrentKey).HasMaxLength(96);
        builder.Property(item => item.ApprovedByUserId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.SupersedesSnapshotId).HasColumnType("char(36)");
        builder.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(item => item.UpdatedAt).HasPrecision(6).IsRequired();
        builder.HasOne<Intake.Domain.Artifacts.IntakeArtifact>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ArtifactId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Intake.Domain.Review.IntakeReview>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ReviewId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.ExecutionKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ArtifactId, item.SnapshotVersion }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ArtifactId, item.IsCurrent });
        builder.HasIndex(item => new { item.TenantId, item.ActiveCurrentKey }).IsUnique();
    }
}

public sealed class IntakeAdapterExecutionConfiguration
    : IEntityTypeConfiguration<IntakeAdapterExecution>
{
    public void Configure(EntityTypeBuilder<IntakeAdapterExecution> builder)
    {
        builder.ToTable("IntakeAdapterExecutions");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.Id })
            .HasName("AK_IntakeAdapterExecutions_TenantId_Id");
        builder.Property(item => item.Id).HasColumnType("char(36)");
        builder.Property(item => item.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.SnapshotId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.AdapterCode).HasMaxLength(96).IsRequired();
        builder.Property(item => item.AdapterVersion).HasMaxLength(32).IsRequired();
        builder.Property(item => item.ExecutionKey).HasMaxLength(240).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(item => item.ClaimToken).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.RequestedByUserId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.FailureCode).HasMaxLength(96);
        builder.Property(item => item.FailureMessage).HasMaxLength(1000);
        builder.Property(item => item.ResultJson).HasColumnType("text").IsRequired();
        builder.Property(item => item.Version).IsConcurrencyToken().ValueGeneratedNever();
        builder.Property(item => item.RequestedAt).HasPrecision(6).IsRequired();
        builder.Property(item => item.StartedAt).HasPrecision(6);
        builder.Property(item => item.CompletedAt).HasPrecision(6);
        builder.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(item => item.UpdatedAt).HasPrecision(6).IsRequired();
        builder.HasOne<ApprovedIntakeSnapshot>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SnapshotId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Attempts).WithOne()
            .HasForeignKey(item => new { item.TenantId, item.AdapterExecutionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.ExternalReferences).WithOne()
            .HasForeignKey(item => new { item.TenantId, item.AdapterExecutionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.TenantId, item.ExecutionKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SnapshotId, item.AdapterCode });
    }
}

public sealed class IntakeAdapterExecutionAttemptConfiguration
    : IEntityTypeConfiguration<IntakeAdapterExecutionAttempt>
{
    public void Configure(EntityTypeBuilder<IntakeAdapterExecutionAttempt> builder)
    {
        builder.ToTable("IntakeAdapterExecutionAttempts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType("char(36)");
        builder.Property(item => item.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.AdapterExecutionId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.FailureCode).HasMaxLength(96);
        builder.Property(item => item.FailureMessage).HasMaxLength(1000);
        builder.Property(item => item.StartedAt).HasPrecision(6).IsRequired();
        builder.Property(item => item.CompletedAt).HasPrecision(6);
        builder.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.AdapterExecutionId, item.AttemptNumber }).IsUnique();
    }
}

public sealed class IntakeAdapterExternalReferenceConfiguration
    : IEntityTypeConfiguration<IntakeAdapterExternalReference>
{
    public void Configure(EntityTypeBuilder<IntakeAdapterExternalReference> builder)
    {
        builder.ToTable("IntakeAdapterExternalReferences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType("char(36)");
        builder.Property(item => item.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.AdapterExecutionId).HasColumnType("char(36)").IsRequired();
        builder.Property(item => item.ReferenceType).HasMaxLength(96).IsRequired();
        builder.Property(item => item.ReferenceId).HasMaxLength(256).IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.AdapterExecutionId });
    }
}