using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Application.Emails;
using Intake.Application.Sources;
using Intake.Contracts.Configuration;
using Intake.Contracts.Emails;
using Intake.Contracts.Sources;
using Intake.Domain.Configuration;
using Intake.Domain.Emails;
using Intake.Domain.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class EmailCaptureTests
{
    [Fact]
    public async Task Capture_persists_provenance_content_recipients_headers_and_attachments()
    {
        var fixture = CreateFixture();
        var command = NewCommand();
        command.FromAddress = "sender@example.com";
        command.FromDisplayName = "Élodie Sender";
        command.ReplyToAddress = "reply@example.com";
        command.TextBody = "Unicode body: café";
        command.HtmlBody = "<p>Untrusted <strong>HTML</strong></p>";
        command.Headers =
        [
            new() { Name = "References", Values = ["<one@example.com>", "<two@example.com>"] },
            new() { Name = "Authorization", Values = ["Bearer should-not-persist"] },
            new() { Name = "Cookie", Values = ["session=should-not-persist"] },
            new() { Name = "X-Session-Token", Values = ["should-not-persist"] },
        ];
        command.Recipients =
        [
            new()
            {
                RecipientType = "TO",
                EmailAddress = "Intake@EXAMPLE.COM",
                DisplayName = "Réception",
            },
            new()
            {
                RecipientType = "CC",
                EmailAddress = "copy@example.com",
            },
            new()
            {
                RecipientType = "BCC",
                EmailAddress = "blind@example.com",
            },
        ];
        command.Attachments =
        [
            new()
            {
                ProviderAttachmentId = "att-1",
                FileName = "notice.pdf",
                ContentType = "application/pdf",
                ContentDisposition = "attachment",
                SizeBytes = 12,
                Sha256 = new string('a', 64),
            },
        ];
        command.RawMessage = "raw MIME source";

        var result = await fixture.Service.CaptureAsync(
            command,
            "capture-1",
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal(fixture.Source.TenantId, result.Email.TenantId);
        Assert.Equal(fixture.Source.Id, result.Email.TenantIntakeSourceId);
        Assert.Equal(7, result.Email.SourceConfigurationVersion);
        Assert.Equal(IntakeSourcePurposes.LienIntake, result.Email.Purpose);
        Assert.Equal(ProcessingProfileCodes.LienIntakeV1, result.Email.ProcessingProfileCode);
        Assert.Equal(12, result.Email.TenantConfigurationVersion);
        Assert.Equal(13, result.Email.TenantProfileConfigurationVersion);
        Assert.Equal("GENERIC", result.Email.Provider);
        Assert.Equal("provider-message-1", result.Email.ProviderMessageId);
        Assert.Equal("<internet-1@example.com>", result.Email.InternetMessageId);
        Assert.Equal(InboundEmailCaptureStatuses.Captured, result.Email.CaptureStatus);
        Assert.Equal(InboundEmailProcessingStatuses.NotStarted, result.Email.ProcessingStatus);
        Assert.Equal("café", result.Email.TextBody!["Unicode body: ".Length..]);
        Assert.Equal(3, result.Email.Recipients.Count);
        Assert.Equal("Intake@example.com", result.Email.Recipients[0].NormalizedEmailAddress);
        Assert.Equal("Réception", result.Email.Recipients[0].DisplayName);
        Assert.Single(result.Email.Attachments);
        Assert.True(result.Email.HasRawMessage);
        Assert.DoesNotContain("Authorization", result.Email.HeadersJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", result.Email.HeadersJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("should-not-persist", result.Email.HeadersJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("References", result.Email.HeadersJson, StringComparison.Ordinal);
        Assert.Single(fixture.Audit.Entries);
        Assert.Equal("EMAIL_CAPTURED", fixture.Audit.Entries[0].Operation);
    }

    [Fact]
    public async Task Duplicate_provider_delivery_returns_canonical_record_and_increments_duplicate_analytics()
    {
        var fixture = CreateFixture();
        var first = await fixture.Service.CaptureAsync(
            NewCommand(),
            "capture-1",
            CancellationToken.None);

        var duplicateCommand = NewCommand();
        duplicateCommand.Subject = "Changed duplicate subject";
        var duplicate = await fixture.Service.CaptureAsync(
            duplicateCommand,
            "capture-2",
            CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(first.Email.Id, duplicate.Email.Id);
        Assert.Equal("Initial subject", duplicate.Email.Subject);
        Assert.Equal(1, duplicate.Email.DuplicateCaptureCount);
        Assert.Single(fixture.Repository.Emails);
        Assert.Equal(
            ["EMAIL_CAPTURED", "EMAIL_CAPTURE_DUPLICATE"],
            fixture.Audit.Entries.Select(entry => entry.Operation));
    }

    [Fact]
    public async Task Capture_rejects_tenant_spoofing_and_inactive_sources()
    {
        var fixture = CreateFixture();
        var spoofed = NewCommand();
        spoofed.TenantId = Guid.NewGuid();

        var spoofException = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.CaptureAsync(spoofed, null, CancellationToken.None));
        Assert.Equal("INTAKE_SOURCE_NOT_FOUND", spoofException.Code);

        fixture.Source.IsActive = false;
        var inactiveException = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.CaptureAsync(NewCommand(), null, CancellationToken.None));
        Assert.Equal("INTAKE_SOURCE_INACTIVE", inactiveException.Code);
    }

    [Fact]
    public async Task Capture_rejects_missing_identity_and_oversized_body()
    {
        var fixture = CreateFixture(new EmailCaptureOptions
        {
            MaxTextBodyBytes = 3,
        });

        var identityMissing = NewCommand();
        identityMissing.ProviderMessageId = null;
        identityMissing.InternetMessageId = null;
        var identityException = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.CaptureAsync(identityMissing, null, CancellationToken.None));
        Assert.Equal("EMAIL_IDENTITY_REQUIRED", identityException.Code);

        var oversized = NewCommand();
        oversized.TextBody = "éé";
        var sizeException = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.CaptureAsync(oversized, null, CancellationToken.None));
        Assert.Equal("EMAIL_CAPTURE_SIZE_LIMIT_EXCEEDED", sizeException.Code);
        Assert.Equal(
            ["EMAIL_IDENTITY_REQUIRED", "EMAIL_CAPTURE_SIZE_LIMIT_EXCEEDED"],
            fixture.Repository.Failures.Select(failure => failure.FailureCode));
    }

    private static CaptureInboundEmailCommand NewCommand() =>
        new()
        {
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            SourceId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
            SourceConfigurationVersion = 7,
            Purpose = IntakeSourcePurposes.LienIntake,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            Provider = IntakeSourceProviders.Generic,
            ProviderMessageId = "provider-message-1",
            ProviderThreadId = "thread-1",
            InternetMessageId = "<internet-1@example.com>",
            InReplyToMessageId = "<previous@example.com>",
            References = ["<previous@example.com>"],
            ReceivedAt = new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.Zero),
            ProviderCreatedAt = new DateTimeOffset(2026, 8, 14, 9, 59, 0, TimeSpan.Zero),
            Subject = "Initial subject",
            TextBody = "body",
            Recipients =
            [
                new()
                {
                    RecipientType = InboundEmailRecipientTypes.To,
                    EmailAddress = "intake@example.com",
                },
            ],
        };

    private static Fixture CreateFixture(EmailCaptureOptions? options = null)
    {
        var source = new TenantIntakeSource
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000007"),
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            SourceType = IntakeSourceTypes.Email,
            EmailAddress = "intake@example.com",
            NormalizedEmailAddress = "intake@example.com",
            Provider = IntakeSourceProviders.Generic,
            Purpose = IntakeSourcePurposes.LienIntake,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            IsActive = true,
            ConfigurationVersion = 7,
        };
        var sourceRepository = new FakeSourceRepository(source);
        var config = new FakeConfigurationService();
        var resolver = new FakeSourceResolver(source);
        var repository = new FakeInboundEmailRepository();
        var audit = new RecordingAuditSink();
        var service = new EmailCaptureService(
            sourceRepository,
            resolver,
            config,
            repository,
            audit,
            options ?? new EmailCaptureOptions(),
            NullLogger<EmailCaptureService>.Instance);
        return new(source, service, repository, audit);
    }

    private sealed record Fixture(
        TenantIntakeSource Source,
        IEmailCaptureService Service,
        FakeInboundEmailRepository Repository,
        RecordingAuditSink Audit);

    private sealed class RecordingAuditSink : IIntakeConfigurationAuditSink
    {
        public List<ConfigurationAuditEntry> Entries { get; } = [];

        public Task RecordAsync(
            ConfigurationAuditEntry entry,
            CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSourceRepository(TenantIntakeSource source)
        : IIntakeSourceRepository
    {
        public Task<IReadOnlyList<TenantIntakeSource>> ListTenantSourcesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantIntakeSource>>(
                source.TenantId == tenantId ? [source] : []);

        public Task<TenantIntakeSource?> FindTenantSourceAsync(
            Guid tenantId,
            Guid sourceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantIntakeSource?>(
                source.TenantId == tenantId && source.Id == sourceId ? source : null);

        public Task<TenantIntakeSource?> FindByNormalizedEmailAddressAsync(
            string normalizedEmailAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantIntakeSource?>(
                source.NormalizedEmailAddress == normalizedEmailAddress ? source : null);

        public Task<IReadOnlyList<TenantIntakeSource>> ListTenantPurposeSourcesAsync(
            Guid tenantId,
            string purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantIntakeSource>>(
                source.TenantId == tenantId && source.Purpose == purpose ? [source] : []);

        public void Add(TenantIntakeSource source) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken) =>
            operation();
    }

    private sealed class FakeSourceResolver(TenantIntakeSource source)
        : IIntakeSourceResolver
    {
        public Task<ResolvedIntakeSource> ResolveByEmailAddressAsync(
            string emailAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedIntakeSource(
                source.Id,
                source.TenantId,
                source.OrgId,
                source.SourceType,
                source.EmailAddress,
                source.NormalizedEmailAddress,
                source.Purpose,
                source.Provider,
                source.ProcessingProfileCode,
                source.ConfigurationVersion,
                DateTimeOffset.UtcNow));
    }

    private sealed class FakeInboundEmailRepository : IInboundEmailRepository
    {
        public List<InboundEmail> Emails { get; } = [];

        public Task<InboundEmailPersistenceResult> PersistCaptureAsync(
            InboundEmail email,
            IReadOnlyList<InboundEmailRecipient> recipients,
            IReadOnlyList<InboundEmailAttachmentMetadata> attachments,
            CancellationToken cancellationToken)
        {
            var existing = Emails.SingleOrDefault(item =>
                item.IdempotencyKey == email.IdempotencyKey);
            if (existing is not null)
            {
                existing.DuplicateCaptureCount++;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                return Task.FromResult(new InboundEmailPersistenceResult(existing.Id, true));
            }

            email.Recipients = recipients.ToList();
            email.AttachmentMetadata = attachments.ToList();
            Emails.Add(email);
            return Task.FromResult(new InboundEmailPersistenceResult(email.Id, false));
        }

        public Task<InboundEmail?> FindTenantEmailAsync(
            Guid tenantId,
            Guid emailId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Emails.SingleOrDefault(email =>
                    email.TenantId == tenantId && email.Id == emailId));

        public Task<InboundEmail?> FindByProviderIdentityAsync(
            Guid tenantId,
            Guid sourceId,
            string provider,
            string providerMessageId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Emails.SingleOrDefault(email =>
                email.TenantId == tenantId &&
                email.TenantIntakeSourceId == sourceId &&
                email.Provider == provider &&
                email.ProviderMessageId == providerMessageId));

        public Task<InboundEmail?> FindByInternetMessageIdAsync(
            Guid tenantId,
            Guid sourceId,
            string internetMessageId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Emails.SingleOrDefault(email =>
                email.TenantId == tenantId &&
                email.TenantIntakeSourceId == sourceId &&
                email.InternetMessageId == internetMessageId));

        public Task RecordCaptureFailureAsync(
            InboundEmailCaptureFailure failure,
            CancellationToken cancellationToken)
        {
            Failures.Add(failure);
            return Task.CompletedTask;
        }

        public List<InboundEmailCaptureFailure> Failures { get; } = [];

        public Task<PagedInboundEmailResponse> ListAsync(
            Guid tenantId,
            InboundEmailListQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedInboundEmailResponse([], 1, 50, 0, 0));

        public Task<InboundEmailAnalyticsResponse> GetAnalyticsAsync(
            Guid tenantId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken) =>
            Task.FromResult(new InboundEmailAnalyticsResponse(
                0,
                [],
                [],
                [],
                [],
                [],
                0,
                0,
                0,
                0));
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
                3,
                12,
                13,
                new LienIntakeV1Configuration(),
                DateTimeOffset.UtcNow));

        public Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(
            Guid tenantId,
            UpsertTenantIntakeConfigurationRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantProcessingProfileResponse> AssignProfileAsync(
            Guid tenantId,
            AssignTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileStatusRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}