using System.Security.Cryptography;
using Intake.Application.Artifacts;
using Intake.Application.Snapshot;
using Intake.Application.Configuration;
using Intake.Application.Manual;
using Intake.Application.Sources;
using Intake.Contracts.Configuration;
using Intake.Contracts.Sources;
using Intake.Domain.Artifacts;
using Intake.Domain.Manual;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class ManualIntakeTests
{
    [Fact]
    public async Task Create_and_submit_creates_manual_artifacts_and_uploads_them()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeManualRepository();
        var artifacts = new FakeArtifactRepository(repository);
        var documents = new FakeDocumentsClient();
        var service = CreateService(repository, artifacts, documents);

        var response = await service.CreateAndSubmitAsync(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "corr-manual-1",
            Request("first.pdf", [0x25, 0x50, 0x44, 0x46]),
            CancellationToken.None);

        var artifact = Assert.Single(response.Artifacts);
        Assert.Equal(IntakeSourceTypes.Manual, response.SourceType);
        Assert.Equal(IntakeArtifactTypes.ManualFile, artifact.ArtifactType);
        Assert.Equal(IntakeSourceTypes.Manual, artifact.ArtifactSourceType);
        Assert.Equal(ManualIntakeSubmissionStatuses.Completed, response.Status);
        Assert.Equal(1, documents.UploadCount);
        Assert.Equal(tenantId, documents.LastTenantId);
    }

    [Fact]
    public async Task Failed_upload_is_partial_and_can_be_retried_with_matching_file()
    {
        var repository = new FakeManualRepository();
        var artifacts = new FakeArtifactRepository(repository);
        var documents = new FakeDocumentsClient { FailNextUpload = true };
        var service = CreateService(repository, artifacts, documents);
        var file = new ManualIntakeFile("retry.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]);

        var initial = await service.CreateAndSubmitAsync(
            Guid.NewGuid(),
            null,
            null,
            null,
            new CreateManualIntakeRequest
            {
                Purpose = IntakeSourcePurposes.LienIntake,
                Files = [file],
            },
            CancellationToken.None);

        var failed = Assert.Single(initial.Artifacts);
        Assert.Equal(ManualIntakeSubmissionStatuses.Failed, initial.Status);
        Assert.True(failed.IsRetryable);

        var retried = await service.RetryArtifactAsync(
            initial.TenantId,
            initial.Id,
            failed.Id,
            file,
            null,
            "corr-manual-retry",
            CancellationToken.None);

        Assert.Equal(ManualIntakeSubmissionStatuses.Completed, retried.Status);
        Assert.Equal(IntakeArtifactProcessingStatuses.Completed, Assert.Single(retried.Artifacts).ProcessingStatus);
        Assert.Equal(2, documents.UploadCount);
    }

    [Fact]
    public async Task Idempotency_is_tenant_scoped_and_returns_existing_submission()
    {
        var repository = new FakeManualRepository();
        var artifacts = new FakeArtifactRepository(repository);
        var documents = new FakeDocumentsClient();
        var service = CreateService(repository, artifacts, documents);
        var request = new CreateManualIntakeRequest
        {
            Purpose = IntakeSourcePurposes.LienIntake,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            ClientRequestId = "operator-request-7",
            Files = [new ManualIntakeFile("same.txt", "text/plain", [1, 2, 3])],
        };

        var tenantA = Guid.NewGuid();
        var first = await service.CreateAndSubmitAsync(
            tenantA, null, null, null, request, CancellationToken.None);
        var duplicate = await service.CreateAndSubmitAsync(
            tenantA, null, null, null, request, CancellationToken.None);
        var otherTenant = await service.CreateAndSubmitAsync(
            Guid.NewGuid(), null, null, null, request, CancellationToken.None);

        Assert.Equal(first.Id, duplicate.Id);
        Assert.NotEqual(first.Id, otherTenant.Id);
        Assert.Equal(2, documents.UploadCount);
    }

    [Fact]
    public async Task Retry_rejects_a_replacement_file_with_a_different_hash()
    {
        var repository = new FakeManualRepository();
        var artifacts = new FakeArtifactRepository(repository);
        var documents = new FakeDocumentsClient { FailNextUpload = true };
        var service = CreateService(repository, artifacts, documents);
        var initial = await service.CreateAndSubmitAsync(
            Guid.NewGuid(),
            null,
            null,
            null,
            Request("hash.txt", [1, 2, 3]),
            CancellationToken.None);
        var artifact = Assert.Single(initial.Artifacts);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.RetryArtifactAsync(
                initial.TenantId,
                initial.Id,
                artifact.Id,
                new ManualIntakeFile("hash.txt", "text/plain", [3, 2, 1]),
                null,
                null,
                CancellationToken.None));

        Assert.Equal(IntakeArtifactFailureCodes.ManualFileHashMismatch, exception.Code);
        Assert.Equal(1, documents.UploadCount);
    }

    private static ManualIntakeService CreateService(
        FakeManualRepository repository,
        FakeArtifactRepository artifacts,
        FakeDocumentsClient documents) =>
        new(
            repository,
            artifacts,
            new FakeConfigurationService(),
            new IntakeSourcePurposeRegistry(),
            documents,
            new NoopAuditSink(),
            new EmailArtifactProcessingOptions
            {
                DocumentsServiceDocumentTypeId = Guid.NewGuid().ToString(),
            },
            NullLogger<ManualIntakeService>.Instance);

    private static CreateManualIntakeRequest Request(string name, byte[] content) =>
        new()
        {
            Purpose = IntakeSourcePurposes.LienIntake,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            Title = "Operator submission",
            Files = [new ManualIntakeFile(name, "application/pdf", content)],
        };

    private sealed class FakeManualRepository : IManualIntakeRepository
    {
        public List<ManualIntakeSubmission> Submissions { get; } = [];

        public Task<ManualIntakeSubmission?> FindAsync(
            Guid tenantId,
            Guid submissionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Submissions.SingleOrDefault(item =>
                item.TenantId == tenantId && item.Id == submissionId));

        public Task<ManualIntakeSubmission?> FindByClientRequestIdAsync(
            Guid tenantId,
            string clientRequestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Submissions.SingleOrDefault(item =>
                item.TenantId == tenantId && item.ClientRequestId == clientRequestId));

        public Task<(IReadOnlyList<ManualIntakeSubmission> Items, long TotalCount)> ListAsync(
            Guid tenantId,
            ManualIntakeListQuery query,
            CancellationToken cancellationToken)
        {
            var items = Submissions.Where(item => item.TenantId == tenantId).ToArray();
            return Task.FromResult<(IReadOnlyList<ManualIntakeSubmission>, long)>((items, items.Length));
        }

        public Task<IReadOnlyList<ManualIntakeSubmission>> ListAllAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ManualIntakeSubmission>>(
                Submissions.Where(item => item.TenantId == tenantId).ToArray());

        public Task AddAsync(
            ManualIntakeSubmission submission,
            CancellationToken cancellationToken)
        {
            if (submission.ClientRequestId is not null &&
                Submissions.Any(item => item.TenantId == submission.TenantId &&
                                        item.ClientRequestId == submission.ClientRequestId))
                throw new InvalidOperationException("duplicate");
            Submissions.Add(submission);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeArtifactRepository(FakeManualRepository submissions)
        : IIntakeArtifactRepository
    {
        private readonly List<IntakeArtifact> artifacts = [];

        public Task<IReadOnlyList<IntakeArtifact>> ListByEmailAsync(
            Guid tenantId,
            Guid emailId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>([]);

        public Task<IReadOnlyList<IntakeArtifact>> ListByManualSubmissionAsync(
            Guid tenantId,
            Guid submissionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>(
                artifacts.Where(item => item.TenantId == tenantId &&
                                        item.ManualIntakeSubmissionId == submissionId)
                    .OrderBy(item => item.ArtifactOrdinal)
                    .ToArray());

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
            Task.FromResult<IntakeArtifact?>(null);

        public Task<IReadOnlyList<IntakeArtifact>> ListBySha256Async(
            Guid tenantId,
            string sha256,
            Guid excludedArtifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>(
                artifacts.Where(item => item.TenantId == tenantId &&
                                        item.Id != excludedArtifactId &&
                                        item.Sha256 == sha256).ToArray());

        public Task<IntakeArtifact?> FindByManualKeyAsync(
            Guid tenantId,
            Guid submissionId,
            string artifactKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(artifacts.SingleOrDefault(item =>
                item.TenantId == tenantId &&
                item.ManualIntakeSubmissionId == submissionId &&
                item.ArtifactKey == artifactKey));

        public Task<IntakeArtifact> AddOrGetAsync(
            IntakeArtifact artifact,
            CancellationToken cancellationToken)
        {
            var existing = artifacts.SingleOrDefault(item =>
                item.TenantId == artifact.TenantId &&
                item.ManualIntakeSubmissionId == artifact.ManualIntakeSubmissionId &&
                item.ArtifactKey == artifact.ArtifactKey);
            if (existing is not null)
                return Task.FromResult(existing);
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
            artifact.FailureCode = null;
            artifact.FailureMessage = null;
            return Task.FromResult(true);
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateEmailProcessingStatusAsync(
            Guid tenantId,
            Guid emailId,
            string status,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateManualSubmissionStatusAsync(
            Guid tenantId,
            Guid submissionId,
            string status,
            string? failureMessage,
            DateTimeOffset? completedAt,
            CancellationToken cancellationToken)
        {
            var submission = submissions.Submissions.Single(item =>
                item.TenantId == tenantId && item.Id == submissionId);
            submission.Status = status;
            submission.FailureMessage = failureMessage;
            submission.CompletedAt = completedAt;
            submission.Version++;
            submission.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        public Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(
            Guid tenantId,
            Guid? emailId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDocumentsClient : IIntakeDocumentsClient
    {
        public bool FailNextUpload { get; set; }
        public int UploadCount { get; private set; }
        public Guid LastTenantId { get; private set; }

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
            UploadCount++;
            LastTenantId = tenantId;
            if (FailNextUpload)
            {
                FailNextUpload = false;
                return new(false, null, null, null, "DOCUMENTS_SERVICE_UNAVAILABLE", "temporary", true);
            }
            await content.CopyToAsync(Stream.Null, cancellationToken);
            return new(true, Guid.NewGuid(), Guid.NewGuid(), "documents:test", null, null, false);
        }
    }

    private sealed class FakeConfigurationService : IIntakeConfigurationService
    {
        public Task<ResolvedProcessingConfiguration> ResolveAsync(
            Guid tenantId,
            string? profileCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedProcessingConfiguration(
                tenantId,
                ProcessingProfileCodes.LienIntakeV1,
                1,
                2,
                3,
                new LienIntakeV1Configuration
                {
                    AllowUnsupportedDocuments = false,
                },
                DateTimeOffset.UtcNow));

        public Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(
            Guid tenantId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(
            Guid tenantId,
            UpsertTenantIntakeConfigurationRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> AssignProfileAsync(
            Guid tenantId,
            AssignTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileStatusRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoopAuditSink : IManualIntakeAuditSink
    {
        public Task RecordAsync(
            ManualIntakeAuditEntry entry,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}