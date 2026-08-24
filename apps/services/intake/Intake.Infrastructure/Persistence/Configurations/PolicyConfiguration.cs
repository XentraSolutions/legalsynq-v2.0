using Intake.Application.Policy;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class PolicyProfileDefinitionConfiguration
    : IEntityTypeConfiguration<PolicyProfileDefinition>
{
    public void Configure(EntityTypeBuilder<PolicyProfileDefinition> builder)
    {
        builder.ToTable("PolicyProfileDefinitions");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).HasColumnType("char(36)");
        builder.Property(profile => profile.Code).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.Description).HasMaxLength(1_000);
        builder.Property(profile => profile.DefinitionJson)
            .HasColumnType("longtext")
            .IsRequired();
        builder.Property(profile => profile.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(profile => profile.UpdatedAt).HasPrecision(6).IsRequired();
        builder.HasIndex(profile => new { profile.Code, profile.Version }).IsUnique();
        builder.HasData(new PolicyProfileDefinition
        {
            Id = PolicyDefinitionIds.LienIntakePolicyV1,
            Code = PolicyProfileDefaults.Code,
            DisplayName = "Lien Intake Policy V1",
            Description = "Deterministic confidence, safety, duplicate, evidence, and policy disposition rules.",
            Version = PolicyProfileDefaults.Version,
            IsActive = true,
            IsSystemDefined = true,
            DefinitionJson = PolicyProfileDefaults.DefinitionJson,
            CreatedAt = PolicyDefinitionIds.SeedTimestamp,
            UpdatedAt = PolicyDefinitionIds.SeedTimestamp,
        });
    }
}

public sealed class ArtifactPolicyEvaluationConfiguration
    : IEntityTypeConfiguration<ArtifactPolicyEvaluation>
{
    public void Configure(EntityTypeBuilder<ArtifactPolicyEvaluation> builder)
    {
        builder.ToTable("ArtifactPolicyEvaluations");
        builder.HasKey(evaluation => evaluation.Id);
        builder.HasAlternateKey(evaluation => new
        {
            evaluation.TenantId,
            evaluation.Id,
        }).HasName("AK_ArtifactPolicyEvaluations_TenantId_Id");
        builder.Property(evaluation => evaluation.Id).HasColumnType("char(36)");
        builder.Property(evaluation => evaluation.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(evaluation => evaluation.ArtifactId).HasColumnType("char(36)").IsRequired();
        builder.Property(evaluation => evaluation.ClassificationId).HasColumnType("char(36)");
        builder.Property(evaluation => evaluation.ArtifactExtractionId).HasColumnType("char(36)");
        builder.Property(evaluation => evaluation.ArtifactNormalizationId).HasColumnType("char(36)");
        builder.Property(evaluation => evaluation.ArtifactMatchRunId).HasColumnType("char(36)");
        builder.Property(evaluation => evaluation.PolicyProfileCode).HasMaxLength(64).IsRequired();
        builder.Property(evaluation => evaluation.Status).HasMaxLength(32).IsRequired();
        builder.Property(evaluation => evaluation.Disposition).HasMaxLength(32).IsRequired();
        builder.Property(evaluation => evaluation.OverallConfidence)
            .HasColumnType("decimal(6,5)")
            .IsRequired();
        builder.Property(evaluation => evaluation.ReviewPriority).HasMaxLength(16).IsRequired();
        builder.Property(evaluation => evaluation.ExecutionKey).HasMaxLength(192).IsRequired();
        builder.Property(evaluation => evaluation.CurrentResultMarker).HasMaxLength(16);
        builder.Property(evaluation => evaluation.FailureCode).HasMaxLength(96);
        builder.Property(evaluation => evaluation.FailureMessage).HasMaxLength(1_000);
        builder.Property(evaluation => evaluation.RequestedAt).HasPrecision(6).IsRequired();
        builder.Property(evaluation => evaluation.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(evaluation => evaluation.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(evaluation => evaluation.CompletedAt).HasPrecision(6);

        builder.HasOne<IntakeArtifact>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.TenantId,
                evaluation.ArtifactId,
            })
            .HasPrincipalKey(artifact => new { artifact.TenantId, artifact.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactClassification>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.TenantId,
                evaluation.ClassificationId,
            })
            .HasPrincipalKey(classification => new
            {
                classification.TenantId,
                classification.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactExtraction>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.TenantId,
                evaluation.ArtifactExtractionId,
            })
            .HasPrincipalKey(extraction => new
            {
                extraction.TenantId,
                extraction.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactNormalization>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.TenantId,
                evaluation.ArtifactNormalizationId,
            })
            .HasPrincipalKey(normalization => new
            {
                normalization.TenantId,
                normalization.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactMatchRun>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.TenantId,
                evaluation.ArtifactMatchRunId,
            })
            .HasPrincipalKey(run => new { run.TenantId, run.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(evaluation => evaluation.Findings)
            .WithOne()
            .HasForeignKey(finding => new
            {
                finding.TenantId,
                finding.ArtifactPolicyEvaluationId,
            })
            .HasPrincipalKey(evaluation => new
            {
                evaluation.TenantId,
                evaluation.Id,
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(evaluation => evaluation.ExecutionKey).IsUnique();
        builder.HasIndex(evaluation => new
        {
            evaluation.TenantId,
            evaluation.ArtifactId,
            evaluation.CurrentResultMarker,
        }).IsUnique();
        builder.HasIndex(evaluation => new
        {
            evaluation.TenantId,
            evaluation.ArtifactId,
            evaluation.CreatedAt,
        });
    }
}

public sealed class ArtifactPolicyFindingConfiguration
    : IEntityTypeConfiguration<ArtifactPolicyFinding>
{
    public void Configure(EntityTypeBuilder<ArtifactPolicyFinding> builder)
    {
        builder.ToTable("ArtifactPolicyFindings");
        builder.HasKey(finding => finding.Id);
        builder.Property(finding => finding.Id).HasColumnType("char(36)");
        builder.Property(finding => finding.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(finding => finding.ArtifactPolicyEvaluationId)
            .HasColumnType("char(36)")
            .IsRequired();
        builder.Property(finding => finding.RuleCode).HasMaxLength(64).IsRequired();
        builder.Property(finding => finding.RuleCategory).HasMaxLength(32).IsRequired();
        builder.Property(finding => finding.Severity).HasMaxLength(16).IsRequired();
        builder.Property(finding => finding.Outcome).HasMaxLength(24).IsRequired();
        builder.Property(finding => finding.ReasonCode).HasMaxLength(128).IsRequired();
        builder.Property(finding => finding.EntityType).HasMaxLength(64);
        builder.Property(finding => finding.FactCode).HasMaxLength(64);
        builder.Property(finding => finding.RelatedEntityMatchId).HasColumnType("char(36)");
        builder.Property(finding => finding.RelatedDuplicateSignalId).HasColumnType("char(36)");
        builder.Property(finding => finding.RelatedNormalizedFactId).HasColumnType("char(36)");
        builder.Property(finding => finding.Score).HasColumnType("decimal(6,5)");
        builder.Property(finding => finding.Threshold).HasColumnType("decimal(6,5)");
        builder.Property(finding => finding.EvidenceReferenceJson)
            .HasColumnType("text")
            .IsRequired();
        builder.Property(finding => finding.CreatedAt).HasPrecision(6).IsRequired();
        builder.HasIndex(finding => new
        {
            finding.TenantId,
            finding.ArtifactPolicyEvaluationId,
            finding.RuleCode,
            finding.ReasonCode,
        });
    }
}