using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Application.Emails;
using Intake.Contracts.Configuration;
using Intake.Domain.Artifacts;
using Intake.Domain.Emails;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Artifacts;

public sealed class EmailArtifactProcessingService(
    IInboundEmailRepository emailRepository,
    IIntakeArtifactRepository artifactRepository,
    IIntakeConfigurationService configurationService,
    IEmailArtifactExtractor extractor,
    IIntakeDocumentsClient documentsClient,
    IEmailArtifactAuditSink auditSink,
    EmailArtifactProcessingOptions options,
    ILogger<EmailArtifactProcessingService> logger) : IEmailArtifactProcessingService
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

    public async Task<EmailArtifactProcessingResponse> ProcessAsync(
        Guid tenantId,
        Guid emailId,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var email = await emailRepository.FindTenantEmailAsync(
            tenantId,
            emailId,
            cancellationToken) ?? throw IntakeConfigurationException.NotFound(
            "INBOUND_EMAIL_NOT_FOUND",
            "The inbound email was not found for the current tenant.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            email.ProcessingProfileCode,
            cancellationToken);

        await artifactRepository.UpdateEmailProcessingStatusAsync(
            tenantId,
            emailId,
            InboundEmailArtifactProcessingStatuses.InProgress,
            cancellationToken);

        var candidates = BuildCandidates(email, resolved.EffectiveConfiguration);
        foreach (var candidate in candidates)
            await ProcessCandidateAsync(
                tenantId,
                email,
                candidate,
                resolved.EffectiveConfiguration.AllowUnsupportedDocuments,
                retryFailed: true,
                cancellationToken);

        var response = await FinishAsync(
            tenantId,
            email,
            candidates.Any(candidate => candidate.EmailLevelFailureCode is not null),
            actorId,
            correlationId,
            cancellationToken);
        return response;
    }

    public async Task<EmailArtifactProcessingResponse> RetryAsync(
        Guid tenantId,
        Guid emailId,
        Guid artifactId,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var email = await emailRepository.FindTenantEmailAsync(
            tenantId,
            emailId,
            cancellationToken) ?? throw IntakeConfigurationException.NotFound(
            "INBOUND_EMAIL_NOT_FOUND",
            "The inbound email was not found for the current tenant.");

        var artifact = await artifactRepository.FindAsync(tenantId, artifactId, cancellationToken);
        if (artifact is null || artifact.InboundEmailId != emailId)
            throw IntakeConfigurationException.NotFound(
                "INTAKE_ARTIFACT_NOT_FOUND",
                "The Intake artifact was not found for the current tenant and email.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            email.ProcessingProfileCode,
            cancellationToken);
        var candidate = BuildCandidates(email, resolved.EffectiveConfiguration)
            .FirstOrDefault(item => item.ArtifactKey == artifact.ArtifactKey);

        if (candidate is null)
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_ARTIFACT_NOT_RETRYABLE",
                "The requested artifact is no longer produced by the effective processing profile.");

        await ProcessCandidateAsync(
            tenantId,
            email,
            candidate,
            resolved.EffectiveConfiguration.AllowUnsupportedDocuments,
            retryFailed: true,
            cancellationToken);

        return await FinishAsync(
            tenantId,
            email,
            emailLevelFailure: false,
            actorId,
            correlationId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<IntakeArtifactResponse>> ListAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken)
    {
        var email = await emailRepository.FindTenantEmailAsync(tenantId, emailId, cancellationToken);
        if (email is null)
            throw IntakeConfigurationException.NotFound(
                "INBOUND_EMAIL_NOT_FOUND",
                "The inbound email was not found for the current tenant.");

        return (await artifactRepository.ListByEmailAsync(tenantId, emailId, cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task<IntakeArtifactReconciliationResponse?> ReconcileAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken)
    {
        var email = await emailRepository.FindTenantEmailAsync(tenantId, emailId, cancellationToken);
        if (email is null)
            return null;

        var metadata = email.AttachmentMetadata.ToArray();
        var artifacts = (await artifactRepository.ListByEmailAsync(tenantId, emailId, cancellationToken))
            .Where(artifact => artifact.ArtifactType == IntakeArtifactTypes.Attachment)
            .ToArray();

        var metadataIds = metadata.Select(item => item.Id).ToHashSet();
        var artifactMetadataIds = artifacts
            .Where(item => item.SourceAttachmentMetadataId.HasValue)
            .Select(item => item.SourceAttachmentMetadataId!.Value)
            .ToHashSet();
        var missingMetadataCount = metadata.Count(item => !artifactMetadataIds.Contains(item.Id));
        var missingArtifactCount = artifacts.Count(item =>
            !item.SourceAttachmentMetadataId.HasValue ||
            !metadataIds.Contains(item.SourceAttachmentMetadataId.Value));
        var warnings = new List<string>();
        if (missingMetadataCount > 0)
            warnings.Add("ATTACHMENT_METADATA_WITHOUT_ARTIFACT");
        if (missingArtifactCount > 0)
            warnings.Add("ARTIFACT_WITHOUT_ATTACHMENT_METADATA");

        return new(
            emailId,
            metadata.Length,
            artifacts.Length,
            missingMetadataCount,
            missingArtifactCount,
            warnings);
    }

    public Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid? emailId,
        CancellationToken cancellationToken) =>
        artifactRepository.GetAnalyticsAsync(tenantId, emailId, cancellationToken);

    private IReadOnlyList<Candidate> BuildCandidates(
        InboundEmail email,
        LienIntakeV1Configuration configuration)
    {
        var candidates = new List<Candidate>();
        string? emailLevelFailureCode = null;
        string? emailLevelFailureMessage = null;

        if (configuration.ProcessAttachments)
        {
            if (string.IsNullOrWhiteSpace(email.RawMessageContent))
            {
                emailLevelFailureCode = IntakeArtifactFailureCodes.RawMessageUnavailable;
                emailLevelFailureMessage = "Raw MIME content is unavailable for attachment processing.";
                candidates.AddRange(email.AttachmentMetadata.Select(metadata =>
                    Candidate.Failure(
                        AttachmentKey(metadata),
                        metadata.Ordinal,
                        metadata.FileName,
                        metadata.ContentType ?? "application/octet-stream",
                        metadata.ContentId,
                        metadata.IsInline,
                        metadata.Id,
                        IntakeArtifactFailureCodes.RawMessageUnavailable,
                        emailLevelFailureMessage)));
            }
            else
            {
                var extraction = extractor.Extract(email.RawMessageContent, options);
                if (extraction.FailureCode is not null)
                {
                    emailLevelFailureCode = extraction.FailureCode;
                    emailLevelFailureMessage = extraction.FailureMessage;
                    candidates.AddRange(email.AttachmentMetadata.Select(metadata =>
                        Candidate.Failure(
                            AttachmentKey(metadata),
                            metadata.Ordinal,
                            metadata.FileName,
                            metadata.ContentType ?? "application/octet-stream",
                            metadata.ContentId,
                            metadata.IsInline,
                            metadata.Id,
                            extraction.FailureCode,
                            extraction.FailureMessage)));
                }
                else
                {
                    var parts = extraction.Parts;
                    var totalBytes = parts.Sum(part => (long)part.Content.Length);
                    if (totalBytes > options.MaxTotalArtifactBytesPerEmail)
                    {
                        emailLevelFailureCode = IntakeArtifactFailureCodes.ArtifactBytesExceeded;
                        emailLevelFailureMessage = "The email artifacts exceed the configured aggregate size limit.";
                        candidates.AddRange(email.AttachmentMetadata.Select(metadata =>
                            Candidate.Failure(
                                AttachmentKey(metadata),
                                metadata.Ordinal,
                                metadata.FileName,
                                metadata.ContentType ?? "application/octet-stream",
                                metadata.ContentId,
                                metadata.IsInline,
                                metadata.Id,
                                emailLevelFailureCode,
                                emailLevelFailureMessage)));
                    }
                    else
                    {
                        var usedMetadataIds = new HashSet<Guid>();
                        foreach (var part in parts)
                        {
                            var metadata = FindMetadata(part, email.AttachmentMetadata, usedMetadataIds);
                            if (metadata is not null)
                                usedMetadataIds.Add(metadata.Id);
                            candidates.Add(Candidate.FromPart(
                                part,
                                metadata,
                                metadata is null
                                    ? IntakeArtifactFailureCodes.AttachmentMetadataMismatch
                                    : null,
                                metadata is null
                                    ? "The MIME attachment has no matching B04 attachment metadata."
                                    : null));
                        }

                        foreach (var metadata in email.AttachmentMetadata
                                     .Where(metadata => !usedMetadataIds.Contains(metadata.Id)))
                        {
                            candidates.Add(Candidate.Failure(
                                AttachmentKey(metadata),
                                metadata.Ordinal,
                                metadata.FileName,
                                metadata.ContentType ?? "application/octet-stream",
                                metadata.ContentId,
                                metadata.IsInline,
                                metadata.Id,
                                IntakeArtifactFailureCodes.AttachmentNotFound,
                                "The B04 attachment metadata has no corresponding MIME part."));
                        }
                    }
                }
            }
        }

        if (configuration.ProcessEmailBody)
        {
            if (!string.IsNullOrEmpty(email.TextBody))
            {
                candidates.Add(Candidate.Body(
                    IntakeArtifactTypes.TextBody,
                    IntakeArtifactRoles.TextBody,
                    100000,
                    "body.txt",
                    "text/plain",
                    Encoding.UTF8.GetBytes(email.TextBody)));
            }

            if (!string.IsNullOrEmpty(email.HtmlBody))
            {
                candidates.Add(Candidate.Body(
                    IntakeArtifactTypes.HtmlBody,
                    IntakeArtifactRoles.HtmlBody,
                    100001,
                    "body.html",
                    "text/html",
                    Encoding.UTF8.GetBytes(email.HtmlBody)));
            }
        }

        var totalCandidateBytes = candidates.Sum(candidate =>
            candidate.Part?.Content.LongLength ?? 0L);
        if (candidates.Count > options.MaxArtifactsPerEmail)
        {
            emailLevelFailureCode = IntakeArtifactFailureCodes.ArtifactCountExceeded;
            emailLevelFailureMessage = "The email contains more artifacts than the configured limit.";
        }
        else if (totalCandidateBytes > options.MaxTotalArtifactBytesPerEmail)
        {
            emailLevelFailureCode = IntakeArtifactFailureCodes.ArtifactBytesExceeded;
            emailLevelFailureMessage = "The email artifacts exceed the configured aggregate size limit.";
        }

        if (emailLevelFailureCode is not null &&
            candidates.Any(candidate => candidate.ImmediateFailureCode is null))
        {
            candidates = candidates
                .Select(candidate => candidate.ImmediateFailureCode is null
                    ? candidate with
                    {
                        ImmediateFailureCode = emailLevelFailureCode,
                        ImmediateFailureMessage = emailLevelFailureMessage,
                    }
                    : candidate)
                .ToList();
        }

        if (emailLevelFailureCode is not null && candidates.Count == 0)
        {
            candidates.Add(Candidate.Failure(
                "EMAIL_PROCESSING:0000",
                0,
                "email-processing",
                "message/rfc822",
                null,
                false,
                null,
                emailLevelFailureCode,
                emailLevelFailureMessage));
        }

        return candidates
            .Select(candidate => candidate with
            {
                EmailLevelFailureCode = emailLevelFailureCode,
                EmailLevelFailureMessage = emailLevelFailureMessage,
            })
            .ToArray();
    }

    private async Task ProcessCandidateAsync(
        Guid tenantId,
        InboundEmail email,
        Candidate candidate,
        bool allowUnsupportedDocuments,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        var artifact = await artifactRepository.FindByKeyAsync(
            tenantId,
            email.Id,
            candidate.ArtifactKey,
            cancellationToken);

        if (artifact is null)
        {
            artifact = await artifactRepository.AddOrGetAsync(
                candidate.ToEntity(email, options),
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(artifact.FailureCode) &&
            !artifact.IsRetryable &&
            artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed)
            return;

        if (candidate.ImmediateFailureCode is not null)
        {
            artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Failed;
            artifact.FailureCode = candidate.ImmediateFailureCode;
            artifact.FailureMessage = candidate.ImmediateFailureMessage;
            artifact.IsRetryable = false;
            artifact.UpdatedAt = DateTimeOffset.UtcNow;
            artifact.CompletedAt = artifact.UpdatedAt;
            await artifactRepository.SaveAsync(cancellationToken);
            return;
        }

        if (artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed)
            return;

        if (!await artifactRepository.TryClaimAsync(
                tenantId,
                artifact.Id,
                retryFailed,
                cancellationToken))
            return;

        artifact = await artifactRepository.FindAsync(tenantId, artifact.Id, cancellationToken)
            ?? throw new InvalidOperationException("The claimed Intake artifact could not be reloaded.");

        var uploadContentType = NormalizeUploadContentType(
            candidate.Part!.DeclaredContentType,
            candidate.Part.ArtifactType);
        if (!allowUnsupportedDocuments && !IsDocumentsSupported(uploadContentType))
        {
            SetSkipped(
                artifact,
                IntakeArtifactFailureCodes.UnsupportedContentType,
                "The artifact content type is not accepted by the Documents Service.");
            await artifactRepository.SaveAsync(cancellationToken);
            return;
        }

        var documentsReferenceId = artifact.Id.ToString();
        const string documentsReferenceType = "intake.email-artifact";
        if (artifact.AttemptCount > 1)
        {
            var lookup = await documentsClient.FindByReferenceAsync(
                tenantId,
                documentsReferenceId,
                documentsReferenceType,
                cancellationToken);
            if (!lookup.ServiceAvailable)
            {
                SetFailure(
                    artifact,
                    lookup.FailureCode ?? IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                    lookup.FailureMessage,
                    retryable: true);
                await artifactRepository.SaveAsync(cancellationToken);
                return;
            }

            if (lookup.Found)
            {
                artifact.DocumentsServiceDocumentId = lookup.DocumentId;
                artifact.DocumentsServiceVersionId = lookup.VersionId;
                artifact.DocumentsServiceReference = lookup.Reference;
                artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Completed;
                artifact.FailureCode = null;
                artifact.FailureMessage = null;
                artifact.IsRetryable = false;
                artifact.UploadedAt ??= DateTimeOffset.UtcNow;
                artifact.CompletedAt = DateTimeOffset.UtcNow;
                artifact.UpdatedAt = artifact.CompletedAt.Value;
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

        using var content = new MemoryStream(candidate.Part.Content, writable: false);
        var result = await documentsClient.UploadAsync(
            content,
            artifact.EffectiveFileName,
            uploadContentType,
            artifact.SizeBytes,
            tenantId,
            artifact.EffectiveFileName,
            BuildDocumentsDescription(email, artifact),
            options.DocumentsServiceProductId,
            documentTypeId.Value,
            documentsReferenceId,
            documentsReferenceType,
            cancellationToken);

        if (!result.Success)
        {
            SetFailure(
                artifact,
                result.FailureCode ?? IntakeArtifactFailureCodes.DocumentsServiceUnavailable,
                result.FailureMessage,
                result.IsRetryable);
            await artifactRepository.SaveAsync(cancellationToken);
            return;
        }

        artifact.DocumentsServiceDocumentId = result.DocumentId;
        artifact.DocumentsServiceVersionId = result.VersionId;
        artifact.DocumentsServiceReference = result.Reference;
        artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Completed;
        artifact.FailureCode = null;
        artifact.FailureMessage = null;
        artifact.IsRetryable = false;
        artifact.UploadedAt = DateTimeOffset.UtcNow;
        artifact.CompletedAt = artifact.UploadedAt;
        artifact.UpdatedAt = artifact.UploadedAt.Value;
        await artifactRepository.SaveAsync(cancellationToken);
    }

    private async Task<EmailArtifactProcessingResponse> FinishAsync(
        Guid tenantId,
        InboundEmail email,
        bool emailLevelFailure,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var artifacts = await artifactRepository.ListByEmailAsync(
            tenantId,
            email.Id,
            cancellationToken);
        var hasFailed = emailLevelFailure ||
                        artifacts.Any(artifact =>
                            artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed);
        var hasInProgress = artifacts.Any(artifact =>
            artifact.ProcessingStatus is IntakeArtifactProcessingStatuses.Pending
                or IntakeArtifactProcessingStatuses.Processing);
        var hasCompleted = artifacts.Any(artifact =>
            artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed);
        var status = hasInProgress
            ? InboundEmailArtifactProcessingStatuses.InProgress
            : hasFailed && hasCompleted
                ? InboundEmailArtifactProcessingStatuses.Partial
                : hasFailed
                    ? InboundEmailArtifactProcessingStatuses.Failed
                    : InboundEmailArtifactProcessingStatuses.Completed;

        await artifactRepository.UpdateEmailProcessingStatusAsync(
            tenantId,
            email.Id,
            status,
            cancellationToken);

        var completedCount = artifacts.Count(item =>
            item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed);
        var failedCount = artifacts.Count(item =>
            item.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed);
        var skippedCount = artifacts.Count(item =>
            item.ProcessingStatus == IntakeArtifactProcessingStatuses.Skipped);
        logger.LogInformation(
            "Inbound email artifact processing finished. Tenant={TenantId} Email={EmailId} Status={Status} ArtifactCount={ArtifactCount}",
            tenantId,
            email.Id,
            status,
            artifacts.Count);
        await auditSink.RecordAsync(
            new EmailArtifactAuditEntry(
                tenantId,
                email.Id,
                actorId,
                status,
                artifacts.Count,
                completedCount,
                failedCount,
                skippedCount,
                correlationId),
            cancellationToken);

        return new(
            email.Id,
            status,
            artifacts.Select(Map).ToArray());
    }

    private Guid? ResolveDocumentTypeId()
    {
        var configured = options.DocumentsServiceDocumentTypeId;
        return Guid.TryParse(configured, out var documentTypeId) && documentTypeId != Guid.Empty
            ? documentTypeId
            : null;
    }

    private static bool IsDocumentsSupported(string contentType) =>
        DocumentsSupportedMimeTypes.Contains(contentType);

    private static string NormalizeUploadContentType(string contentType, string artifactType) =>
        artifactType == IntakeArtifactTypes.HtmlBody && contentType == "text/html"
            ? "text/plain"
            : contentType;

    private static string BuildDocumentsDescription(
        InboundEmail email,
        IntakeArtifact artifact) =>
        $"source=synq-intake;email={email.Id};artifact={artifact.Id};type={artifact.ArtifactType};sha256={artifact.Sha256 ?? "not-computed"}";

    private static string AttachmentKey(int ordinal) =>
        $"{IntakeArtifactTypes.Attachment}:{ordinal:D6}";

    private static string AttachmentKey(InboundEmailAttachmentMetadata metadata) =>
        $"{IntakeArtifactTypes.Attachment}:META:{metadata.Id:N}";

    private static InboundEmailAttachmentMetadata? FindMetadata(
        ExtractedEmailPart part,
        IEnumerable<InboundEmailAttachmentMetadata> metadata,
        ISet<Guid> usedMetadataIds)
    {
        var available = metadata.Where(item => !usedMetadataIds.Contains(item.Id)).ToArray();
        var contentId = NormalizeContentId(part.SourceContentId);
        if (!string.IsNullOrWhiteSpace(contentId))
        {
            var byContentId = available.FirstOrDefault(item =>
                string.Equals(
                    NormalizeContentId(item.ContentId),
                    contentId,
                    StringComparison.OrdinalIgnoreCase));
            if (byContentId is not null)
                return byContentId;
        }

        var hash = Convert.ToHexString(SHA256.HashData(part.Content));
        var byHash = available.FirstOrDefault(item =>
            string.Equals(item.Sha256, hash, StringComparison.OrdinalIgnoreCase));
        if (byHash is not null)
            return byHash;

        var byNameAndSize = available.FirstOrDefault(item =>
            string.Equals(item.FileName.Trim(), part.OriginalFileName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            (!item.SizeBytes.HasValue || item.SizeBytes.Value == part.Content.LongLength));
        if (byNameAndSize is not null)
            return byNameAndSize;

        return available.FirstOrDefault(item => item.Ordinal == part.SourceOrdinal);
    }

    private static string? NormalizeContentId(string? contentId) =>
        string.IsNullOrWhiteSpace(contentId)
            ? null
            : contentId.Trim().Trim('<', '>');

    private static IntakeArtifactResponse Map(IntakeArtifact artifact) =>
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

    private static void SetFailure(
        IntakeArtifact artifact,
        string code,
        string? message,
        bool retryable)
    {
        artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Failed;
        artifact.FailureCode = code;
        artifact.FailureMessage = message;
        artifact.IsRetryable = retryable;
        artifact.CompletedAt = DateTimeOffset.UtcNow;
        artifact.UpdatedAt = artifact.CompletedAt.Value;
    }

    private static void SetSkipped(
        IntakeArtifact artifact,
        string code,
        string? message)
    {
        artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Skipped;
        artifact.FailureCode = code;
        artifact.FailureMessage = message;
        artifact.IsRetryable = false;
        artifact.CompletedAt = DateTimeOffset.UtcNow;
        artifact.UpdatedAt = artifact.CompletedAt.Value;
    }

    private sealed record Candidate(
        string ArtifactKey,
        int ArtifactOrdinal,
        string OriginalFileName,
        string DeclaredContentType,
        string? SourceContentId,
        bool IsInline,
        Guid? SourceAttachmentMetadataId,
        ExtractedEmailPart? Part,
        string? ImmediateFailureCode,
        string? ImmediateFailureMessage,
        string? EmailLevelFailureCode = null,
        string? EmailLevelFailureMessage = null)
    {
        public static Candidate FromPart(
            ExtractedEmailPart part,
            InboundEmailAttachmentMetadata? metadata,
            string? failureCode,
            string? failureMessage)
        {
            if (metadata is not null && metadata.SizeBytes.HasValue &&
                metadata.SizeBytes.Value != part.Content.LongLength)
            {
                failureCode = IntakeArtifactFailureCodes.AttachmentSizeMismatch;
                failureMessage = "The extracted attachment size does not match B04 metadata.";
            }
            else if (metadata is not null &&
                     !string.IsNullOrWhiteSpace(metadata.Sha256) &&
                     !string.Equals(
                         metadata.Sha256,
                         Convert.ToHexString(SHA256.HashData(part.Content)),
                         StringComparison.OrdinalIgnoreCase))
            {
                failureCode = IntakeArtifactFailureCodes.AttachmentHashMismatch;
                failureMessage = "The extracted attachment hash does not match B04 metadata.";
            }
            else if (metadata is not null &&
                     !string.IsNullOrWhiteSpace(metadata.FileName) &&
                     !string.Equals(
                         metadata.FileName.Trim(),
                         part.OriginalFileName.Trim(),
                         StringComparison.OrdinalIgnoreCase))
            {
                failureCode = IntakeArtifactFailureCodes.AttachmentMetadataMismatch;
                failureMessage = "The extracted attachment filename does not match B04 metadata.";
            }

            return new(
                AttachmentKey(part.SourceOrdinal),
                part.SourceOrdinal,
                part.OriginalFileName,
                part.DeclaredContentType,
                part.SourceContentId,
                part.IsInline,
                metadata?.Id,
                part,
                failureCode,
                failureMessage);
        }

        public static Candidate Failure(
            string artifactKey,
            int ordinal,
            string originalFileName,
            string contentType,
            string? contentId,
            bool isInline,
            Guid? metadataId,
            string failureCode,
            string? failureMessage) =>
            new(
                artifactKey,
                ordinal,
                originalFileName,
                contentType,
                contentId,
                isInline,
                metadataId,
                null,
                failureCode,
                failureMessage);

        public static Candidate Body(
            string type,
            string role,
            int ordinal,
            string fileName,
            string contentType,
            byte[] content)
        {
            var part = new ExtractedEmailPart(
                ordinal,
                type,
                role,
                fileName,
                contentType,
                null,
                false,
                content);
            return new($"{type}:{ordinal:D6}", ordinal, fileName, contentType, null, false, null, part, null, null);
        }

        public IntakeArtifact ToEntity(
            InboundEmail email,
            EmailArtifactProcessingOptions options)
        {
            var content = Part?.Content ?? [];
            var now = DateTimeOffset.UtcNow;
            var sha = content.Length == 0 ? null : Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            return new()
            {
                Id = Guid.NewGuid(),
                TenantId = email.TenantId,
                OrgId = email.OrgId,
                InboundEmailId = email.Id,
                ManualIntakeSubmissionId = null,
                TenantIntakeSourceId = email.TenantIntakeSourceId,
                ArtifactSourceType = Intake.Contracts.Sources.IntakeSourceTypes.Email,
                SourceAttachmentMetadataId = SourceAttachmentMetadataId,
                ArtifactKey = ArtifactKey,
                ArtifactType = Part?.ArtifactType ?? IntakeArtifactTypes.Attachment,
                ArtifactRole = Part?.ArtifactRole ??
                               (IsInline ? IntakeArtifactRoles.InlineAttachment : IntakeArtifactRoles.Attachment),
                ArtifactOrdinal = ArtifactOrdinal,
                SourceContentId = SourceContentId,
                OriginalFileName = OriginalFileName,
                EffectiveFileName = SafeFileName(
                    OriginalFileName,
                    ArtifactOrdinal,
                    Part?.ArtifactType == IntakeArtifactTypes.HtmlBody,
                    options.MaxFileNameLength),
                DeclaredContentType = DeclaredContentType,
                DetectedContentType = DetectContentType(content),
                SizeBytes = content.LongLength,
                Sha256 = sha,
                IsInline = IsInline,
                ProcessingStatus = ImmediateFailureCode is null
                    ? IntakeArtifactProcessingStatuses.Pending
                    : IntakeArtifactProcessingStatuses.Failed,
                FailureCode = ImmediateFailureCode,
                FailureMessage = ImmediateFailureMessage,
                IsRetryable = false,
                AttemptCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = ImmediateFailureCode is null ? null : now,
            };
        }

        private static string SafeFileName(
            string value,
            int ordinal,
            bool html,
            int maxFileNameLength)
        {
            var original = string.IsNullOrWhiteSpace(value)
                ? html ? "body.html" : $"attachment-{ordinal + 1:D3}.bin"
                : value.Trim();
            var sanitized = string.Join(
                "_",
                original.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
                .Replace("..", "_", StringComparison.Ordinal);
            sanitized = new string(sanitized.Where(character => !char.IsControl(character)).ToArray());
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = html ? "body.html" : $"attachment-{ordinal + 1:D3}.bin";
            if (sanitized.Length > maxFileNameLength)
                sanitized = sanitized[..maxFileNameLength];
            return $"{ordinal:D4}-{sanitized}";
        }

        private static string? DetectContentType(byte[] content)
        {
            if (content.AsSpan().StartsWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }))
                return "application/pdf";
            if (content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
                return "image/jpeg";
            if (content.AsSpan().StartsWith(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                return "image/png";
            return null;
        }
    }
}