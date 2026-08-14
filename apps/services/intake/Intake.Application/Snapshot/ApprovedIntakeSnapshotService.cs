using System.Security.Cryptography;
using System.Text;
using Intake.Application.Artifacts;
using Intake.Application.Configuration;
using Intake.Application.Emails;
using Intake.Application.Manual;
using Intake.Domain.Artifacts;
using Intake.Contracts.Snapshot;
using Intake.Domain.Normalization;
using Intake.Domain.Review;
using Intake.Domain.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Snapshot;

public sealed class ApprovedIntakeSnapshotService(
    IApprovedSnapshotRepository snapshotRepository,
    IReviewedIntakeProjectionService projectionService,
    IIntakeArtifactRepository artifactRepository,
    IInboundEmailRepository emailRepository,
    IManualIntakeRepository manualRepository,
    IIntakeConfigurationRepository configurationRepository,
    IProcessingProfileRegistry profileRegistry,
    ICanonicalSnapshotSerializer serializer,
    ISnapshotAuditSink auditSink,
    ILogger<ApprovedIntakeSnapshotService> logger) : IApprovedIntakeSnapshotService
{
    public async Task<ApprovedSnapshotResponse> CreateAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndUser(tenantId, actorUserId);
        var source = await LoadProjectionAsync(tenantId, reviewId, cancellationToken);
        var workspace = source.Workspace;
        var review = workspace.Review;

        ValidateEligibility(source);
        var schema = await snapshotRepository.FindSchemaAsync(
            ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1,
            1,
            cancellationToken);
        if (schema is null || !schema.IsActive)
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.SchemaUnavailable,
                "The approved snapshot schema is not active.");

        var artifact = await artifactRepository.FindAsync(
            tenantId,
            review.ArtifactId,
            cancellationToken) ?? throw SnapshotException(
                ApprovedSnapshotFailureCodes.PayloadInvalid,
                "The reviewed artifact is not available.");
        var processingProfileCode = await ResolveProcessingProfileAsync(
            tenantId,
            artifact,
            cancellationToken);
        _ = profileRegistry.GetRequired(processingProfileCode);

        var executionKey = BuildExecutionKey(
            tenantId,
            review.Id,
            schema.Code,
            schema.Version);
        var existing = await snapshotRepository.FindByExecutionKeyAsync(
            tenantId,
            executionKey,
            cancellationToken);
        if (existing is not null)
            return MapResponse(existing);

        var current = await snapshotRepository.FindCurrentAsync(
            tenantId,
            review.ArtifactId,
            cancellationToken);
        var snapshotVersion = (current?.SnapshotVersion ?? 0) + 1;
        var approvedBy = review.CompletedByUserId ?? actorUserId;
        var approvedAt = review.CompletedAt ?? DateTimeOffset.UtcNow;
        var payload = BuildPayload(
            source,
            artifact,
            processingProfileCode,
            schema,
            snapshotVersion,
            approvedBy,
            approvedAt);
        var canonicalJson = serializer.Serialize(payload);
        var snapshotHash = serializer.Hash(canonicalJson);
        var now = DateTimeOffset.UtcNow;
        var snapshot = new ApprovedIntakeSnapshot
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ArtifactId = review.ArtifactId,
            ReviewId = review.Id,
            PolicyEvaluationId = review.ArtifactPolicyEvaluationId,
            ClassificationId = review.ClassificationId,
            ArtifactExtractionId = review.ArtifactExtractionId,
            ArtifactNormalizationId = review.ArtifactNormalizationId,
            ArtifactMatchRunId = review.ArtifactMatchRunId,
            ProcessingProfileCode = processingProfileCode,
            SchemaCode = schema.Code,
            SchemaVersion = schema.Version,
            SnapshotVersion = snapshotVersion,
            Status = ApprovedSnapshotStatuses.Ready,
            PayloadJson = canonicalJson,
            SnapshotHash = snapshotHash,
            ExecutionKey = executionKey,
            IsCurrent = true,
            ActiveCurrentKey = $"{tenantId:N}:{review.ArtifactId:N}",
            ApprovedByUserId = approvedBy,
            ApprovedAt = approvedAt,
            SupersedesSnapshotId = current?.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            var persisted = await snapshotRepository.PersistReadyAsync(
                snapshot,
                cancellationToken);
            await auditSink.RecordAsync(
                new SnapshotAuditEntry(
                    "created",
                    tenantId,
                    persisted.Id,
                    persisted.ArtifactId,
                    persisted.ReviewId,
                    actorUserId,
                    persisted.Status,
                    null,
                    null,
                    correlationId),
                CancellationToken.None);
            logger.LogInformation(
                "Approved Intake snapshot created. TenantId={TenantId} SnapshotId={SnapshotId} ReviewId={ReviewId} SchemaCode={SchemaCode} SchemaVersion={SchemaVersion} Status={Status}",
                tenantId,
                persisted.Id,
                persisted.ReviewId,
                persisted.SchemaCode,
                persisted.SchemaVersion,
                persisted.Status);
            return MapResponse(persisted);
        }
        catch (SnapshotVersionConflictException)
        {
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.ConcurrencyConflict,
                "A newer approved snapshot was created concurrently. Reload the current snapshot and retry.");
        }
        catch (InvalidOperationException)
        {
            var concurrent = await snapshotRepository.FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (concurrent is not null)
                return MapResponse(concurrent);
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.CreationFailed,
                "The approved snapshot could not be persisted.");
        }
    }

    public async Task<ApprovedSnapshotResponse?> GetAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var snapshot = await snapshotRepository.FindAsync(
            tenantId,
            snapshotId,
            cancellationToken);
        return snapshot is null ? null : MapResponse(snapshot);
    }

    public async Task<ApprovedSnapshotSummaryResponse> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var snapshot = await snapshotRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken) ?? throw SnapshotException(
                ApprovedSnapshotFailureCodes.NotFound,
                "No current approved snapshot exists for this artifact.");
        return MapSummary(snapshot);
    }

    public async Task<(IReadOnlyList<ApprovedSnapshotSummaryResponse> Items, long TotalCount)> ListByArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var result = await snapshotRepository.ListByArtifactAsync(
            tenantId,
            artifactId,
            Math.Clamp(page, 1, 10_000),
            Math.Clamp(pageSize, 1, 100),
            cancellationToken);
        return (result.Items.Select(MapSummary).ToArray(), result.TotalCount);
    }

    private async Task<ReviewedIntakeSnapshotSource> LoadProjectionAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await projectionService.GetAsync(
                tenantId,
                reviewId,
                cancellationToken);
        }
        catch (IntakeConfigurationException exception)
            when (exception.Code is IntakeReviewErrorCodes.Stale)
        {
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.ReviewStale,
                "The B12 review is stale and cannot produce an approved snapshot.");
        }
    }

    private static void ValidateEligibility(ReviewedIntakeSnapshotSource source)
    {
        var review = source.Workspace.Review;
        if (!string.Equals(review.Status, IntakeReviewStatuses.Completed, StringComparison.Ordinal))
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.ReviewRequired,
                "Only completed B12 reviews can produce approved snapshots.");
        if (review.IsStale)
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.ReviewStale,
                "The B12 review is stale and cannot produce an approved snapshot.");
        if (source.Projection.ReviewOutcome is not
            (IntakeReviewOutcomes.Approved or IntakeReviewOutcomes.ApprovedWithCorrections))
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.ReviewNotApproved,
                "The B12 review outcome is not eligible for an approved snapshot.");
        if (source.Projection.ReviewedClassification?.RequiresReprocessing == true)
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.ReprocessingRequired,
                "The reviewed classification requires reprocessing.");
        if (source.Workspace.Facts.Any(fact =>
                !fact.IsRejected &&
                fact.ValidationStatus.Contains("INVALID", StringComparison.OrdinalIgnoreCase)))
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.PayloadInvalid,
                "The reviewed projection contains an invalid active fact.");
        if (source.Workspace.Findings.Any(finding => finding.CurrentDecision is null))
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.PayloadInvalid,
                "The reviewed projection contains an unresolved policy finding.");

        var decisionsBySignal = source.Workspace.DuplicateDecisions
            .GroupBy(decision => decision.ArtifactDuplicateSignalId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.CreatedAt).First());
        if (source.Workspace.Duplicates.Any(signal =>
                !decisionsBySignal.TryGetValue(signal.Id, out var decision) ||
                decision.Decision == IntakeReviewDuplicateDecisions.NeedsFurtherReview))
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.PayloadInvalid,
                "Every duplicate signal must have a final B12 decision.");
    }

    private static ApprovedIntakeSnapshotV1 BuildPayload(
        ReviewedIntakeSnapshotSource source,
        IntakeArtifact artifact,
        string processingProfileCode,
        ApprovedSnapshotSchemaDefinition schema,
        int snapshotVersion,
        Guid approvedBy,
        DateTimeOffset approvedAt)
    {
        var workspace = source.Workspace;
        var projection = source.Projection;
        var classification = new ApprovedSnapshotClassification(
            workspace.Classification?.ClassificationCode,
            projection.ReviewedClassification?.ClassificationCode,
            projection.ReviewedClassification?.WasOverridden ?? false);

        var facts = projection.ReviewedFacts
            .Where(fact => !fact.IsRejected)
            .OrderBy(fact => fact.FactCode, StringComparer.Ordinal)
            .ThenBy(fact => fact.OriginalNormalizedFactId)
            .Select((fact, ordinal) => new ApprovedSnapshotFact(
                fact.FactCode,
                fact.DataType,
                fact.EffectiveValue ?? fact.NormalizedValue ?? fact.RawValue,
                fact.NormalizedJson,
                fact.ValidationStatus,
                ResolveFactSource(fact),
                fact.IsHumanCorrected,
                fact.IsHumanAdded,
                fact.OriginalExtractedFactId,
                fact.OriginalNormalizedFactId,
                fact.CorrectionId,
                fact.IsHumanCorrected || fact.IsHumanAdded ? null : fact.SourceConfidence,
                fact.EvidenceReferences.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ordinal))
            .ToArray();

        var entities = projection.ReviewedEntityDecisions
            .OrderBy(decision => decision.EntityType, StringComparer.Ordinal)
            .ThenByDescending(decision => decision.CreatedAt)
            .GroupBy(decision => decision.EntityType, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(decision => new ApprovedSnapshotEntityDecision(
                decision.EntityType,
                decision.Decision,
                decision.CandidateEntityId,
                decision.ArtifactEntityMatchId,
                decision.IsManualSelection,
                decision.ReasonCode))
            .ToArray();

        var duplicateDecisions = projection.DuplicateDecisions
            .OrderBy(decision => decision.ArtifactDuplicateSignalId)
            .ThenByDescending(decision => decision.CreatedAt)
            .GroupBy(decision => decision.ArtifactDuplicateSignalId)
            .Select(group => group.First())
            .Select(decision => new ApprovedSnapshotDuplicateDecision(
                decision.ArtifactDuplicateSignalId,
                decision.Decision,
                decision.ReasonCode))
            .ToArray();

        var documents = new List<ApprovedSnapshotDocument>
        {
            new(
                artifact.DocumentsServiceDocumentId,
                artifact.Id,
                string.IsNullOrWhiteSpace(artifact.ArtifactRole)
                    ? "PRIMARY_SOURCE"
                    : artifact.ArtifactRole,
                string.IsNullOrWhiteSpace(artifact.EffectiveFileName)
                    ? artifact.OriginalFileName
                    : artifact.EffectiveFileName,
                artifact.DetectedContentType ?? artifact.DeclaredContentType,
                artifact.Sha256,
                artifact.DocumentsServiceReference),
        };

        return new(
            schema.Code,
            schema.Version,
            snapshotVersion,
            processingProfileCode,
            classification,
            facts,
            entities,
            documents,
            duplicateDecisions,
            new(
                projection.ReviewId,
                projection.ReviewOutcome,
                approvedBy,
                approvedAt),
            new(
                projection.ArtifactId,
                workspace.Review.ClassificationId,
                workspace.Review.ArtifactExtractionId,
                workspace.Review.ArtifactNormalizationId,
                workspace.Review.ArtifactMatchRunId,
                projection.PolicyEvaluationId,
                projection.ReviewId));
    }

    private async Task<string> ResolveProcessingProfileAsync(
        Guid tenantId,
        IntakeArtifact artifact,
        CancellationToken cancellationToken)
    {
        if (artifact.InboundEmailId.HasValue)
        {
            var email = await emailRepository.FindTenantEmailAsync(
                tenantId,
                artifact.InboundEmailId.Value,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(email?.ProcessingProfileCode))
                return email.ProcessingProfileCode;
        }
        if (artifact.ManualIntakeSubmissionId.HasValue)
        {
            var submission = await manualRepository.FindAsync(
                tenantId,
                artifact.ManualIntakeSubmissionId.Value,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(submission?.ProcessingProfileCode))
                return submission.ProcessingProfileCode;
        }
        var configuration = await configurationRepository.FindTenantConfigurationAsync(
            tenantId,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(configuration?.DefaultProcessingProfileCode))
            return configuration.DefaultProcessingProfileCode;
        throw SnapshotException(
            ApprovedSnapshotFailureCodes.PayloadInvalid,
            "The approved snapshot does not have a processing profile.");
    }

    private static string ResolveFactSource(Intake.Contracts.Review.IntakeReviewFactResponse fact)
    {
        if (fact.IsHumanAdded)
            return "HUMAN_ADDITION";
        if (fact.IsHumanCorrected)
            return "HUMAN_CORRECTION";
        return string.IsNullOrWhiteSpace(fact.SourceType) ? "NORMALIZED_AI" : fact.SourceType;
    }

    private static string BuildExecutionKey(
        Guid tenantId,
        Guid reviewId,
        string schemaCode,
        int schemaVersion) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{tenantId:N}|{reviewId:N}|{schemaCode}|{schemaVersion}")));

    private static ApprovedSnapshotResponse MapResponse(ApprovedIntakeSnapshot snapshot)
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<ApprovedIntakeSnapshotV1>(
            snapshot.PayloadJson) ?? throw SnapshotException(
            ApprovedSnapshotFailureCodes.PayloadInvalid,
            "The persisted approved snapshot payload is invalid.");
        return new(
            snapshot.Id,
            snapshot.ArtifactId,
            snapshot.ReviewId,
            snapshot.SnapshotVersion,
            snapshot.SchemaCode,
            snapshot.SchemaVersion,
            snapshot.ProcessingProfileCode,
            snapshot.Status,
            snapshot.SnapshotHash,
            snapshot.IsCurrent,
            snapshot.ApprovedByUserId,
            snapshot.ApprovedAt,
            snapshot.CreatedAt,
            payload);
    }

    private static ApprovedSnapshotSummaryResponse MapSummary(ApprovedIntakeSnapshot snapshot) =>
        new(
            snapshot.Id,
            snapshot.ArtifactId,
            snapshot.ReviewId,
            snapshot.SnapshotVersion,
            snapshot.SchemaCode,
            snapshot.SchemaVersion,
            snapshot.ProcessingProfileCode,
            snapshot.Status,
            snapshot.SnapshotHash,
            snapshot.IsCurrent,
            snapshot.ApprovedByUserId,
            snapshot.ApprovedAt,
            snapshot.CreatedAt);

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.TenantContextInvalid,
                "A tenant context is required.");
    }

    private static void ValidateTenantAndUser(Guid tenantId, Guid userId)
    {
        ValidateTenant(tenantId);
        if (userId == Guid.Empty)
            throw SnapshotException(
                ApprovedSnapshotFailureCodes.TenantContextInvalid,
                "An authenticated user is required.");
    }

    private static IntakeConfigurationException SnapshotException(
        string code,
        string message) =>
        IntakeConfigurationException.Conflict(code, message);
}