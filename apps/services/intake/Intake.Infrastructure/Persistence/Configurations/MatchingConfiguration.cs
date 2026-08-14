using Intake.Application.Matching;
using Intake.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class MatchingProfileDefinitionConfiguration
    : IEntityTypeConfiguration<MatchingProfileDefinition>
{
    public void Configure(EntityTypeBuilder<MatchingProfileDefinition> builder)
    {
        builder.ToTable("MatchingProfileDefinitions");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Code).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.Description).HasMaxLength(1_000);
        builder.Property(profile => profile.ScoringVersion).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.DefinitionJson).HasColumnType("longtext").IsRequired();
        builder.HasIndex(profile => new { profile.Code, profile.Version }).IsUnique();
        builder.HasData(new MatchingProfileDefinition
        {
            Id = MatchingDefinitionIds.LienIntakeMatchingProfileV1,
            Code = MatchingProfileDefaults.Code,
            DisplayName = "Lien Intake Matching V1",
            Description = "Deterministic tenant-scoped candidate matching and duplicate signals.",
            Version = MatchingProfileDefaults.Version,
            ScoringVersion = MatchingProfileDefaults.ScoringVersion,
            IsActive = true,
            IsSystemDefined = true,
            DefinitionJson = MatchingProfileDefaults.DefinitionJson,
            CreatedAt = MatchingDefinitionIds.SeedTimestamp,
            UpdatedAt = MatchingDefinitionIds.SeedTimestamp,
        });
    }
}

