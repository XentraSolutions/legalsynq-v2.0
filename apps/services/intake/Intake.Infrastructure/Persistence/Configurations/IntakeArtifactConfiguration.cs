using Intake.Domain.Artifacts;
using Intake.Domain.Sources;
using Intake.Domain.Manual;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class IntakeArtifactConfiguration : IEntityTypeConfiguration<IntakeArtifact>
{
    public void Configure(EntityTypeBuilder<IntakeArtifact> builder)
    {
        builder.ToTable("IntakeArtifacts");
        builder.HasKey(artifact => artifact.Id);
        builder.HasAlternateKey(artifact => new { artifact.TenantId, artifact.Id })
            .HasName("AK_IntakeArtifacts_TenantId_Id");

        builder.Property(artifact => artifact.Id).HasColumnType("char(36)");
        builder.Property(artifact => artifact.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(artifact => artifact.OrgId).HasColumnType("char(36)");
        builder.Property(artifact => artifact.InboundEmailId).HasColumnType("char(36)");
        builder.Property(artifact => artifact.ManualIntakeSubmissionId).HasColumnType("char(36)");
        builder.Property(artifact => artifact.TenantIntakeSourceId).HasColumnType("char(36)");
        builder.Property(artifact => artifact.ArtifactSourceType).HasMaxLength(32).IsRequired();
        builder.Property(artifact => artifact.SourceAttachmentMetadataId).HasColumnType("char(36)");

        builder.Property(artifact => artifact.ArtifactKey).HasMaxLength(128).IsRequired();
        builder.Property(artifact => artifact.ArtifactType).HasMaxLength(32).IsRequired();
        builder.Property(artifact => artifact.ArtifactRole).HasMaxLength(32).IsRequired();
        builder.Property(artifact => artifact.SourceContentId).HasMaxLength(512);
        builder.Property(artifact => artifact.OriginalFileName).HasMaxLength(512).IsRequired();
        builder.Property(artifact => artifact.EffectiveFileName).HasMaxLength(240).IsRequired();
        builder.Property(artifact => artifact.DeclaredContentType).HasMaxLength(255).IsRequired();
        builder.Property(artifact => artifact.DetectedContentType).HasMaxLength(255);
        builder.Property(artifact => artifact.Sha256).HasMaxLength(64);
        builder.Property(artifact => artifact.ProcessingStatus).HasMaxLength(32).IsRequired();
        builder.Property(artifact => artifact.FailureCode).HasMaxLength(64);
        builder.Property(artifact => artifact.FailureMessage).HasMaxLength(1000);
        builder.Property(artifact => artifact.DocumentsServiceDocumentId).HasColumnType("char(36)");
        builder.Property(artifact => artifact.DocumentsServiceVersionId).HasColumnType("char(36)");
        builder.Property(artifact => artifact.DocumentsServiceReference).HasMaxLength(256);
        builder.Property(artifact => artifact.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(artifact => artifact.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(artifact => artifact.UploadedAt).HasPrecision(6);
        builder.Property(artifact => artifact.CompletedAt).HasPrecision(6);

        builder.HasOne<Intake.Domain.Emails.InboundEmail>()
            .WithMany()
            .HasForeignKey(artifact => artifact.InboundEmailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ManualIntakeSubmission>()
            .WithMany()
            .HasForeignKey(artifact => artifact.ManualIntakeSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Intake.Domain.Emails.InboundEmailAttachmentMetadata>()
            .WithMany()
            .HasForeignKey(artifact => artifact.SourceAttachmentMetadataId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TenantIntakeSource>()
            .WithMany()
            .HasForeignKey(artifact => artifact.TenantIntakeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(artifact => new { artifact.InboundEmailId, artifact.ArtifactKey })
            .IsUnique();
        builder.HasIndex(artifact => new { artifact.ManualIntakeSubmissionId, artifact.ArtifactKey })
            .IsUnique();
        builder.HasIndex(artifact => new { artifact.TenantId, artifact.ProcessingStatus, artifact.UpdatedAt });
        builder.HasIndex(artifact => new { artifact.TenantId, artifact.InboundEmailId, artifact.ArtifactOrdinal });
        builder.HasIndex(artifact => new { artifact.TenantId, artifact.ManualIntakeSubmissionId, artifact.ArtifactOrdinal });
        builder.HasIndex(artifact => artifact.DocumentsServiceDocumentId);
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_IntakeArtifacts_ExactlyOneParent",
            "((InboundEmailId IS NOT NULL AND ManualIntakeSubmissionId IS NULL) OR (InboundEmailId IS NULL AND ManualIntakeSubmissionId IS NOT NULL))"));
    }
}