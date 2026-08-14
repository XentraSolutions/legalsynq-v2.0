using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;
using Intake.Domain.Review;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class IntakeReviewConfiguration : IEntityTypeConfiguration<IntakeReview>
{
    public void Configure(EntityTypeBuilder<IntakeReview> builder)
    {
        builder.ToTable("IntakeReviews");
        builder.HasKey(review => review.Id);
        builder.HasAlternateKey(review => new { review.TenantId, review.Id })
            .HasName("AK_IntakeReviews_TenantId_Id");
        builder.Property(review => review.Id).HasColumnType("char(36)");
        builder.Property(review => review.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(review => review.ArtifactId).HasColumnType("char(36)").IsRequired();
        builder.Property(review => review.ClassificationId).HasColumnType("char(36)");
        builder.Property(review => review.ArtifactExtractionId).HasColumnType("char(36)");
        builder.Property(review => review.ArtifactNormalizationId).HasColumnType("char(36)");
        builder.Property(review => review.ArtifactMatchRunId).HasColumnType("char(36)");
        builder.Property(review => review.ArtifactPolicyEvaluationId)
            .HasColumnType("char(36)")
            .IsRequired();
        builder.Property(review => review.Status).HasMaxLength(24).IsRequired();
        builder.Property(review => review.Priority).HasMaxLength(16).IsRequired();
        builder.Property(review => review.ReviewOutcome).HasMaxLength(40).IsRequired();
        builder.Property(review => review.B11Disposition).HasMaxLength(32).IsRequired();
        builder.Property(review => review.ClassificationCode).HasMaxLength(64).IsRequired();
        builder.Property(review => review.SourceType).HasMaxLength(32).IsRequired();
        builder.Property(review => review.CompletionReasonCode).HasMaxLength(64);
        builder.Property(review => review.CompletionComment).HasMaxLength(2_000);
        builder.Property(review => review.ActiveContextKey).HasMaxLength(80);
        builder.Property(review => review.Version)
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        builder.Property(review => review.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(review => review.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(review => review.AssignedAt).HasPrecision(6);
        builder.Property(review => review.StartedAt).HasPrecision(6);
        builder.Property(review => review.CompletedAt).HasPrecision(6);

        builder.HasOne<IntakeArtifact>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.ArtifactId })
            .HasPrincipalKey(artifact => new { artifact.TenantId, artifact.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactClassification>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.ClassificationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactExtraction>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.ArtifactExtractionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactNormalization>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.ArtifactNormalizationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactMatchRun>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.ArtifactMatchRunId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArtifactPolicyEvaluation>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.ArtifactPolicyEvaluationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(review => review.Corrections)
            .WithOne()
            .HasForeignKey(item => new { item.TenantId, item.IntakeReviewId })
            .HasPrincipalKey(review => new { review.TenantId, review.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(review => review.MatchDecisions)
            .WithOne()
            .HasForeignKey(item => new { item.TenantId, item.IntakeReviewId })
            .HasPrincipalKey(review => new { review.TenantId, review.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(review => review.DuplicateDecisions)
            .WithOne()
            .HasForeignKey(item => new { item.TenantId, item.IntakeReviewId })
            .HasPrincipalKey(review => new { review.TenantId, review.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(review => review.FindingDecisions)
            .WithOne()
            .HasForeignKey(item => new { item.TenantId, item.IntakeReviewId })
            .HasPrincipalKey(review => new { review.TenantId, review.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(review => review.Activities)
            .WithOne()
            .HasForeignKey(item => new { item.TenantId, item.IntakeReviewId })
            .HasPrincipalKey(review => new { review.TenantId, review.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(review => new { review.TenantId, review.Status });
        builder.HasIndex(review => new { review.TenantId, review.Priority });
        builder.HasIndex(review => new { review.TenantId, review.AssignedToUserId, review.Status });
        builder.HasIndex(review => new { review.TenantId, review.CreatedAt });
        builder.HasIndex(review => review.ArtifactId);
        builder.HasIndex(review => review.ArtifactPolicyEvaluationId);
        builder.HasIndex(review => new { review.TenantId, review.ActiveContextKey })
            .IsUnique();
    }
}

public abstract class IntakeReviewChildConfiguration<TEntity>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : class
{
    public abstract void Configure(EntityTypeBuilder<TEntity> builder);

    protected static void ConfigureBase(
        EntityTypeBuilder<TEntity> builder,
        string table,
        Action<EntityTypeBuilder<TEntity>> configure)
    {
        builder.ToTable(table);
        builder.HasKey("Id");
        builder.Property<Guid>("Id").HasColumnType("char(36)");
        builder.Property<Guid>("TenantId").HasColumnType("char(36)").IsRequired();
        builder.Property<Guid>("IntakeReviewId").HasColumnType("char(36)").IsRequired();
        configure(builder);
        builder.HasIndex("TenantId", "IntakeReviewId");
    }
}

public sealed class IntakeReviewCorrectionConfiguration
    : IntakeReviewChildConfiguration<IntakeReviewCorrection>
{
    public override void Configure(EntityTypeBuilder<IntakeReviewCorrection> builder)
    {
        ConfigureBase(builder, "IntakeReviewCorrections", b =>
        {
            b.Property(item => item.FactCode).HasMaxLength(64).IsRequired();
            b.Property(item => item.TargetType).HasMaxLength(32).IsRequired();
            b.Property(item => item.CorrectionType).HasMaxLength(32).IsRequired();
            b.Property(item => item.CorrectedValue).HasMaxLength(4_000);
            b.Property(item => item.CorrectedJson).HasColumnType("longtext");
            b.Property(item => item.NormalizedValue).HasMaxLength(4_000);
            b.Property(item => item.ValidationStatus).HasMaxLength(32);
            b.Property(item => item.SourceType).HasMaxLength(16).IsRequired();
            b.Property(item => item.ReasonCode).HasMaxLength(64).IsRequired();
            b.Property(item => item.Comment).HasMaxLength(2_000);
            b.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        });
    }
}

public sealed class IntakeReviewMatchDecisionConfiguration
    : IntakeReviewChildConfiguration<IntakeReviewMatchDecision>
{
    public override void Configure(EntityTypeBuilder<IntakeReviewMatchDecision> builder)
    {
        ConfigureBase(builder, "IntakeReviewMatchDecisions", b =>
        {
            b.Property(item => item.EntityType).HasMaxLength(32).IsRequired();
            b.Property(item => item.ArtifactEntityMatchId).HasColumnType("char(36)");
            b.Property(item => item.CandidateEntityId).HasColumnType("char(36)");
            b.Property(item => item.Decision).HasMaxLength(32).IsRequired();
            b.Property(item => item.ReasonCode).HasMaxLength(64).IsRequired();
            b.Property(item => item.Comment).HasMaxLength(2_000);
            b.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        });
    }
}

public sealed class IntakeReviewDuplicateDecisionConfiguration
    : IntakeReviewChildConfiguration<IntakeReviewDuplicateDecision>
{
    public override void Configure(EntityTypeBuilder<IntakeReviewDuplicateDecision> builder)
    {
        ConfigureBase(builder, "IntakeReviewDuplicateDecisions", b =>
        {
            b.Property(item => item.ArtifactDuplicateSignalId).HasColumnType("char(36)");
            b.Property(item => item.Decision).HasMaxLength(32).IsRequired();
            b.Property(item => item.RelatedArtifactId).HasColumnType("char(36)");
            b.Property(item => item.ReasonCode).HasMaxLength(64).IsRequired();
            b.Property(item => item.Comment).HasMaxLength(2_000);
            b.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        });
    }
}

public sealed class IntakeReviewFindingDecisionConfiguration
    : IntakeReviewChildConfiguration<IntakeReviewFindingDecision>
{
    public override void Configure(EntityTypeBuilder<IntakeReviewFindingDecision> builder)
    {
        ConfigureBase(builder, "IntakeReviewFindingDecisions", b =>
        {
            b.Property(item => item.ArtifactPolicyFindingId).HasColumnType("char(36)");
            b.Property(item => item.Decision).HasMaxLength(32).IsRequired();
            b.Property(item => item.ReasonCode).HasMaxLength(64).IsRequired();
            b.Property(item => item.Comment).HasMaxLength(2_000);
            b.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
        });
    }
}

public sealed class IntakeReviewActivityConfiguration
    : IntakeReviewChildConfiguration<IntakeReviewActivity>
{
    public override void Configure(EntityTypeBuilder<IntakeReviewActivity> builder)
    {
        ConfigureBase(builder, "IntakeReviewActivities", b =>
        {
            b.Property(item => item.ActivityType).HasMaxLength(48).IsRequired();
            b.Property(item => item.ActorUserId).HasColumnType("char(36)");
            b.Property(item => item.SafeMetadataJson).HasColumnType("text").IsRequired();
            b.Property(item => item.CreatedAt).HasPrecision(6).IsRequired();
            b.HasIndex("CreatedAt");
        });
    }
}