using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Services;
using System.Text.Json;
using Xunit;

namespace Notifications.Tests;

/// <summary>
/// Verifies that the <see cref="NotificationServiceImpl.SubmitAsync"/> method
/// propagates the actual <see cref="ProviderFailure.Category"/> from the provider
/// adapter onto <see cref="NotificationResultDto.FailureCategory"/>.
///
/// Task #114, requirement (3):
///   "notification-level FailureCategory reflects actual SendGrid failure category
///    (e.g. auth_config_failure) rather than the generic retryable_provider_failure."
/// </summary>
public class NotificationServiceFailureCategoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NotificationServiceImpl BuildService(IEmailProviderAdapter emailAdapter)
    {
        var notifRepo    = new StubNotificationRepository();
        var attemptRepo  = new StubNotificationAttemptRepository();
        var eventRepo    = new StubNotificationEventRepository();
        var issueRepo    = new StubDeliveryIssueRepository();
        var routing      = new StubProviderRoutingService();
        var contact      = new StubContactEnforcementService();
        var usage        = new StubUsageEvaluationService();
        var metering     = new StubUsageMeteringService();
        var templateRes  = new StubTemplateResolutionService();
        var templateRend = new StubTemplateRenderingService();
        var branding     = new StubBrandingResolutionService();
        var smsAdapter   = new StubSmsProviderAdapter();
        var recipient    = new StubRecipientResolver();
        var audit        = new StubAuditEventClient();
        var logger       = NullLogger<NotificationServiceImpl>.Instance;

        return new NotificationServiceImpl(
            notifRepo, attemptRepo, eventRepo, issueRepo,
            routing, contact, usage, metering,
            templateRes, templateRend, branding,
            emailAdapter, smsAdapter,
            recipient, audit, logger);
    }

    private static SubmitNotificationDto EmailRequest(string toEmail = "to@example.com") => new()
    {
        Channel   = "email",
        Recipient = new { email = toEmail },
        Message   = new { subject = "Test subject", body = "Hello" },
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_FailureCategory_IsAuthConfigFailure_WhenAdapterReturnsAuthConfigFailure()
    {
        // Arrange: email adapter returns non-retryable auth_config_failure
        var adapter = new StubEmailProviderAdapter(new EmailSendResult
        {
            Success = false,
            Failure = new ProviderFailure
            {
                Category  = "auth_config_failure",
                Message   = "Invalid SendGrid API key",
                Retryable = false,
            },
        });

        var svc    = BuildService(adapter);
        var result = await svc.SubmitAsync(TenantId, EmailRequest());

        Assert.Equal("auth_config_failure", result.FailureCategory);
        Assert.Equal("failed", result.Status);
    }

    [Fact]
    public async Task SubmitAsync_FailureCategory_IsInvalidRecipient_WhenAdapterReturnsInvalidRecipient()
    {
        var adapter = new StubEmailProviderAdapter(new EmailSendResult
        {
            Success = false,
            Failure = new ProviderFailure
            {
                Category  = "invalid_recipient",
                Message   = "The recipient email address is invalid",
                Retryable = false,
            },
        });

        var svc    = BuildService(adapter);
        var result = await svc.SubmitAsync(TenantId, EmailRequest("bad@"));

        Assert.Equal("invalid_recipient", result.FailureCategory);
        Assert.Equal("failed", result.Status);
    }

    [Fact]
    public async Task SubmitAsync_FailureCategory_IsRetryableProviderFailure_WhenAdapterReturnsRetryableError()
    {
        // Arrange: adapter returns retryable failure (e.g. 429 Too Many Requests)
        var adapter = new StubEmailProviderAdapter(new EmailSendResult
        {
            Success = false,
            Failure = new ProviderFailure
            {
                Category  = "retryable_provider_failure",
                Message   = "Too many requests",
                Retryable = true,
            },
        });

        var svc    = BuildService(adapter);
        var result = await svc.SubmitAsync(TenantId, EmailRequest());

        // After exhausting all routes with only retryable failures, the service
        // schedules a retry and uses the generic "retryable_provider_failure" category.
        Assert.Equal("retryable_provider_failure", result.FailureCategory);
        Assert.Equal("retrying", result.Status);
    }

    [Fact]
    public async Task SubmitAsync_FailureCategory_IsAuthConfigFailure_WhenNoProviderRoutesConfigured()
    {
        // With an empty route list (routing service returns nothing) the service
        // sets FailureCategory = "auth_config_failure" and status = "failed".
        var adapter = new StubEmailProviderAdapter(new EmailSendResult { Success = true });
        var svc     = BuildService(adapter); // adapter won't even be called

        // Swap in a no-routes routing service on the same instance by building
        // a fresh service with the no-routes stub.
        var noRouteSvc = new NotificationServiceImpl(
            new StubNotificationRepository(),
            new StubNotificationAttemptRepository(),
            new StubNotificationEventRepository(),
            new StubDeliveryIssueRepository(),
            new NoRoutesProviderRoutingService(),
            new StubContactEnforcementService(),
            new StubUsageEvaluationService(),
            new StubUsageMeteringService(),
            new StubTemplateResolutionService(),
            new StubTemplateRenderingService(),
            new StubBrandingResolutionService(),
            adapter,
            new StubSmsProviderAdapter(),
            new StubRecipientResolver(),
            new StubAuditEventClient(),
            NullLogger<NotificationServiceImpl>.Instance);

        var result = await noRouteSvc.SubmitAsync(TenantId, EmailRequest());

        Assert.Equal("auth_config_failure", result.FailureCategory);
        Assert.Equal("failed", result.Status);
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubEmailProviderAdapter : IEmailProviderAdapter
    {
        private readonly EmailSendResult _result;
        public string ProviderType => "sendgrid";
        public StubEmailProviderAdapter(EmailSendResult result) => _result = result;
        public Task<bool>               ValidateConfigAsync() => Task.FromResult(true);
        public Task<EmailSendResult>    SendAsync(EmailSendPayload payload) => Task.FromResult(_result);
        public Task<ProviderHealthResult> HealthCheckAsync() => Task.FromResult(new ProviderHealthResult { Status = "healthy" });
    }

    private sealed class StubSmsProviderAdapter : ISmsProviderAdapter
    {
        public string ProviderType => "twilio";
        public Task<bool>              ValidateConfigAsync() => Task.FromResult(true);
        public Task<SmsSendResult>     SendAsync(SmsSendPayload payload) => Task.FromResult(new SmsSendResult { Success = true });
        public Task<ProviderHealthResult> HealthCheckAsync() => Task.FromResult(new ProviderHealthResult { Status = "healthy" });
    }

    private sealed class StubNotificationRepository : INotificationRepository
    {
        public Task<Notification?> GetByIdAsync(Guid id)
            => Task.FromResult<Notification?>(null);

        public Task<Notification?> GetByIdAndTenantAsync(Guid id, Guid tenantId)
            => Task.FromResult<Notification?>(null);

        public Task<Notification?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey)
            => Task.FromResult<Notification?>(null);

        public Task<List<Notification>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
            => Task.FromResult(new List<Notification>());

        public Task<Notification> CreateAsync(Notification notification)
        {
            if (notification.Id == Guid.Empty)
                notification.Id = Guid.NewGuid();
            return Task.FromResult(notification);
        }

        public Task UpdateAsync(Notification notification) => Task.CompletedTask;

        public Task UpdateStatusAsync(Guid id, string status, string? providerUsed = null,
            string? failureCategory = null, string? lastErrorMessage = null)
            => Task.CompletedTask;

        public Task<(List<Notification> Items, int Total)> GetPagedAsync(Guid tenantId, NotificationListQuery query)
            => Task.FromResult((new List<Notification>(), 0));

        public Task<NotificationStatsData> GetStatsAsync(Guid tenantId, NotificationStatsQuery query)
            => Task.FromResult(new NotificationStatsData());

        public Task<(List<Notification> Items, int Total)> GetPagedAdminAsync(Guid? tenantId, NotificationListQuery query)
            => Task.FromResult((new List<Notification>(), 0));

        public Task<NotificationStatsData> GetStatsAdminAsync(Guid? tenantId, NotificationStatsQuery query)
            => Task.FromResult(new NotificationStatsData());

        public Task<List<Notification>> GetEligibleForRetryAsync(int batchSize = 10)
            => Task.FromResult(new List<Notification>());

        public Task<List<Notification>> GetStalledProcessingAsync(TimeSpan threshold, int batchSize = 20)
            => Task.FromResult(new List<Notification>());
    }

    private sealed class StubNotificationAttemptRepository : INotificationAttemptRepository
    {
        public Task<NotificationAttempt?> GetByIdAsync(Guid id)
            => Task.FromResult<NotificationAttempt?>(null);

        public Task<NotificationAttempt?> FindByProviderMessageIdAsync(string providerMessageId)
            => Task.FromResult<NotificationAttempt?>(null);

        public Task<List<NotificationAttempt>> GetByNotificationIdAsync(Guid notificationId)
            => Task.FromResult(new List<NotificationAttempt>());

        public Task<NotificationAttempt> CreateAsync(NotificationAttempt attempt)
        {
            if (attempt.Id == Guid.Empty)
                attempt.Id = Guid.NewGuid();
            return Task.FromResult(attempt);
        }

        public Task UpdateAsync(NotificationAttempt attempt) => Task.CompletedTask;

        public Task UpdateStatusAsync(Guid id, string status, DateTime? completedAt = null)
            => Task.CompletedTask;
    }

    private sealed class StubNotificationEventRepository : INotificationEventRepository
    {
        public Task<NotificationEvent?> FindByDedupKeyAsync(string dedupKey)
            => Task.FromResult<NotificationEvent?>(null);

        public Task<NotificationEvent> CreateAsync(NotificationEvent evt)
        {
            if (evt.Id == Guid.Empty) evt.Id = Guid.NewGuid();
            return Task.FromResult(evt);
        }

        public Task<List<NotificationEvent>> GetByNotificationIdAsync(Guid notificationId, int limit = 50)
            => Task.FromResult(new List<NotificationEvent>());
    }

    private sealed class StubDeliveryIssueRepository : IDeliveryIssueRepository
    {
        public Task<DeliveryIssue?> CreateIfNotExistsAsync(DeliveryIssue issue)
            => Task.FromResult<DeliveryIssue?>(issue);

        public Task<List<DeliveryIssue>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
            => Task.FromResult(new List<DeliveryIssue>());

        public Task<List<DeliveryIssue>> GetByNotificationIdAsync(Guid notificationId)
            => Task.FromResult(new List<DeliveryIssue>());
    }

    private sealed class StubProviderRoutingService : IProviderRoutingService
    {
        public Task<List<ProviderRoute>> ResolveRoutesAsync(Guid tenantId, string channel)
            => Task.FromResult(new List<ProviderRoute>
            {
                new() { ProviderType = "sendgrid", OwnershipMode = "platform" },
            });
    }

    private sealed class NoRoutesProviderRoutingService : IProviderRoutingService
    {
        public Task<List<ProviderRoute>> ResolveRoutesAsync(Guid tenantId, string channel)
            => Task.FromResult(new List<ProviderRoute>());
    }

    private sealed class StubContactEnforcementService : IContactEnforcementService
    {
        public Task<ContactEnforcementResult> EvaluateAsync(ContactEnforcementInput input)
            => Task.FromResult(new ContactEnforcementResult { Allowed = true });
    }

    private sealed class StubUsageEvaluationService : IUsageEvaluationService
    {
        public Task<EnforcementDecision> CheckRequestAllowedAsync(Guid tenantId, string channel)
            => Task.FromResult(new EnforcementDecision { Allowed = true });

        public Task<EnforcementDecision> CheckAttemptAllowedAsync(Guid tenantId, string channel)
            => Task.FromResult(new EnforcementDecision { Allowed = true });
    }

    private sealed class StubUsageMeteringService : IUsageMeteringService
    {
        public Task MeterAsync(MeterEventInput input)     => Task.CompletedTask;
        public Task MeterBatchAsync(IEnumerable<MeterEventInput> inputs) => Task.CompletedTask;
    }

    private sealed class StubTemplateResolutionService : ITemplateResolutionService
    {
        public Task<ResolvedTemplate?> ResolveAsync(Guid tenantId, string templateKey, string channel)
            => Task.FromResult<ResolvedTemplate?>(null);

        public Task<ResolvedTemplate?> ResolveByProductAsync(Guid tenantId, string templateKey, string channel, string productType)
            => Task.FromResult<ResolvedTemplate?>(null);
    }

    private sealed class StubTemplateRenderingService : ITemplateRenderingService
    {
        public RenderResult Render(string? subjectTemplate, string bodyTemplate, string? textTemplate, Dictionary<string, string> data)
            => new() { Subject = "", Body = "", Text = null };

        public RenderResult RenderBranded(string? subjectTemplate, string bodyTemplate, string? textTemplate, Dictionary<string, string> data, Dictionary<string, string> brandingTokens)
            => new() { Subject = "", Body = "", Text = null };
    }

    private sealed class StubBrandingResolutionService : IBrandingResolutionService
    {
        public Task<ResolvedBranding> ResolveAsync(Guid tenantId, string productType)
            => Task.FromResult(new ResolvedBranding());

        public ResolvedBranding GetDefault(string productType) => new();

        public Dictionary<string, string> BuildBrandingTokens(ResolvedBranding branding)
            => new();
    }

    private sealed class StubRecipientResolver : IRecipientResolver
    {
        public Task<IReadOnlyList<ResolvedRecipient>> ResolveAsync(Guid tenantId, JsonElement recipient)
            => Task.FromResult<IReadOnlyList<ResolvedRecipient>>(Array.Empty<ResolvedRecipient>());
    }

    private sealed class StubAuditEventClient : IAuditEventClient
    {
        public Task<IngestResult> IngestAsync(IngestAuditEventRequest request, CancellationToken ct = default)
            => Task.FromResult(new IngestResult(true, Guid.NewGuid().ToString(), null, 202));

        public Task<BatchIngestResult> IngestBatchAsync(BatchIngestRequest request, CancellationToken ct = default)
            => Task.FromResult(new BatchIngestResult(0, 0, 0, Array.Empty<IngestResult>()));
    }
}
