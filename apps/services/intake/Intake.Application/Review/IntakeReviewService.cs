using System.Globalization;
using System.Text.Json;
using Intake.Application.Artifacts;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Emails;
using Intake.Application.Extraction;
using Intake.Application.Matching;
using Intake.Application.Normalization;
using Intake.Application.Policy;
using Intake.Application.Manual;
using Intake.Contracts.Review;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;
using Intake.Domain.Review;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Review;

public sealed class IntakeReviewService(
    IIntakeReviewRepository reviewRepository,
    IIntakeArtifactRepository artifactRepository,
    IInboundEmailRepository emailRepository,
    IManualIntakeRepository manualRepository,
    IClassificationRepository classificationRepository,
    IArtifactExtractionRepository extractionRepository,
    IArtifactNormalizationRepository normalizationRepository,
    IArtifactMatchingRepository matchingRepository,
    IArtifactPolicyRepository policyRepository,
    IFactNormalizerRegistry normalizerRegistry,
    IReviewAuditSink auditSink,
    ILogger<IntakeReviewService> logger) : IIntakeReviewService
{
    public async Task<IntakeReviewListResponse> ListAsync(
        Guid tenantId,
        IntakeReviewListQuery query,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var (items, totalCount) = await reviewRepository.ListAsync(
            tenantId,
            query,
            cancellationToken);
        var responses = new List<IntakeReviewSummaryResponse>(items.Count);
        foreach (var item in items)
            responses.Add(await MapSummaryAsync(tenantId, item, cancellationToken));
        return new(
            responses,
            Math.Clamp(query.Page, 1, 10_000),
            Math.Clamp(query.PageSize, 1, 100),
            totalCount);
    }

    public Task<IntakeReviewQueueSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        return reviewRepository.GetSummaryAsync(tenantId, cancellationToken);
    }

    public async Task<IntakeReviewWorkspaceResponse?> GetAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var review = await reviewRepository.FindAsync(
            tenantId,
            reviewId,
            false,
            cancellationToken);
        return review is null
            ? null
            : await MapWorkspaceAsync(tenantId, review, cancellationToken);
    }

    public async Task<IntakeReviewWorkspaceResponse> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        string? correlationId,
        CreateIntakeReviewRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        RequireUser(actorUserId);
        var context = await LoadContextAsync(
            tenantId,
            request.ArtifactId,
            request.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureReviewEligible(context);

        var existing = await reviewRepository.FindActiveByContextAsync(
            tenantId,
            context.Artifact.Id,
            context.Policy.Id,
            cancellationToken);
        if (existing is not null)
            return await MapWorkspaceAsync(tenantId, existing, cancellationToken);

        await reviewRepository.MarkOpenReviewsSupersededAsync(
            tenantId,
            context.Artifact.Id,
            context.Policy.Id,
            actorUserId,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var review = new IntakeReview
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ArtifactId = context.Artifact.Id,
            ClassificationId = context.Classification?.Id,
            ArtifactExtractionId = context.Extraction?.Id,
            ArtifactNormalizationId = context.Normalization?.Id,
            ArtifactMatchRunId = context.Matching?.Id,
            ArtifactPolicyEvaluationId = context.Policy.Id,
            Status = IntakeReviewStatuses.Pending,
            Priority = context.Policy.ReviewPriority,
            B11Disposition = context.Policy.Disposition,
            ClassificationCode = context.Classification?.ClassificationCode ?? string.Empty,
            SourceType = context.Artifact.ArtifactSourceType,
            ActiveContextKey = $"{context.Artifact.Id:N}:{context.Policy.Id:N}",
            CreatedAt = now,
            UpdatedAt = now,
            Activities =
            [
                Activity(
                    tenantId,
                    Guid.Empty,
                    IntakeReviewActivityTypes.Created,
                    actorUserId,
                    now,
                    $"{{\"policyEvaluationId\":\"{context.Policy.Id}\"}}"),
            ],
        };
        review.Activities.Single().IntakeReviewId = review.Id;
        await reviewRepository.AddAsync(review, cancellationToken);
        await AuditAsync(
            "created",
            review,
            actorUserId,
            null,
            null,
            null,
            correlationId,
            cancellationToken);
        var created = await reviewRepository.FindAsync(
            tenantId,
            review.Id,
            false,
            cancellationToken);
        return await MapWorkspaceAsync(tenantId, created!, cancellationToken);
    }

    public Task<IntakeReviewResponse> AssignAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        AssignIntakeReviewRequest request,
        string? correlationId,
        CancellationToken cancellationToken) =>
        MutateAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            IntakeReviewActivityTypes.Assigned,
            correlationId,
            review =>
            {
                var assignee = request.UserId ?? actorUserId;
                if (assignee == Guid.Empty)
                    throw IntakeConfigurationException.BadRequest(
                        IntakeReviewErrorCodes.UnauthorizedUser,
                        "A valid assignee is required.");
                review.AssignedToUserId = assignee;
                review.AssignedAt = DateTimeOffset.UtcNow;
                review.Status = IntakeReviewStatuses.Assigned;
            },
            cancellationToken);

    public Task<IntakeReviewResponse> ClaimAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        ReviewVersionRequest request,
        string? correlationId,
        CancellationToken cancellationToken) =>
        MutateAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            IntakeReviewActivityTypes.Claimed,
            correlationId,
            review =>
            {
                if (review.AssignedToUserId.HasValue &&
                    review.AssignedToUserId != actorUserId)
                    throw IntakeConfigurationException.Conflict(
                        IntakeReviewErrorCodes.AssignmentConflict,
                        "This review is assigned to another reviewer.");
                review.AssignedToUserId = actorUserId;
                review.AssignedAt ??= DateTimeOffset.UtcNow;
                review.StartedAt ??= DateTimeOffset.UtcNow;
                review.Status = IntakeReviewStatuses.InReview;
            },
            cancellationToken);

    public Task<IntakeReviewResponse> UnassignAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        ReviewVersionRequest request,
        string? correlationId,
        CancellationToken cancellationToken) =>
        MutateAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            IntakeReviewActivityTypes.Unassigned,
            correlationId,
            review =>
            {
                review.AssignedToUserId = null;
                review.AssignedAt = null;
                review.Status = IntakeReviewStatuses.Pending;
            },
            cancellationToken);

    public async Task<IntakeReviewResponse> AddCorrectionAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        AddReviewCorrectionRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var review = await LoadMutableReviewAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            cancellationToken);
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureOpenAndFresh(review, context);
        var correction = BuildCorrection(
            tenantId,
            review,
            actorUserId,
            request,
            context.Normalization);
        correction.SupersedesCorrectionId = LatestCorrection(
            review,
            request.FactCode,
            request.CorrectionType)?.Id;
        review.Corrections.Add(correction);
        review.Status = IntakeReviewStatuses.InReview;
        review.StartedAt ??= DateTimeOffset.UtcNow;
        review.Version++;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        review.Activities.Add(Activity(
            tenantId,
            review.Id,
            request.CorrectionType == IntakeReviewCorrectionTypes.FactAdded
                ? IntakeReviewActivityTypes.FactAdded
                : request.CorrectionType == IntakeReviewCorrectionTypes.FactRejected
                    ? IntakeReviewActivityTypes.FactRejected
                    : request.CorrectionType == IntakeReviewCorrectionTypes.ClassificationOverride
                        ? IntakeReviewActivityTypes.ClassificationOverridden
                        : IntakeReviewActivityTypes.FactCorrected,
            actorUserId,
            review.UpdatedAt,
            $"{{\"factCode\":\"{JsonEncoded(request.FactCode)}\"}}"));
        await SaveMutationAsync(review, cancellationToken);
        await AuditAsync(
            "correction",
            review,
            actorUserId,
            request.CorrectionType,
            null,
            request.FactCode,
            correlationId,
            cancellationToken);
        return MapResponse(review, await IsStaleAsync(tenantId, review, cancellationToken));
    }

    public async Task<IntakeReviewResponse> DecideMatchAsync(
        Guid tenantId,
        Guid reviewId,
        string entityType,
        Guid actorUserId,
        ReviewMatchDecisionRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var review = await LoadMutableReviewAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            cancellationToken);
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureOpenAndFresh(review, context);
        var matches = context.Matching?.EntityMatches
            .Where(item => item.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? [];
        if (matches.Length == 0)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.MatchDecisionInvalid,
                "No current tenant-scoped candidate matches exist for this entity type.");
        var selected = request.ArtifactEntityMatchId.HasValue
            ? matches.SingleOrDefault(item => item.Id == request.ArtifactEntityMatchId)
            : null;
        if (request.Decision is not (
            IntakeReviewMatchDecisions.Confirmed or
            IntakeReviewMatchDecisions.Rejected or
            IntakeReviewMatchDecisions.NoMatch or
            IntakeReviewMatchDecisions.ManualSelection))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.MatchDecisionInvalid,
                "The match decision code is not supported.");
        if (request.Decision is IntakeReviewMatchDecisions.Confirmed or
            IntakeReviewMatchDecisions.ManualSelection)
        {
            if (selected is null || request.CandidateEntityId != selected.CandidateEntityId)
                throw IntakeConfigurationException.BadRequest(
                    IntakeReviewErrorCodes.MatchDecisionInvalid,
                    "A decision must reference a current candidate from the tenant-scoped match run.");
        }
        var decision = new IntakeReviewMatchDecision
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeReviewId = review.Id,
            EntityType = entityType,
            ArtifactEntityMatchId = request.ArtifactEntityMatchId,
            CandidateEntityId = request.CandidateEntityId,
            Decision = request.Decision,
            IsManualSelection = request.Decision == IntakeReviewMatchDecisions.ManualSelection,
            ReasonCode = request.ReasonCode,
            Comment = request.Comment,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            SupersedesDecisionId = review.MatchDecisions
                .Where(item => item.EntityType == entityType)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefault(),
        };
        review.MatchDecisions.Add(decision);
        Bump(review, actorUserId, IntakeReviewActivityTypes.MatchDecided);
        await SaveMutationAsync(review, cancellationToken);
        await AuditAsync(
            "match-decision",
            review,
            actorUserId,
            request.Decision,
            null,
            null,
            correlationId,
            cancellationToken,
            entityType);
        return MapResponse(review, await IsStaleAsync(tenantId, review, cancellationToken));
    }

    public async Task<IntakeReviewResponse> DecideDuplicateAsync(
        Guid tenantId,
        Guid reviewId,
        Guid signalId,
        Guid actorUserId,
        ReviewDuplicateDecisionRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var review = await LoadMutableReviewAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            cancellationToken);
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureOpenAndFresh(review, context);
        var signal = context.Matching?.DuplicateSignals
            .SingleOrDefault(item => item.Id == signalId);
        if (signal is null)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.DuplicateDecisionRequired,
                "The duplicate signal is not part of the current tenant-scoped match run.");
        if (request.Decision is not (
            IntakeReviewDuplicateDecisions.Confirmed or
            IntakeReviewDuplicateDecisions.NotDuplicate or
            IntakeReviewDuplicateDecisions.NeedsFurtherReview))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.DuplicateDecisionRequired,
                "The duplicate decision code is not supported.");
        var decision = new IntakeReviewDuplicateDecision
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeReviewId = review.Id,
            ArtifactDuplicateSignalId = signal.Id,
            Decision = request.Decision,
            RelatedArtifactId = signal.RelatedArtifactId,
            ReasonCode = request.ReasonCode,
            Comment = request.Comment,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            SupersedesDecisionId = review.DuplicateDecisions
                .Where(item => item.ArtifactDuplicateSignalId == signal.Id)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefault(),
        };
        review.DuplicateDecisions.Add(decision);
        Bump(review, actorUserId, IntakeReviewActivityTypes.DuplicateDecided);
        await SaveMutationAsync(review, cancellationToken);
        await AuditAsync(
            "duplicate-decision",
            review,
            actorUserId,
            request.Decision,
            null,
            null,
            correlationId,
            cancellationToken,
            null);
        return MapResponse(review, await IsStaleAsync(tenantId, review, cancellationToken));
    }

    public async Task<IntakeReviewResponse> DecideFindingAsync(
        Guid tenantId,
        Guid reviewId,
        Guid findingId,
        Guid actorUserId,
        ReviewFindingDecisionRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var review = await LoadMutableReviewAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            cancellationToken);
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureOpenAndFresh(review, context);
        var finding = context.Policy.Findings.SingleOrDefault(item => item.Id == findingId);
        if (finding is null)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.FindingUnresolved,
                "The finding is not part of the bound policy evaluation.");
        if (finding.Severity == PolicyFindingSeverities.Blocking)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.FindingUnresolved,
                "Blocking policy findings cannot be overridden by human review.");
        if (request.Decision is not (
            IntakeReviewFindingDecisions.Resolved or
            IntakeReviewFindingDecisions.Acknowledged or
            IntakeReviewFindingDecisions.NotApplicable))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.FindingUnresolved,
                "The finding decision code is not supported.");
        var decision = new IntakeReviewFindingDecision
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeReviewId = review.Id,
            ArtifactPolicyFindingId = finding.Id,
            Decision = request.Decision,
            ReasonCode = request.ReasonCode,
            Comment = request.Comment,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            SupersedesDecisionId = review.FindingDecisions
                .Where(item => item.ArtifactPolicyFindingId == finding.Id)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefault(),
        };
        review.FindingDecisions.Add(decision);
        Bump(review, actorUserId, IntakeReviewActivityTypes.FindingDecided);
        await SaveMutationAsync(review, cancellationToken);
        await AuditAsync(
            "finding-decision",
            review,
            actorUserId,
            request.Decision,
            null,
            null,
            correlationId,
            cancellationToken,
            null);
        return MapResponse(review, await IsStaleAsync(tenantId, review, cancellationToken));
    }

    public async Task<IntakeReviewResponse> CompleteAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        CompleteIntakeReviewRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var review = await LoadMutableReviewAsync(
            tenantId,
            reviewId,
            actorUserId,
            request.Version,
            cancellationToken);
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureOpenAndFresh(review, context);
        ValidateCompletion(review, context, request);
        var now = DateTimeOffset.UtcNow;
        review.Status = IntakeReviewStatuses.Completed;
        review.ActiveContextKey = null;
        review.ReviewOutcome = request.Outcome;
        review.CompletionReasonCode = request.ReasonCode;
        review.CompletionComment = request.Comment;
        review.CompletedAt = now;
        review.CompletedByUserId = actorUserId;
        Bump(review, actorUserId, IntakeReviewActivityTypes.Completed);
        await SaveMutationAsync(review, cancellationToken);
        await AuditAsync(
            "completed",
            review,
            actorUserId,
            request.Outcome,
            request.Outcome,
            null,
            correlationId,
            cancellationToken);
        return MapResponse(review, false);
    }

    public async Task<ReviewedIntakeProjectionResponse> GetEffectiveAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var review = await reviewRepository.FindAsync(
            tenantId,
            reviewId,
            false,
            cancellationToken);
        if (review is null)
            throw IntakeConfigurationException.NotFound(
                IntakeReviewErrorCodes.NotFound,
                "The Intake review was not found.");
        var workspace = await MapWorkspaceAsync(tenantId, review, cancellationToken);
        if (workspace.Review.IsStale)
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.Stale,
                "The bound upstream intelligence has changed; start a new review.");
        var latestCorrections = review.Corrections
            .GroupBy(item => $"{item.FactCode}:{item.TargetId}:{item.CorrectionType}")
            .Select(group => group.OrderByDescending(item => item.CreatedAt).First())
            .ToArray();
        var facts = workspace.Facts.ToList();
        foreach (var correction in latestCorrections)
        {
            if (correction.CorrectionType == IntakeReviewCorrectionTypes.FactAdded)
            {
                facts.Add(new IntakeReviewFactResponse(
                    correction.FactCode,
                    "TEXT",
                    correction.CorrectedValue,
                    correction.NormalizedValue,
                    correction.CorrectedJson,
                    1,
                    NormalizationStatuses.Normalized,
                    correction.ValidationStatus ?? ValidationStatuses.Valid,
                    [],
                    [],
                    correction.NormalizedValue ?? correction.CorrectedValue,
                    null,
                    null,
                    correction.Id,
                    "HUMAN",
                    true,
                    true,
                    false));
            }
        }
        return new(
            review.Id,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            workspace.Classification,
            facts,
            workspace.MatchDecisions,
            workspace.DuplicateDecisions,
            workspace.FindingDecisions,
            review.ReviewOutcome);
    }

    private async Task<IntakeReviewResponse> MutateAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        int expectedVersion,
        string activityType,
        string? correlationId,
        Action<IntakeReview> mutate,
        CancellationToken cancellationToken)
    {
        var review = await LoadMutableReviewAsync(
            tenantId,
            reviewId,
            actorUserId,
            expectedVersion,
            cancellationToken);
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken);
        EnsureOpenAndFresh(review, context);
        mutate(review);
        Bump(review, actorUserId, activityType);
        await SaveMutationAsync(review, cancellationToken);
        await AuditAsync(
            activityType.ToLowerInvariant(),
            review,
            actorUserId,
            null,
            null,
            null,
            correlationId,
            cancellationToken);
        return MapResponse(review, await IsStaleAsync(tenantId, review, cancellationToken));
    }

    private async Task<IntakeReview> LoadMutableReviewAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        RequireUser(actorUserId);
        var review = await reviewRepository.FindAsync(
            tenantId,
            reviewId,
            true,
            cancellationToken);
        if (review is null)
            throw IntakeConfigurationException.NotFound(
                IntakeReviewErrorCodes.NotFound,
                "The Intake review was not found.");
        if (review.Version != expectedVersion)
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.ConcurrencyConflict,
                "The review changed since it was loaded. Refresh the workspace.");
        if (review.Status is IntakeReviewStatuses.Completed
            or IntakeReviewStatuses.Cancelled
            or IntakeReviewStatuses.Superseded)
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.AlreadyCompleted,
                "Completed or superseded reviews are immutable.");
        return review;
    }

    private async Task<ReviewContext> LoadContextAsync(
        Guid tenantId,
        Guid artifactId,
        Guid? requestedPolicyId,
        CancellationToken cancellationToken,
        bool rejectRequestedLineageMismatch = true)
    {
        var artifact = await artifactRepository.FindAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (artifact is null)
            throw IntakeConfigurationException.NotFound(
                IntakeReviewErrorCodes.NotFound,
                "The Intake artifact was not found.");
        var policy = await policyRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (policy is null ||
            policy.Status != PolicyEvaluationStatuses.Completed)
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.NotEligible,
                "The artifact does not have a completed current policy evaluation.");
        if (rejectRequestedLineageMismatch &&
            requestedPolicyId.HasValue &&
            requestedPolicyId != policy.Id)
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.Stale,
                "The requested policy evaluation is no longer current.");

        var classification = await classificationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        var extraction = await extractionRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            policy.ClassificationId ?? classification?.Id ?? Guid.Empty,
            cancellationToken);
        var normalization = await normalizationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            policy.ArtifactExtractionId ?? extraction?.Id ?? Guid.Empty,
            cancellationToken);
        var matching = await matchingRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            policy.ArtifactNormalizationId ?? normalization?.Id ?? Guid.Empty,
            cancellationToken);
        return new(
            artifact,
            classification,
            extraction,
            normalization,
            matching,
            policy);
    }

    private static void EnsureReviewEligible(ReviewContext context)
    {
        if (context.Policy.Disposition is PolicyDispositionCodes.AutoAcceptable)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.NotEligible,
                "Auto-acceptable artifacts do not require human review.");
        if (context.Policy.Disposition is PolicyDispositionCodes.InsufficientData)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.NotEligible,
                "The artifact must be reprocessed before human review.");
    }

    private static void EnsureOpenAndFresh(
        IntakeReview review,
        ReviewContext context)
    {
        if (review.Status is IntakeReviewStatuses.Completed
            or IntakeReviewStatuses.Cancelled
            or IntakeReviewStatuses.Superseded)
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.AlreadyCompleted,
                "Completed or superseded reviews are immutable.");
        if (!SameLineage(review, context))
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.Stale,
                "The bound upstream intelligence changed. Start a new review.");
    }

    private static bool SameLineage(IntakeReview review, ReviewContext context) =>
        review.ArtifactPolicyEvaluationId == context.Policy.Id &&
        review.ClassificationId == context.Classification?.Id &&
        review.ArtifactExtractionId == context.Extraction?.Id &&
        review.ArtifactNormalizationId == context.Normalization?.Id &&
        review.ArtifactMatchRunId == context.Matching?.Id;

    private static void ValidateCompletion(
        IntakeReview review,
        ReviewContext context,
        CompleteIntakeReviewRequest request)
    {
        var validOutcomes = new[]
        {
            IntakeReviewOutcomes.Approved,
            IntakeReviewOutcomes.ApprovedWithCorrections,
            IntakeReviewOutcomes.Rejected,
            IntakeReviewOutcomes.DuplicateConfirmed,
            IntakeReviewOutcomes.NoDuplicate,
            IntakeReviewOutcomes.NoMatchConfirmed,
            IntakeReviewOutcomes.ReturnForReprocessing,
        };
        if (!validOutcomes.Contains(request.Outcome))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.CompletionInvalid,
                "The review outcome code is not supported.");
        if (request.Outcome is IntakeReviewOutcomes.Rejected
            or IntakeReviewOutcomes.ReturnForReprocessing &&
            string.IsNullOrWhiteSpace(request.ReasonCode))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.CompletionInvalid,
                "A reason code is required for this outcome.");

        var latestDuplicates = review.DuplicateDecisions
            .GroupBy(item => item.ArtifactDuplicateSignalId)
            .Select(item => item.OrderByDescending(decision => decision.CreatedAt).First())
            .ToDictionary(item => item.ArtifactDuplicateSignalId);
        var duplicateSignals = context.Matching?.DuplicateSignals ?? [];
        if (context.Policy.Disposition == PolicyDispositionCodes.Duplicate &&
            duplicateSignals.Any(signal =>
                !latestDuplicates.TryGetValue(signal.Id, out var decision) ||
                decision.Decision == IntakeReviewDuplicateDecisions.NeedsFurtherReview))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.DuplicateDecisionRequired,
                "Every duplicate signal must have a final human decision.");

        var latestFindings = review.FindingDecisions
            .GroupBy(item => item.ArtifactPolicyFindingId)
            .Select(item => item.OrderByDescending(decision => decision.CreatedAt).First())
            .ToDictionary(item => item.ArtifactPolicyFindingId);
        var unresolved = context.Policy.Findings
            .Where(finding =>
                finding.Severity is PolicyFindingSeverities.Review or
                PolicyFindingSeverities.Warning)
            .Any(finding =>
                !latestFindings.TryGetValue(finding.Id, out var decision) ||
                decision.Decision == IntakeReviewFindingDecisions.Acknowledged);
        if (request.Outcome is IntakeReviewOutcomes.Approved
            or IntakeReviewOutcomes.ApprovedWithCorrections && unresolved)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.FindingUnresolved,
                "Review-level policy findings must be resolved before approval.");

        var requiresReprocessing = review.Corrections.Any(correction =>
            correction.CorrectionType == IntakeReviewCorrectionTypes.ClassificationOverride);
        if (requiresReprocessing &&
            request.Outcome is IntakeReviewOutcomes.Approved
                or IntakeReviewOutcomes.ApprovedWithCorrections)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.ReprocessingRequired,
                "A classification correction requires reprocessing before approval.");
    }

    private IntakeReviewCorrection BuildCorrection(
        Guid tenantId,
        IntakeReview review,
        Guid actorUserId,
        AddReviewCorrectionRequest request,
        ArtifactNormalization? normalization)
    {
        if (string.IsNullOrWhiteSpace(request.FactCode) &&
            request.CorrectionType != IntakeReviewCorrectionTypes.ClassificationOverride)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.CorrectionInvalid,
                "FactCode is required.");
        if (request.CorrectionType == IntakeReviewCorrectionTypes.FactRejected &&
            string.IsNullOrWhiteSpace(request.ReasonCode))
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.CorrectionInvalid,
                "A reason code is required when rejecting a fact.");
        ArtifactNormalizedFact? source = null;
        if (request.TargetId.HasValue && normalization is not null)
            source = normalization.Facts.SingleOrDefault(
                item => item.Id == request.TargetId.Value);
        if (request.CorrectionType is IntakeReviewCorrectionTypes.ValueCorrection
            or IntakeReviewCorrectionTypes.FactRejected && source is null &&
            request.CorrectionType != IntakeReviewCorrectionTypes.FactAdded)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.CorrectionInvalid,
                "The correction target is not part of the bound normalization.");

        string? normalizedValue = null;
        string? validationStatus = null;
        if (request.CorrectionType is IntakeReviewCorrectionTypes.ValueCorrection
            or IntakeReviewCorrectionTypes.FactAdded)
        {
            if (string.IsNullOrWhiteSpace(request.CorrectedValue))
                throw IntakeConfigurationException.BadRequest(
                    IntakeReviewErrorCodes.CorrectionInvalid,
                    "CorrectedValue is required.");
            if (!normalizerRegistry.TryResolve(
                    request.FactCode,
                    request.DataType,
                    out var normalizer))
                throw IntakeConfigurationException.BadRequest(
                    IntakeReviewErrorCodes.CorrectionInvalid,
                    "The fact type is not supported by the Intake normalizer registry.");
            var result = normalizer.Normalize(new FactNormalizationInput(
                request.FactCode,
                request.DataType,
                request.CorrectedValue,
                new FactNormalizationOptions(
                    "US",
                    "USD",
                    CultureInfo.InvariantCulture,
                    true,
                    "FormC",
                    "1")));
            normalizedValue = result.NormalizedValue ?? result.ComparisonKey;
            validationStatus = result.ValidationStatus;
            if (result.ValidationStatus == ValidationStatuses.InvalidFormat)
                throw IntakeConfigurationException.BadRequest(
                    IntakeReviewErrorCodes.CorrectionInvalid,
                    "The corrected value did not pass deterministic normalization.");
        }
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeReviewId = review.Id,
            TargetType = request.CorrectionType ==
                         IntakeReviewCorrectionTypes.ClassificationOverride
                ? "CLASSIFICATION"
                : "FACT",
            TargetId = request.TargetId,
            FactCode = request.FactCode,
            OriginalExtractedFactId = null,
            OriginalNormalizedFactId = source?.Id,
            CorrectionType = request.CorrectionType,
            CorrectedValue = request.CorrectedValue,
            CorrectedJson = request.CorrectedJson,
            NormalizedValue = normalizedValue,
            ValidationStatus = validationStatus,
            SourceType = "HUMAN",
            HumanVerified = request.HumanVerified,
            ReasonCode = request.ReasonCode,
            Comment = request.Comment,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task<IntakeReviewWorkspaceResponse> MapWorkspaceAsync(
        Guid tenantId,
        IntakeReview review,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(
            tenantId,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            cancellationToken,
            rejectRequestedLineageMismatch: false);
        var stale = !SameLineage(review, context);
        var source = await MapSourceAsync(tenantId, context.Artifact, cancellationToken);
        var classification = context.Classification is null
            ? null
            : new IntakeReviewClassificationResponse(
                context.Classification.ClassificationCode,
                context.Classification.ClassificationLabel,
                (decimal)(context.Classification.Confidence ?? 0),
                context.Classification.Reason,
                review.Corrections.Any(item =>
                    item.CorrectionType == IntakeReviewCorrectionTypes.ClassificationOverride),
                review.Corrections
                    .Where(item => item.CorrectionType ==
                                   IntakeReviewCorrectionTypes.ClassificationOverride)
                    .OrderByDescending(item => item.CreatedAt)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefault(),
                review.Corrections.Any(item =>
                    item.CorrectionType == IntakeReviewCorrectionTypes.ClassificationOverride));
        var corrections = review.Corrections
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new IntakeReviewCorrectionResponse(
                item.Id,
                item.FactCode,
                item.CorrectionType,
                item.CorrectedValue,
                item.NormalizedValue,
                item.ValidationStatus,
                item.ReasonCode,
                item.Comment,
                item.CreatedByUserId,
                item.CreatedAt,
                item.HumanVerified))
            .ToArray();
        var facts = (context.Normalization?.Facts ?? [])
            .OrderBy(item => item.Ordinal)
            .Select(item => MapFact(item, review.Corrections))
            .ToArray();
        var matches = (context.Matching?.EntityMatches ?? [])
            .Select(match => new IntakeReviewMatchResponse(
                match.Id,
                match.EntityType,
                match.CandidateEntityId,
                match.CandidateDisplayLabel,
                match.Score,
                match.Rank,
                match.MatchStatus,
                match.MatchedFieldCount,
                match.ConflictingFieldCount,
                (match.Fields ?? [])
                    .Select(field => new IntakeReviewMatchFieldResponse(
                        field.FactCode,
                        field.MatchOutcome,
                        field.ReasonCode))
                    .ToArray()))
            .ToArray();
        var duplicateSignals = (context.Matching?.DuplicateSignals ?? [])
            .Select(signal => new IntakeReviewDuplicateResponse(
                signal.Id,
                signal.DuplicateType,
                signal.RelatedArtifactId,
                signal.RelatedBusinessEntityId,
                signal.RelatedBusinessEntityType,
                signal.Score,
                signal.Status,
                signal.ReasonCode))
            .ToArray();
        var findingDecisions = review.FindingDecisions
            .GroupBy(item => item.ArtifactPolicyFindingId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.CreatedAt)
                    .First().Decision);
        var findings = context.Policy.Findings
            .Select(finding => new IntakeReviewFindingResponse(
                finding.Id,
                finding.RuleCode,
                finding.RuleCategory,
                finding.Severity,
                finding.Outcome,
                finding.ReasonCode,
                finding.EntityType,
                finding.FactCode,
                finding.Score,
                finding.Threshold,
                ReadJsonArray(finding.EvidenceReferenceJson),
                findingDecisions.GetValueOrDefault(finding.Id)))
            .ToArray();
        return new(
            MapResponse(review, stale),
            source,
            classification,
            facts,
            matches,
            duplicateSignals,
            findings,
            corrections,
            review.MatchDecisions
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new IntakeReviewMatchDecisionResponse(
                    item.Id,
                    item.EntityType,
                    item.ArtifactEntityMatchId,
                    item.CandidateEntityId,
                    item.Decision,
                    item.IsManualSelection,
                    item.ReasonCode,
                    item.CreatedByUserId,
                    item.CreatedAt))
                .ToArray(),
            review.DuplicateDecisions
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new IntakeReviewDuplicateDecisionResponse(
                    item.Id,
                    item.ArtifactDuplicateSignalId,
                    item.Decision,
                    item.ReasonCode,
                    item.CreatedByUserId,
                    item.CreatedAt))
                .ToArray(),
            review.FindingDecisions
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new IntakeReviewFindingDecisionResponse(
                    item.Id,
                    item.ArtifactPolicyFindingId,
                    item.Decision,
                    item.ReasonCode,
                    item.CreatedByUserId,
                    item.CreatedAt))
                .ToArray(),
            review.Activities
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new IntakeReviewActivityResponse(
                    item.Id,
                    item.ActivityType,
                    item.ActorUserId,
                    item.CreatedAt))
                .ToArray());
    }

    private async Task<IntakeReviewSourceResponse> MapSourceAsync(
        Guid tenantId,
        IntakeArtifact artifact,
        CancellationToken cancellationToken)
    {
        var artifacts = artifact.InboundEmailId.HasValue
            ? await artifactRepository.ListByEmailAsync(
                tenantId,
                artifact.InboundEmailId.Value,
                cancellationToken)
            : artifact.ManualIntakeSubmissionId.HasValue
                ? await artifactRepository.ListByManualSubmissionAsync(
                    tenantId,
                    artifact.ManualIntakeSubmissionId.Value,
                    cancellationToken)
                : [artifact];
        var documents = artifacts
            .Select(item => new IntakeReviewDocumentResponse(
                item.DocumentsServiceDocumentId,
                item.Id,
                item.EffectiveFileName,
                item.DetectedContentType ?? item.DeclaredContentType,
                item.SizeBytes,
                item.DocumentsServiceReference))
            .ToArray();
        if (artifact.InboundEmailId.HasValue)
        {
            var email = await emailRepository.FindTenantEmailAsync(
                tenantId,
                artifact.InboundEmailId.Value,
                cancellationToken);
            return new(
                artifact.ArtifactSourceType,
                email?.ReceivedAt,
                email?.Subject,
                email?.FromAddress,
                null,
                null,
                documents);
        }
        if (artifact.ManualIntakeSubmissionId.HasValue)
        {
            var submission = await manualRepository.FindAsync(
                tenantId,
                artifact.ManualIntakeSubmissionId.Value,
                cancellationToken);
            return new(
                artifact.ArtifactSourceType,
                submission?.CreatedAt,
                null,
                null,
                submission?.Title,
                submission?.ClientRequestId,
                documents);
        }
        return new(artifact.ArtifactSourceType, null, null, null, null, null, documents);
    }

    private async Task<IntakeReviewSummaryResponse> MapSummaryAsync(
        Guid tenantId,
        IntakeReview review,
        CancellationToken cancellationToken) =>
        new(
            review.Id,
            review.ArtifactId,
            review.ArtifactPolicyEvaluationId,
            review.Status,
            review.Priority,
            review.B11Disposition,
            review.ClassificationCode,
            review.SourceType,
            review.CreatedAt,
            review.UpdatedAt,
            review.AssignedToUserId,
            review.Version,
            await IsStaleAsync(tenantId, review, cancellationToken));

    private async Task<bool> IsStaleAsync(
        Guid tenantId,
        IntakeReview review,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = await LoadContextAsync(
                tenantId,
                review.ArtifactId,
                review.ArtifactPolicyEvaluationId,
                cancellationToken);
            return !SameLineage(review, context);
        }
        catch (IntakeConfigurationException)
        {
            return true;
        }
    }

    private static IntakeReviewFactResponse MapFact(
        ArtifactNormalizedFact fact,
        IEnumerable<IntakeReviewCorrection> corrections)
    {
        var correction = corrections
            .Where(item =>
                item.OriginalNormalizedFactId == fact.Id ||
                (item.FactCode == fact.FactCode &&
                 item.CorrectionType is IntakeReviewCorrectionTypes.ValueCorrection
                     or IntakeReviewCorrectionTypes.FactRejected))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var rejected = correction?.CorrectionType == IntakeReviewCorrectionTypes.FactRejected;
        var human = correction is not null;
        return new(
            fact.FactCode,
            fact.DataType,
            fact.RawValue,
            fact.NormalizedValue,
            fact.NormalizedJson,
            fact.SourceConfidence,
            fact.NormalizationStatus,
            fact.ValidationStatus,
            ReadJsonArray(fact.WarningCodesJson),
            ReadJsonArray(fact.EvidenceReferenceJson),
            rejected ? null : correction?.NormalizedValue ?? fact.NormalizedValue,
            null,
            fact.Id,
            correction?.Id,
            correction?.SourceType ?? "AI",
            human,
            false,
            rejected);
    }

    private static IntakeReviewActivity Activity(
        Guid tenantId,
        Guid reviewId,
        string activityType,
        Guid? actorUserId,
        DateTimeOffset createdAt,
        string metadata) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeReviewId = reviewId,
            ActivityType = activityType,
            ActorUserId = actorUserId,
            SafeMetadataJson = metadata,
            CreatedAt = createdAt,
        };

    private static void Bump(
        IntakeReview review,
        Guid actorUserId,
        string activityType)
    {
        review.Status = review.Status == IntakeReviewStatuses.Pending
            ? IntakeReviewStatuses.InReview
            : review.Status;
        review.StartedAt ??= DateTimeOffset.UtcNow;
        review.Version++;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        review.Activities.Add(Activity(
            review.TenantId,
            review.Id,
            activityType,
            actorUserId,
            review.UpdatedAt,
            "{}"));
    }

    private static IntakeReviewCorrection? LatestCorrection(
        IntakeReview review,
        string factCode,
        string correctionType) =>
        review.Corrections
            .Where(item => item.FactCode == factCode &&
                           item.CorrectionType == correctionType)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

    private async Task SaveMutationAsync(
        IntakeReview review,
        CancellationToken cancellationToken)
    {
        try
        {
            await reviewRepository.SaveAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception.GetType().Name == "DbUpdateConcurrencyException")
        {
            logger.LogInformation(
                exception,
                "Concurrent Intake review mutation rejected. Review={ReviewId}",
                review.Id);
            throw IntakeConfigurationException.Conflict(
                IntakeReviewErrorCodes.ConcurrencyConflict,
                "The review changed while this action was being saved.");
        }
    }

    private async Task AuditAsync(
        string action,
        IntakeReview review,
        Guid actorUserId,
        string? decisionCode,
        string? outcome,
        string? factCode,
        string? correlationId,
        CancellationToken cancellationToken,
        string? entityType = null) =>
        await auditSink.RecordAsync(
            new ReviewAuditEntry(
                action,
                review.TenantId,
                review.Id,
                review.ArtifactId,
                actorUserId,
                review.Status,
                decisionCode,
                outcome,
                factCode,
                entityType,
                correlationId),
            cancellationToken);

    private static IntakeReviewResponse MapResponse(
        IntakeReview review,
        bool isStale) =>
        new(
            review.Id,
            review.TenantId,
            review.ArtifactId,
            review.ClassificationId,
            review.ArtifactExtractionId,
            review.ArtifactNormalizationId,
            review.ArtifactMatchRunId,
            review.ArtifactPolicyEvaluationId,
            review.Status,
            review.Priority,
            review.B11Disposition,
            review.ReviewOutcome,
            review.AssignedToUserId,
            review.AssignedAt,
            review.StartedAt,
            review.CompletedAt,
            review.CompletedByUserId,
            review.CompletionReasonCode,
            review.CompletionComment,
            review.RevisionNumber,
            review.Version,
            isStale,
            review.CreatedAt,
            review.UpdatedAt);

    private static IReadOnlyList<string> ReadJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string JsonEncoded(string value) =>
        JsonSerializer.Serialize(value).Trim('"');

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw IntakeConfigurationException.BadRequest(
                IntakeReviewErrorCodes.TenantContextInvalid,
                "A tenant context is required.");
    }

    private static void RequireUser(Guid userId)
    {
        if (userId == Guid.Empty)
            throw IntakeConfigurationException.Forbidden(
                IntakeReviewErrorCodes.UnauthorizedUser,
                "An authenticated LegalSynq identity is required.");
    }

    private sealed record ReviewContext(
        IntakeArtifact Artifact,
        ArtifactClassification? Classification,
        ArtifactExtraction? Extraction,
        ArtifactNormalization? Normalization,
        ArtifactMatchRun? Matching,
        ArtifactPolicyEvaluation Policy);
}