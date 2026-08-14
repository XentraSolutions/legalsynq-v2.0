using System.Security.Cryptography;
using Intake.Application.Artifacts;
using Intake.Application.Configuration;
using Intake.Application.Sources;
using Intake.Contracts.Sources;
using Intake.Domain.Artifacts;
using Intake.Domain.Manual;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Manual;

public sealed class ManualIntakeService(
    IManualIntakeRepository submissionRepository,
    IIntakeArtifactRepository artifactRepository,
    IIntakeConfigurationService configurationService,
    IIntakeSourcePurposeRegistry purposeRegistry,
    IIntakeDocumentsClient documentsClient,
    IManualIntakeAuditSink auditSink,
    EmailArtifactProcessingOptions options,
    ILogger<ManualIntakeService> logger) : IManualIntakeService
{
    private static readonly HashSet<string> DocumentsSupportedMimeTypes =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/jpeg",
        "image/png",
        "image/tiff",
        "text/plain",
        "text/csv",
    ];

    public async Task<ManualIntakeSubmissionResponse> CreateAndSubmitAsync(
        Guid tenantId,
        Guid? orgId,
        Guid? actorId,
        string? correlationId,
        CreateManualIntakeRequest request,
        CancellationToken cancellationToken)
    {
        var purpose = purposeRegistry.GetRequired(request.Purpose);
        if (request.Files.Count == 0)
            throw IntakeConfigurationException.BadRequest(
                "MANUAL_FILES_REQUIRED",
                "At least one file is required for a manual Intake submission.");

        ValidateRequest(request.Files);
        var resolved = await configurationService.ResolveAsync(
            tenantId,
            request.ProcessingProfileCode,
            cancellationToken);

        var clientRequestId = NormalizeClientRequestId(request.ClientRequestId);
        if (clientRequestId is not null)
        {
            var existing = await submissionRepository.FindByClientRequestIdAsync(
                tenantId,
                clientRequestId,
                cancellationToken);
            if (existing is not null)
                return await BuildResponseAsync(tenantId, existing, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var submission = new ManualIntakeSubmission
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrgId = orgId,
            SourceType = IntakeSourceTypes.Manual,
            Purpose = purpose,
            ProcessingProfileCode = resolved.ProcessingProfileCode,
            Title = TrimOrNull(request.Title, 512),
            ExternalReference = TrimOrNull(request.ExternalReference, 256),
            Notes = TrimOrNull(request.Notes, 4000),
            ClientRequestId = clientRequestId,
            SubmittedBy = actorId,
            SubmittedAt = now,
            Status = ManualIntakeSubmissionStatuses.Processing,
            ConfigurationVersion = resolved.TenantConfigurationVersion,
            ProfileConfigurationVersion = resolved.TenantProfileConfigurationVersion,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };

        try
        {
            await submissionRepository.AddAsync(submission, cancellationToken);
        }
        catch (InvalidOperationException) when (clientRequestId is not null)
        {
            var existing = await submissionRepository.FindByClientRequestIdAsync(
                tenantId,
                clientRequestId,
                cancellationToken);
            if (existing is not null)
                return await BuildResponseAsync(tenantId, existing, cancellationToken);
            throw IntakeConfigurationException.Conflict(
                "MANUAL_IDEMPOTENCY_CONFLICT",
                "The manual submission idempotency key is already being processed.");
        }

        for (var ordinal = 0; ordinal < request.Files.Count; ordinal++)
        {
            var file = request.Files[ordinal];
            var artifact = await artifactRepository.AddOrGetAsync(
                CreateArtifact(submission, file, ordinal),
                cancellationToken);
            await ProcessArtifactAsync(
                tenantId,
                submission,
                artifact,
                file,
                resolved.EffectiveConfiguration.AllowUnsupportedDocuments,
                cancellationToken);
        }

        await FinalizeAsync(
            tenantId,
            submission,
            actorId,
            correlationId,
            "SUBMIT",
            cancellationToken);
        return await BuildResponseAsync(tenantId, submission, cancellationToken);
    }

    public async Task<ManualIntakeListResponse> ListAsync(
        Guid tenantId,
        ManualIntakeListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Clamp(query.Page, 1, 10_000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var (items, totalCount) = await submissionRepository.ListAsync(
            tenantId,
            new ManualIntakeListQuery
            {
                Status = query.Status,
                Purpose = query.Purpose,
                Page = page,
                PageSize = pageSize,
            },
            cancellationToken);
        var responses = new List<ManualIntakeSubmissionResponse>(items.Count);
        foreach (var item in items)
            responses.Add(await BuildResponseAsync(tenantId, item, cancellationToken));
        return new(responses, page, pageSize, totalCount);
    }

    public async Task<ManualIntakeSubmissionResponse?> GetAsync(
        Guid tenantId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.FindAsync(tenantId, submissionId, cancellationToken);
        return submission is null
            ? null
            : await BuildResponseAsync(tenantId, submission, cancellationToken);
    }

    public async Task<ManualIntakeSubmissionResponse> RetryArtifactAsync(
        Guid tenantId,
        Guid submissionId,
        Guid artifactId,
        ManualIntakeFile file,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ValidateRequest([file]);
        var submission = await submissionRepository.FindAsync(tenantId, submissionId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "MANUAL_SUBMISSION_NOT_FOUND",
                "The manual Intake submission was not found for the current tenant.");
        if (submission.Status == ManualIntakeSubmissionStatuses.Cancelled)
            throw IntakeConfigurationException.Conflict(
                "MANUAL_SUBMISSION_CANCELLED",
                "Cancelled manual Intake submissions cannot be retried.");

        var artifact = await artifactRepository.FindAsync(tenantId, artifactId, cancellationToken);
        if (artifact is null || artifact.ManualIntakeSubmissionId != submissionId)
            throw IntakeConfigurationException.NotFound(
                "INTAKE_ARTIFACT_NOT_FOUND",
                "The manual Intake artifact was not found for the current tenant and submission.");
        if (!artifact.IsRetryable)
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_ARTIFACT_NOT_RETRYABLE",
                "The requested manual artifact is not retryable.");

        var hash = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant();
        if (!string.Equals(hash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw IntakeConfigurationException.BadRequest(
                IntakeArtifactFailureCodes.ManualFileHashMismatch,
                "The replacement file does not match the failed artifact.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            submission.ProcessingProfileCode,
            cancellationToken);
        await ProcessArtifactAsync(
            tenantId,
            submission,
            artifact,
            file,
            resolved.EffectiveConfiguration.AllowUnsupportedDocuments,
            cancellationToken);
        await FinalizeAsync(
            tenantId,
            submission,
            actorId,
            correlationId,
            "RETRY_ARTIFACT",
            cancellationToken);
        return await BuildResponseAsync(tenantId, submission, cancellationToken);
    }

    public async Task<ManualIntakeSubmissionResponse> CancelAsync(
        Guid tenantId,
        Guid submissionId,
        int expectedVersion,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.FindAsync(tenantId, submissionId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "MANUAL_SUBMISSION_NOT_FOUND",
                "The manual Intake submission was not found for the current tenant.");
        if (submission.Version != expectedVersion)
            throw IntakeConfigurationException.Conflict(
                "STALE_MANUAL_SUBMISSION_VERSION",
                "The manual submission changed since it was loaded. Refresh and try again.");
        if (submission.Status is ManualIntakeSubmissionStatuses.Completed
            or ManualIntakeSubmissionStatuses.Cancelled)
            throw IntakeConfigurationException.Conflict(
                "MANUAL_SUBMISSION_NOT_CANCELLABLE",
                "Only processing or failed manual submissions can be cancelled.");

        await artifactRepository.UpdateManualSubmissionStatusAsync(
            tenantId,
            submissionId,
            ManualIntakeSubmissionStatuses.Cancelled,
            "Cancelled by an Intake operator.",
            DateTimeOffset.UtcNow,
            cancellationToken);
        var current = await submissionRepository.FindAsync(tenantId, submissionId, cancellationToken)
            ?? submission;
        await auditSink.RecordAsync(
            new ManualIntakeAuditEntry(
                tenantId,
                submissionId,
                actorId,
                "CANCEL",
                current.Status,
                0,
                0,
                0,
                0,
                correlationId),
            cancellationToken);
        return await BuildResponseAsync(tenantId, current, cancellationToken);
    }

    public async Task<ManualIntakeAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var submissionQuery = await submissionRepository.ListAllAsync(tenantId, cancellationToken);
        var artifacts = new List<IntakeArtifact>();
        foreach (var submission in submissionQuery)
        {
            artifacts.AddRange(await artifactRepository.ListByManualSubmissionAsync(
                tenantId,
                submission.Id,
                cancellationToken));
        }

        return new(
            tenantId,
            submissionQuery.Count,
            submissionQuery.LongCount(item => item.Status == ManualIntakeSubmissionStatuses.Completed),
            submissionQuery.LongCount(item => item.Status == ManualIntakeSubmissionStatuses.Partial),
            submissionQuery.LongCount(item => item.Status == ManualIntakeSubmissionStatuses.Failed),
            submissionQuery.LongCount(item => item.Status == ManualIntakeSubmissionStatuses.Cancelled),
            artifacts.Count,
            artifacts.LongCount(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed),
            artifacts.LongCount(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed),
            artifacts.Sum(item => item.SizeBytes),
            artifacts.Where(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed)
                .Sum(item => item.SizeBytes));
    }

    private async Task ProcessArtifactAsync(
        Guid tenantId,
        ManualIntakeSubmission submission,
        IntakeArtifact artifact,
        ManualIntakeFile file,
        bool allowUnsupportedDocuments,
        CancellationToken cancellationToken)
    {
        if (artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed)
            return;
        if (!await artifactRepository.TryClaimAsync(
                tenantId,
                artifact.Id,
                retryFailed: true,
                cancellationToken))
            return;

        artifact = await artifactRepository.FindAsync(tenantId, artifact.Id, cancellationToken)
            ?? throw new InvalidOperationException("The claimed manual Intake artifact could not be reloaded.");
        var contentType = NormalizeUploadContentType(file.ContentType);
        if (!allowUnsupportedDocuments && !DocumentsSupportedMimeTypes.Contains(contentType))
        {
            artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Skipped;
            artifact.FailureCode = IntakeArtifactFailureCodes.UnsupportedContentType;
            artifact.FailureMessage = "The artifact content type is not accepted by the Documents Service.";
            artifact.IsRetryable = false;
            artifact.CompletedAt = DateTimeOffset.UtcNow;
            artifact.UpdatedAt = artifact.CompletedAt.Value;
            await artifactRepository.SaveAsync(cancellationToken);
            return;
        }

        const string referenceType = "intake.manual-artifact";
        if (artifact.AttemptCount > 1)
        {
            var lookup = await documentsClient.FindByReferenceAsync(
                tenantId,
                artifact.Id.ToString(),
                referenceType,
                cancellationToken);
            if (!lookup.ServiceAvailable)
            {
                SetFailure(artifact, lookup.FailureCode, lookup.FailureMessage, retryable: true);
                await artifactRepository.SaveAsync(cancellationToken);
                return;
            }
            if (lookup.Found)
            {
                Complete(artifact, lookup.DocumentId, lookup.VersionId, lookup.Reference);
                await artifactRepository.SaveAsync(cancellationToken);
                return;
            }
        }

        var documentTypeId = ResolveDocumentTypeId();
        if (!documentTypeId.HasValue)
        {
            SetFailure(
                artifact,
                IntakeArtifactFailureCodes.ProcessingConfigurationInvalid,
                "DocumentsService:DocumentTypeId is not configured as a valid UUID.",
                retryable: false);
            await artifactRepository.SaveAsync(cancellationToken);
            return;
        }

        using var content = new MemoryStream(file.Content, writable: false);
        var result = await documentsClient.UploadAsync(
            content,
            artifact.EffectiveFileName,
            contentType,
            artifact.SizeBytes,
            tenantId,
            artifact.EffectiveFileName,
            $"source=synq-intake;submission={submission.Id};artifact={artifact.Id};type={artifact.ArtifactType};sha256={artifact.Sha256 ?? "not-computed"}",
            options.DocumentsServiceProductId,
            documentTypeId.Value,
            artifact.Id.ToString(),
            referenceType,
            cancellationToken);
        if (!result.Success)
        {
            SetFailure(
                artifact,
                result.FailureCode,
                result.FailureMessage,
                result.IsRetryable);
            await artifactRepository.SaveAsync(cancellationToken);
            return;
        }

        Complete(artifact, result.DocumentId, result.VersionId, result.Reference);
        await artifactRepository.SaveAsync(cancellationToken);
    }

    private async Task FinalizeAsync(
        Guid tenantId,
        ManualIntakeSubmission submission,
        Guid? actorId,
        string? correlationId,
        string action,
        CancellationToken cancellationToken)
    {
        var artifacts = await artifactRepository.ListByManualSubmissionAsync(
            tenantId,
            submission.Id,
            cancellationToken);
        var hasFailed = artifacts.Any(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed);
        var hasInProgress = artifacts.Any(item => item.ProcessingStatus is
            IntakeArtifactProcessingStatuses.Pending or IntakeArtifactProcessingStatuses.Processing);
        var hasCompleted = artifacts.Any(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed);
        var hasSkipped = artifacts.Any(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Skipped);
        var status = hasInProgress
            ? ManualIntakeSubmissionStatuses.Processing
            : hasFailed && hasCompleted
                ? ManualIntakeSubmissionStatuses.Partial
                : hasFailed || (hasSkipped && !hasCompleted)
                    ? ManualIntakeSubmissionStatuses.Failed
                    : ManualIntakeSubmissionStatuses.Completed;
        DateTimeOffset? completedAt = status == ManualIntakeSubmissionStatuses.Processing
            ? null
            : DateTimeOffset.UtcNow;
        await artifactRepository.UpdateManualSubmissionStatusAsync(
            tenantId,
            submission.Id,
            status,
            hasFailed ? "One or more manual artifacts failed during Documents processing." : null,
            completedAt,
            cancellationToken);
        submission.Status = status;
        submission.FailureMessage = hasFailed
            ? "One or more manual artifacts failed during Documents processing."
            : null;
        submission.CompletedAt = completedAt;
        submission.UpdatedAt = DateTimeOffset.UtcNow;
        submission.Version++;
        logger.LogInformation(
            "Manual Intake submission finalized. TenantId={TenantId} SubmissionId={SubmissionId} Status={Status} ArtifactCount={ArtifactCount}",
            tenantId,
            submission.Id,
            status,
            artifacts.Count);
        await auditSink.RecordAsync(
            new ManualIntakeAuditEntry(
                tenantId,
                submission.Id,
                actorId,
                action,
                status,
                artifacts.Count,
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed),
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed),
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Skipped),
                correlationId),
            cancellationToken);
    }

    private async Task<ManualIntakeSubmissionResponse> BuildResponseAsync(
        Guid tenantId,
        ManualIntakeSubmission submission,
        CancellationToken cancellationToken)
    {
        var artifacts = await artifactRepository.ListByManualSubmissionAsync(
            tenantId,
            submission.Id,
            cancellationToken);
        return new(
            submission.Id,
            submission.TenantId,
            submission.OrgId,
            submission.TenantIntakeSourceId,
            submission.SourceType,
            submission.Purpose,
            submission.ProcessingProfileCode,
            submission.Title,
            submission.ExternalReference,
            submission.Notes,
            submission.ClientRequestId,
            submission.SubmittedBy,
            submission.SubmittedAt,
            submission.Status,
            submission.FailureMessage,
            submission.ConfigurationVersion,
            submission.ProfileConfigurationVersion,
            submission.Version,
            submission.CreatedAt,
            submission.UpdatedAt,
            submission.CompletedAt,
            artifacts.Select(MapArtifact).ToArray());
    }

    private IntakeArtifact CreateArtifact(
        ManualIntakeSubmission submission,
        ManualIntakeFile file,
        int ordinal)
    {
        var originalName = string.IsNullOrWhiteSpace(file.FileName)
            ? $"manual-file-{ordinal + 1:D3}.bin"
            : file.FileName.Trim();
        var safeName = SafeFileName(originalName, ordinal);
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = submission.TenantId,
            OrgId = submission.OrgId,
            InboundEmailId = null,
            ManualIntakeSubmissionId = submission.Id,
            TenantIntakeSourceId = submission.TenantIntakeSourceId,
            ArtifactSourceType = IntakeSourceTypes.Manual,
            ArtifactKey = $"manual-file:{ordinal:D6}",
            ArtifactType = IntakeArtifactTypes.ManualFile,
            ArtifactRole = IntakeArtifactRoles.Attachment,
            ArtifactOrdinal = ordinal,
            OriginalFileName = originalName,
            EffectiveFileName = safeName,
            DeclaredContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType.Trim().ToLowerInvariant(),
            DetectedContentType = DetectContentType(file.Content),
            SizeBytes = file.Content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant(),
            IsInline = false,
            ProcessingStatus = IntakeArtifactProcessingStatuses.Pending,
            AttemptCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private void ValidateRequest(IReadOnlyList<ManualIntakeFile> files)
    {
        var maxFiles = options.MaxManualFiles > 0 ? options.MaxManualFiles : options.MaxArtifactsPerEmail;
        var maxFileBytes = options.MaxManualFileBytes > 0 ? options.MaxManualFileBytes : options.MaxArtifactBytes;
        var maxTotalBytes = options.MaxTotalManualFileBytes > 0
            ? options.MaxTotalManualFileBytes
            : options.MaxTotalArtifactBytesPerEmail;
        if (files.Count > maxFiles)
            throw IntakeConfigurationException.BadRequest(
                IntakeArtifactFailureCodes.ArtifactCountExceeded,
                $"A manual submission may contain at most {maxFiles} files.");
        var totalBytes = files.Sum(file => (long)file.Content.LongLength);
        if (totalBytes > maxTotalBytes)
            throw IntakeConfigurationException.BadRequest(
                IntakeArtifactFailureCodes.ArtifactBytesExceeded,
                $"A manual submission may contain at most {maxTotalBytes} total bytes.");
        if (files.Any(file => file.Content.LongLength > maxFileBytes))
            throw IntakeConfigurationException.BadRequest(
                IntakeArtifactFailureCodes.ArtifactBytesExceeded,
                $"Each manual file may contain at most {maxFileBytes} bytes.");
    }

    private Guid? ResolveDocumentTypeId() =>
        Guid.TryParse(options.DocumentsServiceDocumentTypeId, out var value) && value != Guid.Empty
            ? value
            : null;

    private static string NormalizeUploadContentType(string contentType) =>
        contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            ? "text/plain"
            : contentType;

    private static string SafeFileName(string value, int ordinal)
    {
        var sanitized = string.Join(
            "_",
            value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
            .Replace("..", "_", StringComparison.Ordinal);
        sanitized = new string(sanitized.Where(character => !char.IsControl(character)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = $"manual-file-{ordinal + 1:D3}.bin";
        if (sanitized.Length > 240)
            sanitized = sanitized[..240];
        return $"{ordinal:D4}-{sanitized}";
    }

    private static string? DetectContentType(byte[] content) =>
        content.AsSpan().StartsWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }) ? "application/pdf" :
        content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) ? "image/jpeg" :
        content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ? "image/png" :
        null;

    private static string? NormalizeClientRequestId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : TrimOrNull(value, 256);

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static void SetFailure(
        IntakeArtifact artifact,
        string? code,
        string? message,
        bool retryable)
    {
        artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Failed;
        artifact.FailureCode = code ?? IntakeArtifactFailureCodes.DocumentsServiceUnavailable;
        artifact.FailureMessage = message;
        artifact.IsRetryable = retryable;
        artifact.CompletedAt = DateTimeOffset.UtcNow;
        artifact.UpdatedAt = artifact.CompletedAt.Value;
    }

    private static void Complete(
        IntakeArtifact artifact,
        Guid? documentId,
        Guid? versionId,
        string? reference)
    {
        artifact.DocumentsServiceDocumentId = documentId;
        artifact.DocumentsServiceVersionId = versionId;
        artifact.DocumentsServiceReference = reference;
        artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Completed;
        artifact.FailureCode = null;
        artifact.FailureMessage = null;
        artifact.IsRetryable = false;
        artifact.UploadedAt ??= DateTimeOffset.UtcNow;
        artifact.CompletedAt = artifact.UploadedAt;
        artifact.UpdatedAt = artifact.UploadedAt.Value;
    }

    private static IntakeArtifactResponse MapArtifact(IntakeArtifact artifact) =>
        new(
            artifact.Id,
            artifact.InboundEmailId,
            artifact.ManualIntakeSubmissionId,
            artifact.ArtifactSourceType,
            artifact.SourceAttachmentMetadataId,
            artifact.ArtifactKey,
            artifact.ArtifactType,
            artifact.ArtifactRole,
            artifact.ArtifactOrdinal,
            artifact.SourceContentId,
            artifact.OriginalFileName,
            artifact.EffectiveFileName,
            artifact.DeclaredContentType,
            artifact.DetectedContentType,
            artifact.SizeBytes,
            artifact.Sha256,
            artifact.IsInline,
            artifact.ProcessingStatus,
            artifact.FailureCode,
            artifact.FailureMessage,
            artifact.IsRetryable,
            artifact.AttemptCount,
            artifact.DocumentsServiceDocumentId,
            artifact.DocumentsServiceVersionId,
            artifact.DocumentsServiceReference,
            artifact.UploadedAt,
            artifact.CompletedAt,
            artifact.UpdatedAt);
}