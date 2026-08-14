using System.Security.Cryptography;
using Intake.Application.Artifacts;
using Intake.Application.Snapshot;
using Intake.Application.Configuration;
using Intake.Application.Emails;
using Intake.Contracts.Configuration;
using Intake.Contracts.Emails;
using Intake.Domain.Artifacts;
using Intake.Domain.Emails;
using Intake.Domain.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class EmailArtifactProcessingTests
{
    [Fact]
    public void Mime_extractor_returns_regular_and_inline_attachments_in_source_order()
    {
        var raw = """
            From: sender@example.com
            To: intake@example.com
            Subject: MIME test
            MIME-Version: 1.0
            Content-Type: multipart/mixed; boundary="b05"

            --b05
            Content-Type: text/plain; charset=utf-8

            body
            --b05
            Content-Type: application/pdf
            Content-Disposition: attachment; filename="notice.pdf"
            Content-Transfer-Encoding: base64

            JVBERi0xLjQK
            --b05
            Content-Type: image/png
            Content-ID: <logo@example.com>
            Content-Disposition: inline; filename="logo.png"
            Content-Transfer-Encoding: base64

            iVBORw0KGgo=
            --b05--
            """;

        var result = new Intake.Infrastructure.Artifacts.MimeKitEmailArtifactExtractor()
            .Extract(raw, new EmailArtifactProcessingOptions());

        Assert.Null(result.FailureCode);
        Assert.Equal(2, result.Parts.Count);
        Assert.Equal("notice.pdf", result.Parts[0].OriginalFileName);
        Assert.False(result.Parts[0].IsInline);
        Assert.Equal("logo.png", result.Parts[1].OriginalFileName);
        Assert.True(result.Parts[1].IsInline);
        Assert.Equal("logo@example.com", result.Parts[1].SourceContentId);
    }

    [Fact]
    public void Mime_extractor_rejects_input_over_the_configured_bound()
    {
        var result = new Intake.Infrastructure.Artifacts.MimeKitEmailArtifactExtractor()
            .Extract("0123456789", new EmailArtifactProcessingOptions
            {
                MaxMimeInputBytes = 5,
            });

        Assert.Equal(IntakeArtifactFailureCodes.MimeInputTooLarge, result.FailureCode);
        Assert.Empty(result.Parts);
    }

    [Fact]
    public async Task Processing_verifies_metadata_hash_before_upload_and_persists_documents_references()
    {
        var tenantId = Guid.NewGuid();
        var emailId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var artifactBytes = System.Text.Encoding.UTF8.GetBytes("plain text artifact");
        var email = new InboundEmail
        {
            Id = emailId,
            TenantId = tenantId,
            TenantIntakeSourceId = sourceId,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            RawMessageContent = "raw",
            ProcessingStatus = InboundEmailProcessingStatuses.NotStarted,
            AttachmentMetadata =
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    InboundEmailId = emailId,
                    FileName = "notice.txt",
                    ContentType = "text/plain",
                    SizeBytes = artifactBytes.Length,
                    Sha256 = Convert.ToHexString(SHA256.HashData(artifactBytes)),
                    Ordinal = 0,
                },
            ],
        };

        var repository = new FakeArtifactRepository(email);
        var documents = new FakeDocumentsClient();
        var service = new EmailArtifactProcessingService(
            new FakeEmailRepository(email),
            repository,
            new FakeConfigurationService(),
            new FakeExtractor(artifactBytes),
            documents,
            new NoopAuditSink(),
            new EmailArtifactProcessingOptions
            {
                DocumentsServiceDocumentTypeId = Guid.NewGuid().ToString(),
            },
            NullLogger<EmailArtifactProcessingService>.Instance);

        var result = await service.ProcessAsync(
            tenantId,
            emailId,
            null,
            "correlation-1",
            CancellationToken.None);

        var artifact = Assert.Single(result.Artifacts);
        Assert.Equal(InboundEmailArtifactProcessingStatuses.Completed, result.EmailProcessingStatus);
        Assert.Equal(IntakeArtifactProcessingStatuses.Completed, artifact.ProcessingStatus);
        Assert.NotNull(artifact.DocumentsServiceDocumentId);
        Assert.Equal(tenantId, documents.TenantId);
        Assert.Equal(artifact.Id.ToString(), documents.ReferenceId);
        Assert.Equal(artifactBytes.Length, documents.UploadedBytes.Length);
    }

    [Fact]
    public async Task Processing_records_hash_mismatch_without_calling_documents_service()
    {
        var tenantId = Guid.NewGuid();
        var email = new InboundEmail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantIntakeSourceId = Guid.NewGuid(),
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            RawMessageContent = "raw",
            AttachmentMetadata =
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    FileName = "notice.txt",
                    ContentType = "text/plain",
                    SizeBytes = 9,
                    Sha256 = new string('0', 64),
                    Ordinal = 0,
                },
            ],
        };
        var documents = new FakeDocumentsClient();
        var service = new EmailArtifactProcessingService(
            new FakeEmailRepository(email),
            new FakeArtifactRepository(email),
            new FakeConfigurationService(),
            new FakeExtractor(System.Text.Encoding.UTF8.GetBytes("different")),
            documents,
            new NoopAuditSink(),
            new EmailArtifactProcessingOptions
            {
                DocumentsServiceDocumentTypeId = Guid.NewGuid().ToString(),
            },
            NullLogger<EmailArtifactProcessingService>.Instance);

        var result = await service.ProcessAsync(
            tenantId,
            email.Id,
            null,
            null,
            CancellationToken.None);

        var artifact = Assert.Single(result.Artifacts);
        Assert.Equal(IntakeArtifactFailureCodes.AttachmentHashMismatch, artifact.FailureCode);
        Assert.Equal(IntakeArtifactProcessingStatuses.Failed, artifact.ProcessingStatus);
        Assert.Equal(0, documents.UploadCount);
    }

    private sealed class FakeExtractor(byte[] content) : IEmailArtifactExtractor
    {
        public EmailArtifactExtractionResult Extract(string rawMessage, EmailArtifactProcessingOptions options) =>
            new(
                [
                    new ExtractedEmailPart(
                        0,
                        IntakeArtifactTypes.Attachment,
                        IntakeArtifactRoles.Attachment,
                        "notice.txt",
                        "text/plain",
                        null,
                        false,
                        content),
                ],
                null,
                null);
    }

    private sealed class FakeDocumentsClient : IIntakeDocumentsClient
    {
        public Guid TenantId { get; private set; }
        public string ReferenceId { get; private set; } = string.Empty;
        public byte[] UploadedBytes { get; private set; } = [];
        public int UploadCount { get; private set; }

        public Task<DocumentMetadataResult> GetMetadataAsync(
            Guid tenantId,
            Guid documentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentMetadataResult(
                true, documentId, tenantId, "Active", "application/pdf", null, false));

        public Task<DocumentsLookupResult> FindByReferenceAsync(
            Guid tenantId,
            string referenceId,
            string referenceType,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentsLookupResult(true, false, null, null, null, null, null));

        public async Task<DocumentsUploadResult> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            long fileSizeBytes,
            Guid tenantId,
            string title,
            string description,
            string productId,
            Guid documentTypeId,
            string referenceId,
            string referenceType,
            CancellationToken cancellationToken)
        {
            TenantId = tenantId;
            ReferenceId = referenceId;
            UploadCount++;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            UploadedBytes = buffer.ToArray();
            return new(true, Guid.NewGuid(), Guid.NewGuid(), "documents:test", null, null, false);
        }
    }

    private sealed class NoopAuditSink : IEmailArtifactAuditSink
    {
        public Task RecordAsync(EmailArtifactAuditEntry entry, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeArtifactRepository(InboundEmail email) : IIntakeArtifactRepository
    {
        private readonly List<IntakeArtifact> artifacts = [];

        public Task<IReadOnlyList<IntakeArtifact>> ListByEmailAsync(
            Guid tenantId,
            Guid emailId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>(
                artifacts.Where(item => item.TenantId == tenantId && item.InboundEmailId == emailId).ToArray());

        public Task<IReadOnlyList<IntakeArtifact>> ListByManualSubmissionAsync(
            Guid tenantId,
            Guid submissionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>(
                artifacts.Where(item => item.TenantId == tenantId &&
                                        item.ManualIntakeSubmissionId == submissionId).ToArray());

        public Task<IntakeArtifact?> FindByManualKeyAsync(
            Guid tenantId,
            Guid submissionId,
            string artifactKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(artifacts.SingleOrDefault(item =>
                item.TenantId == tenantId &&
                item.ManualIntakeSubmissionId == submissionId &&
                item.ArtifactKey == artifactKey));

        public Task<IntakeArtifact?> FindAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult(artifacts.SingleOrDefault(item =>
                item.TenantId == tenantId && item.Id == artifactId));

        public Task<IntakeArtifact?> FindByKeyAsync(
            Guid tenantId,
            Guid emailId,
            string artifactKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(artifacts.SingleOrDefault(item =>
                item.TenantId == tenantId &&
                item.InboundEmailId == emailId &&
                item.ArtifactKey == artifactKey));

        public Task<IReadOnlyList<IntakeArtifact>> ListBySha256Async(
            Guid tenantId,
            string sha256,
            Guid excludedArtifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>(
                artifacts.Where(item => item.TenantId == tenantId &&
                                        item.Id != excludedArtifactId &&
                                        item.Sha256 == sha256).ToArray());

        public Task<IntakeArtifact> AddOrGetAsync(
            IntakeArtifact artifact,
            CancellationToken cancellationToken)
        {
            artifacts.Add(artifact);
            return Task.FromResult(artifact);
        }

        public Task<bool> TryClaimAsync(
            Guid tenantId,
            Guid artifactId,
            bool retryFailed,
            CancellationToken cancellationToken)
        {
            var artifact = artifacts.SingleOrDefault(item =>
                item.TenantId == tenantId && item.Id == artifactId);
            if (artifact is null ||
                (artifact.ProcessingStatus != IntakeArtifactProcessingStatuses.Pending &&
                 !(retryFailed &&
                   artifact.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed &&
                   artifact.IsRetryable)))
                return Task.FromResult(false);

            artifact.ProcessingStatus = IntakeArtifactProcessingStatuses.Processing;
            artifact.AttemptCount++;
            return Task.FromResult(true);
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateManualSubmissionStatusAsync(
            Guid tenantId,
            Guid submissionId,
            string status,
            string? failureMessage,
            DateTimeOffset? completedAt,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateEmailProcessingStatusAsync(
            Guid tenantId,
            Guid emailId,
            string status,
            CancellationToken cancellationToken)
        {
            email.ProcessingStatus = status;
            return Task.CompletedTask;
        }

        public Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(
            Guid tenantId,
            Guid? emailId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeArtifactAnalyticsResponse(
                tenantId,
                emailId,
                artifacts.Count,
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed),
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Failed),
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Skipped),
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Pending),
                artifacts.Count(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Processing),
                artifacts.Sum(item => item.SizeBytes),
                artifacts.Where(item => item.ProcessingStatus == IntakeArtifactProcessingStatuses.Completed)
                    .Sum(item => item.SizeBytes)));
    }

    private sealed class FakeEmailRepository(InboundEmail email) : IInboundEmailRepository
    {
        public Task<InboundEmail?> FindTenantEmailAsync(
            Guid tenantId,
            Guid emailId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InboundEmail?>(
                email.TenantId == tenantId && email.Id == emailId ? email : null);

        public Task RecordCaptureFailureAsync(InboundEmailCaptureFailure failure, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<InboundEmailPersistenceResult> PersistCaptureAsync(
            InboundEmail email,
            IReadOnlyList<InboundEmailRecipient> recipients,
            IReadOnlyList<InboundEmailAttachmentMetadata> attachments,
            CancellationToken cancellationToken) =>
            Task.FromResult(new InboundEmailPersistenceResult(email.Id, false));

        public Task<InboundEmail?> FindByProviderIdentityAsync(Guid tenantId, Guid sourceId, string provider, string providerMessageId, CancellationToken cancellationToken) =>
            Task.FromResult<InboundEmail?>(null);

        public Task<InboundEmail?> FindByInternetMessageIdAsync(Guid tenantId, Guid sourceId, string internetMessageId, CancellationToken cancellationToken) =>
            Task.FromResult<InboundEmail?>(null);

        public Task<PagedInboundEmailResponse> ListAsync(Guid tenantId, InboundEmailListQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InboundEmailAnalyticsResponse> GetAnalyticsAsync(Guid tenantId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeConfigurationService : IIntakeConfigurationService
    {
        private static readonly LienIntakeV1Configuration Config = new()
        {
            ProcessAttachments = true,
            ProcessEmailBody = false,
            AllowUnsupportedDocuments = false,
        };

        public Task<ResolvedProcessingConfiguration> ResolveAsync(Guid tenantId, string? profileCode, CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedProcessingConfiguration(
                tenantId,
                ProcessingProfileCodes.LienIntakeV1,
                1,
                1,
                1,
                Config,
                DateTimeOffset.UtcNow));

        public Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(Guid tenantId, UpsertTenantIntakeConfigurationRequest request, Guid? actorId, string? correlationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> AssignProfileAsync(Guid tenantId, AssignTenantProcessingProfileRequest request, Guid? actorId, string? correlationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(Guid tenantId, string profileCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(Guid tenantId, string profileCode, UpdateTenantProcessingProfileRequest request, Guid? actorId, string? correlationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(Guid tenantId, string profileCode, UpdateTenantProcessingProfileStatusRequest request, Guid? actorId, string? correlationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}