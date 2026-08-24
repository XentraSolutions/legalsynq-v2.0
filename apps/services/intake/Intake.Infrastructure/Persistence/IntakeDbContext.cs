using Microsoft.EntityFrameworkCore;
using Intake.Domain.Configuration;
using Intake.Domain.Sources;
using Intake.Domain.Emails;
using Intake.Domain.Artifacts;
using Intake.Domain.Manual;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Normalization;
using Intake.Domain.Matching;
using Intake.Domain.Policy;
using Intake.Domain.Review;
using Intake.Domain.Snapshot;
using Intake.Domain.Operations;

namespace Intake.Infrastructure.Persistence;

/// <summary>
/// Dedicated persistence boundary for Synq Intake configuration and future
/// Intake aggregates. This context must not reuse another service's database.
/// </summary>
public sealed class IntakeDbContext(DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
    public DbSet<TenantIntakeConfiguration> TenantIntakeConfigurations => Set<TenantIntakeConfiguration>();
    public DbSet<ProcessingProfileDefinition> ProcessingProfileDefinitions => Set<ProcessingProfileDefinition>();
    public DbSet<TenantProcessingProfile> TenantProcessingProfiles => Set<TenantProcessingProfile>();
    public DbSet<TenantIntakeSource> TenantIntakeSources => Set<TenantIntakeSource>();
    public DbSet<InboundEmail> InboundEmails => Set<InboundEmail>();
    public DbSet<InboundEmailRecipient> InboundEmailRecipients => Set<InboundEmailRecipient>();
    public DbSet<InboundEmailAttachmentMetadata> InboundEmailAttachmentMetadata =>
        Set<InboundEmailAttachmentMetadata>();
    public DbSet<InboundEmailCaptureFailure> InboundEmailCaptureFailures =>
        Set<InboundEmailCaptureFailure>();
    public DbSet<IntakeArtifact> IntakeArtifacts => Set<IntakeArtifact>();
    public DbSet<ManualIntakeSubmission> ManualIntakeSubmissions => Set<ManualIntakeSubmission>();
    public DbSet<TenantAiPolicy> TenantAiPolicies => Set<TenantAiPolicy>();
    public DbSet<ClassificationProfileDefinition> ClassificationProfileDefinitions =>
        Set<ClassificationProfileDefinition>();
    public DbSet<ClassificationTaxonomyDefinition> ClassificationTaxonomyDefinitions =>
        Set<ClassificationTaxonomyDefinition>();
    public DbSet<ClassificationPromptDefinition> ClassificationPromptDefinitions =>
        Set<ClassificationPromptDefinition>();
    public DbSet<ArtifactClassification> ArtifactClassifications => Set<ArtifactClassification>();
    public DbSet<ExtractionProfileDefinition> ExtractionProfileDefinitions =>
        Set<ExtractionProfileDefinition>();
    public DbSet<ExtractionSchemaDefinition> ExtractionSchemaDefinitions =>
        Set<ExtractionSchemaDefinition>();
    public DbSet<ExtractionPromptDefinition> ExtractionPromptDefinitions =>
        Set<ExtractionPromptDefinition>();
    public DbSet<ArtifactExtraction> ArtifactExtractions => Set<ArtifactExtraction>();
    public DbSet<ArtifactExtractedFact> ArtifactExtractedFacts => Set<ArtifactExtractedFact>();
    public DbSet<NormalizationProfileDefinition> NormalizationProfileDefinitions =>
        Set<NormalizationProfileDefinition>();
    public DbSet<ArtifactNormalization> ArtifactNormalizations =>
        Set<ArtifactNormalization>();
    public DbSet<ArtifactNormalizedFact> ArtifactNormalizedFacts =>
        Set<ArtifactNormalizedFact>();
    public DbSet<MatchingProfileDefinition> MatchingProfileDefinitions =>
        Set<MatchingProfileDefinition>();
    public DbSet<ArtifactMatchRun> ArtifactMatchRuns =>
        Set<ArtifactMatchRun>();
    public DbSet<ArtifactEntityMatch> ArtifactEntityMatches =>
        Set<ArtifactEntityMatch>();
    public DbSet<ArtifactMatchField> ArtifactMatchFields =>
        Set<ArtifactMatchField>();
    public DbSet<ArtifactDuplicateSignal> ArtifactDuplicateSignals =>
        Set<ArtifactDuplicateSignal>();
    public DbSet<PolicyProfileDefinition> PolicyProfileDefinitions =>
        Set<PolicyProfileDefinition>();
    public DbSet<ArtifactPolicyEvaluation> ArtifactPolicyEvaluations =>
        Set<ArtifactPolicyEvaluation>();
    public DbSet<ArtifactPolicyFinding> ArtifactPolicyFindings =>
        Set<ArtifactPolicyFinding>();
    public DbSet<IntakeReview> IntakeReviews => Set<IntakeReview>();
    public DbSet<IntakeReviewCorrection> IntakeReviewCorrections =>
        Set<IntakeReviewCorrection>();
    public DbSet<IntakeReviewMatchDecision> IntakeReviewMatchDecisions =>
        Set<IntakeReviewMatchDecision>();
    public DbSet<IntakeReviewDuplicateDecision> IntakeReviewDuplicateDecisions =>
        Set<IntakeReviewDuplicateDecision>();
    public DbSet<IntakeReviewFindingDecision> IntakeReviewFindingDecisions =>
        Set<IntakeReviewFindingDecision>();
    public DbSet<IntakeReviewActivity> IntakeReviewActivities =>
        Set<IntakeReviewActivity>();
    public DbSet<ApprovedSnapshotSchemaDefinition> ApprovedSnapshotSchemaDefinitions =>
        Set<ApprovedSnapshotSchemaDefinition>();
    public DbSet<ApprovedIntakeSnapshot> ApprovedIntakeSnapshots =>
        Set<ApprovedIntakeSnapshot>();
    public DbSet<IntakeAdapterExecution> IntakeAdapterExecutions =>
        Set<IntakeAdapterExecution>();
    public DbSet<IntakeAdapterExecutionAttempt> IntakeAdapterExecutionAttempts =>
        Set<IntakeAdapterExecutionAttempt>();
    public DbSet<IntakeAdapterExternalReference> IntakeAdapterExternalReferences =>
        Set<IntakeAdapterExternalReference>();
    public DbSet<DocumentAssociationExecution> DocumentAssociationExecutions =>
        Set<DocumentAssociationExecution>();
    public DbSet<DocumentAssociationItem> DocumentAssociationItems =>
        Set<DocumentAssociationItem>();
    public DbSet<IntakeRecoveryWorkItem> IntakeRecoveryWorkItems =>
        Set<IntakeRecoveryWorkItem>();
    public DbSet<IntakeRecoveryAttempt> IntakeRecoveryAttempts =>
        Set<IntakeRecoveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntakeDbContext).Assembly);
    }
}