public sealed class ArtifactMatchRunConfiguration
    : IEntityTypeConfiguration<ArtifactMatchRun>
{
    public void Configure(EntityTypeBuilder<ArtifactMatchRun> builder)
    {
        builder.ToTable("ArtifactMatchRuns");
        builder.HasKey(run => run.Id);
        builder.HasAlternateKey(run => new { run.TenantId, run.Id })
            .HasName("AK_ArtifactMatchRuns_TenantId_Id");
        builder.Property(run => run.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(run => run.IntakeArtifactId).HasColumnType("char(36)").IsRequired();
        builder.Property(run => run.ArtifactNormalizationId).HasColumnType("char(36)").IsRequired();
        builder.Property(run => run.MatchingProfileCode).HasMaxLength(64).IsRequired();
        builder.Property(run => run.ScoringVersion).HasMaxLength(64).IsRequired();
        builder.Property(run => run.Status).HasMaxLength(32).IsRequired();
        builder.Property(run => run.ExecutionKey).HasMaxLength(128).IsRequired();
        builder.Property(run => run.CurrentResultMarker).HasMaxLength(32);
        builder.Property(run => run.BusinessKeyFingerprint).HasMaxLength(128);
        builder.Property(run => run.BusinessDuplicateRuleCode).HasMaxLength(128);
        builder.Property(run => run.FailureCode).HasMaxLength(96);
        builder.Property(run => run.FailureMessage).HasMaxLength(1_000);
        builder.HasIndex(run => run.ExecutionKey).IsUnique();
        builder.HasIndex(run => new { run.TenantId, run.IntakeArtifactId, run.CurrentResultMarker })
            .IsUnique();
        builder.HasIndex(run => new
        {
            run.TenantId,
            run.ArtifactNormalizationId,
            run.Status,
        });
        builder.HasIndex(run => new
        {
            run.TenantId,
            run.BusinessKeyFingerprint,
            run.Status,
        });
        builder.HasOne<Intake.Domain.Artifacts.IntakeArtifact>()
            .WithMany()
            .HasForeignKey(run => new { run.TenantId, run.IntakeArtifactId })
            .HasPrincipalKey(artifact => new { artifact.TenantId, artifact.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Intake.Domain.Normalization.ArtifactNormalization>()
            .WithMany()
            .HasForeignKey(run => new { run.TenantId, run.ArtifactNormalizationId })
            .HasPrincipalKey(normalization => new
            {
                normalization.TenantId,
                normalization.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ArtifactEntityMatchConfiguration
    : IEntityTypeConfiguration<ArtifactEntityMatch>
{
    public void Configure(EntityTypeBuilder<ArtifactEntityMatch> builder)
    {
        builder.ToTable("ArtifactEntityMatches");
        builder.HasKey(match => match.Id);
        builder.HasAlternateKey(match => new { match.TenantId, match.Id })
            .HasName("AK_ArtifactEntityMatches_TenantId_Id");
        builder.Property(match => match.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(match => match.ArtifactMatchRunId).HasColumnType("char(36)").IsRequired();
        builder.Property(match => match.EntityType).HasMaxLength(32).IsRequired();
        builder.Property(match => match.CandidateEntityId).HasColumnType("char(36)").IsRequired();
        builder.Property(match => match.CandidateDisplayLabel).HasMaxLength(256).IsRequired();
        builder.Property(match => match.Score).HasColumnType("decimal(6,5)").IsRequired();
        builder.Property(match => match.MatchStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(match => new
        {
            match.ArtifactMatchRunId,
            match.EntityType,
            match.CandidateEntityId,
        }).IsUnique();
        builder.HasIndex(match => new
        {
            match.ArtifactMatchRunId,
            match.EntityType,
            match.Rank,
        });
        builder.HasOne<ArtifactMatchRun>()
            .WithMany(run => run.EntityMatches)
            .HasForeignKey(match => new { match.TenantId, match.ArtifactMatchRunId })
            .HasPrincipalKey(run => new { run.TenantId, run.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ArtifactMatchFieldConfiguration
    : IEntityTypeConfiguration<ArtifactMatchField>
{
    public void Configure(EntityTypeBuilder<ArtifactMatchField> builder)
    {
        builder.ToTable("ArtifactMatchFields");
        builder.HasKey(field => field.Id);
        builder.Property(field => field.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(field => field.ArtifactEntityMatchId).HasColumnType("char(36)").IsRequired();
        builder.Property(field => field.SourceNormalizedFactId).HasColumnType("char(36)");
        builder.Property(field => field.FactCode).HasMaxLength(96).IsRequired();
        builder.Property(field => field.CandidateFieldName).HasMaxLength(96).IsRequired();
        builder.Property(field => field.ComparisonMethod).HasMaxLength(64).IsRequired();
        builder.Property(field => field.FieldScore).HasColumnType("decimal(6,5)").IsRequired();
        builder.Property(field => field.Weight).HasColumnType("decimal(6,5)").IsRequired();
        builder.Property(field => field.EffectiveWeight).HasColumnType("decimal(6,5)").IsRequired();
        builder.Property(field => field.WeightedScore).HasColumnType("decimal(8,5)").IsRequired();
        builder.Property(field => field.MatchOutcome).HasMaxLength(32).IsRequired();
        builder.Property(field => field.ReasonCode).HasMaxLength(96).IsRequired();
        builder.HasIndex(field => new
        {
            field.ArtifactEntityMatchId,
            field.SourceNormalizedFactId,
            field.CandidateFieldName,
        }).IsUnique();
        builder.HasIndex(field => new
        {
            field.ArtifactEntityMatchId,
            field.SourceNormalizedFactId,
            field.FactCode,
        });
        builder.HasOne<ArtifactEntityMatch>()
            .WithMany(match => match.Fields)
            .HasForeignKey(field => new { field.TenantId, field.ArtifactEntityMatchId })
            .HasPrincipalKey(match => new { match.TenantId, match.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ArtifactDuplicateSignalConfiguration
    : IEntityTypeConfiguration<ArtifactDuplicateSignal>
{
    public void Configure(EntityTypeBuilder<ArtifactDuplicateSignal> builder)
    {
        builder.ToTable("ArtifactDuplicateSignals");
        builder.HasKey(signal => signal.Id);
        builder.Property(signal => signal.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(signal => signal.ArtifactMatchRunId).HasColumnType("char(36)").IsRequired();
        builder.Property(signal => signal.DuplicateType).HasMaxLength(64).IsRequired();
        builder.Property(signal => signal.RelatedArtifactId).HasColumnType("char(36)");
        builder.Property(signal => signal.RelatedBusinessEntityType).HasMaxLength(32);
        builder.Property(signal => signal.RelatedBusinessEntityId).HasColumnType("char(36)");
        builder.Property(signal => signal.Score).HasColumnType("decimal(6,5)").IsRequired();
        builder.Property(signal => signal.Status).HasMaxLength(32).IsRequired();
        builder.Property(signal => signal.ReasonCode).HasMaxLength(96).IsRequired();
        builder.Property(signal => signal.EvidenceJson).HasColumnType("text").IsRequired();
        builder.HasIndex(signal => new
        {
            signal.ArtifactMatchRunId,
            signal.DuplicateType,
            signal.RelatedArtifactId,
        });
        builder.HasOne<ArtifactMatchRun>()
            .WithMany(run => run.DuplicateSignals)
            .HasForeignKey(signal => new { signal.TenantId, signal.ArtifactMatchRunId })
            .HasPrincipalKey(run => new { run.TenantId, run.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}