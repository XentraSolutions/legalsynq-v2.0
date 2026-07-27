using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Api.Tests.Helpers;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;

namespace Liens.Api.Tests;

/// <summary>
/// Single WebApplicationFactory shared across all legacy API test classes.
/// Replaces MySQL with an InMemory database and stubs out external HTTP calls.
/// </summary>
public sealed class LiensApiFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"liens-tests-{Guid.CreateVersion7()}";

    static LiensApiFactory()
    {
        // These must be set BEFORE the host builds because Program.cs reads them
        // via builder.Configuration during service registration.
        Environment.SetEnvironmentVariable("Jwt__Issuer",     JwtTokenHelper.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience",   JwtTokenHelper.Audience);
        Environment.SetEnvironmentVariable("Jwt__SigningKey",  JwtTokenHelper.SigningKey);

        // Dummy DB connection string — replaced with InMemory in ConfigureServices.
        Environment.SetEnvironmentVariable("ConnectionStrings__LiensDb",
            "Server=localhost;Database=liens_test;Uid=test;Pwd=test;");

        // Dummy URLs for external HTTP clients — not called in tests.
        Environment.SetEnvironmentVariable("Flow__BaseUrl",              "http://localhost:19999/");
        Environment.SetEnvironmentVariable("AuditClient__BaseUrl",       "http://localhost:19998/");
        Environment.SetEnvironmentVariable("Services__NotificationsUrl", "http://localhost:19997/");
        Environment.SetEnvironmentVariable("Services__TaskServiceUrl",   "http://localhost:19996/");
        Environment.SetEnvironmentVariable("Services__DocumentsUrl",     "http://localhost:19995/");
        Environment.SetEnvironmentVariable("Services__CommerceUrl",      "http://localhost:19994/");
        Environment.SetEnvironmentVariable("Liens__Selling__BuyerPortalBaseUrl",
            "https://app.legalsynq.test/selling/public");

        // Service token issuer requires a signing key.
        Environment.SetEnvironmentVariable("ServiceTokens__liens-service__SigningKey",
            JwtTokenHelper.SigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace MySQL DbContext with InMemory.
            // Remove every descriptor whose ServiceType references LiensDbContext
            // (includes DbContext, DbContextOptions<T>, IDbContextOptionsConfiguration<T>).
            var toRemove = services
                .Where(d => d.ServiceType.FullName != null
                    && (d.ServiceType.FullName.Contains("LiensDbContext")
                        || (d.ServiceType.IsGenericType
                            && d.ServiceType.GetGenericArguments()
                               .Any(a => a.FullName != null
                                    && a.FullName.Contains("LiensDbContext")))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);
            services.AddDbContext<LiensDbContext>(o => o
                .UseInMemoryDatabase(DbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Stub out IFlowInstanceResolver so no Flow HTTP calls happen.
            services.RemoveAll<IFlowInstanceResolver>();
            services.AddScoped<IFlowInstanceResolver, NoOpFlowInstanceResolver>();

            services.RemoveAll<INotificationPublisher>();
            services.AddSingleton<CapturingNotificationPublisher>();
            services.AddSingleton<INotificationPublisher>(sp => sp.GetRequiredService<CapturingNotificationPublisher>());

            services.RemoveAll<IAuditPublisher>();
            services.AddSingleton<CapturingAuditPublisher>();
            services.AddSingleton<IAuditPublisher>(sp => sp.GetRequiredService<CapturingAuditPublisher>());

            services.RemoveAll<ILegacyDocumentUploadClient>();
            services.AddSingleton<CapturingLegacyDocumentUploadClient>();
            services.AddSingleton<ILegacyDocumentUploadClient>(sp => sp.GetRequiredService<CapturingLegacyDocumentUploadClient>());

            services.RemoveAll<IPublicBuyerAccountProvisioningService>();
            services.AddSingleton<CapturingPublicBuyerAccountProvisioningService>();
            services.AddSingleton<IPublicBuyerAccountProvisioningService>(
                sp => sp.GetRequiredService<CapturingPublicBuyerAccountProvisioningService>());

            services.AddHttpClient("MedicareProcedureLookup")
                .ConfigurePrimaryHttpMessageHandler(() => new StubMedicareProcedureLookupHandler());
            services.AddHttpClient("Identity")
                .ConfigurePrimaryHttpMessageHandler(() => new StubIdentityHandler());
        });
    }
}

internal sealed class StubMedicareProcedureLookupHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryGetValues("apiKey", out var apiKeyValues).Should().BeTrue();
        apiKeyValues!.Should().Contain("1iuNYl3IYBHTSjmn34m0XOLLqfm1nrmz");

        request.Headers.TryGetValues("amaLicense", out var licenseValues).Should().BeTrue();
        licenseValues!.Should().Contain("b733fd32-ee85-4174-9ab1-e09ec14048bb");

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var response = true switch
        {
            _ when path.EndsWith("/codes", StringComparison.OrdinalIgnoreCase) => JsonResponse("""
                [
                  { "code": "45385", "description": "Colonoscopy, flexible; with removal by snare technique (45385)", "frequency": 1075901 }
                ]
                """),
            _ when path.EndsWith("/costs/45385", StringComparison.OrdinalIgnoreCase) => JsonResponse("""
                [
                  { "code": "45385", "facilityType": "hospital", "cost": 1156, "copay": 288, "facilityTotal": 1222, "physicianTotal": 223, "total": 1445 },
                  { "code": "45385", "facilityType": "asc", "cost": 703, "copay": 175, "facilityTotal": 656, "physicianTotal": 223, "total": 879 }
                ]
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        return Task.FromResult(response);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}

internal sealed class StubIdentityHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.RequestUri?.AbsolutePath.Should().Be("/api/users");
        request.Headers.Authorization.Should().NotBeNull();

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""[{"id":"{{SeedHelper.UserId}}","firstName":"Demo","lastName":"User","email":"demo@example.com"}]""",
                System.Text.Encoding.UTF8,
                "application/json"),
        });
    }
}

/// <summary>No-op stub — returns (null, null) for every case lookup.</summary>
internal sealed class NoOpFlowInstanceResolver : IFlowInstanceResolver
{
    public Task<(Guid? WorkflowInstanceId, string? WorkflowStepKey)> ResolveAsync(
        Guid caseId, CancellationToken ct = default)
        => Task.FromResult<(Guid?, string?)>((null, null));
}

internal sealed class CapturingNotificationPublisher : INotificationPublisher
{
    private readonly List<CapturedEmail> _emails = [];
    private readonly Dictionary<string, Guid> _idempotentEmails = new(StringComparer.Ordinal);

    public IReadOnlyList<CapturedEmail> Emails => _emails;

    public void Clear()
    {
        _emails.Clear();
        _idempotentEmails.Clear();
        FailEmailSends = false;
    }

    public bool FailEmailSends { get; set; }

    public Task PublishAsync(
        string notificationType,
        Guid tenantId,
        Dictionary<string, string> data,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<NotificationEmailSendResult> SendEmailAsync(
        string notificationType,
        Guid tenantId,
        string recipientEmail,
        string subject,
        string body,
        Dictionary<string, string> metadata,
        CancellationToken ct = default,
        NotificationEmailSendOptions? options = null)
    {
        if (FailEmailSends)
        {
            return Task.FromResult(new NotificationEmailSendResult(
                null,
                "failed",
                false,
                null,
                "transient",
                "Simulated notification failure."));
        }

        var idempotencyKey = options?.IdempotencyKey;
        if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
            _idempotentEmails.TryGetValue($"{tenantId:N}:{idempotencyKey}", out var existingNotificationId))
        {
            return Task.FromResult(new NotificationEmailSendResult(
                existingNotificationId,
                "sent",
                false,
                null,
                null,
                null));
        }

        var notificationId = Guid.CreateVersion7();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            _idempotentEmails[$"{tenantId:N}:{idempotencyKey}"] = notificationId;

        _emails.Add(new CapturedEmail(
            notificationType,
            tenantId,
            recipientEmail,
            subject,
            body,
            metadata,
            options,
            notificationId));

        return Task.FromResult(new NotificationEmailSendResult(
            notificationId,
            "sent",
            false,
            null,
            null,
            null));
    }
}

internal sealed record CapturedEmail(
    string NotificationType,
    Guid TenantId,
    string RecipientEmail,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string> Metadata,
    NotificationEmailSendOptions? Options,
    Guid NotificationId);

internal sealed class CapturingPublicBuyerAccountProvisioningService : IPublicBuyerAccountProvisioningService
{
    private readonly List<PublicBuyerAccountProvisioningRequest> _requests = [];

    public IReadOnlyList<PublicBuyerAccountProvisioningRequest> Requests => _requests;
    public PublicBuyerAccountProvisioningResult? NextResult { get; set; }

    public void Clear()
    {
        _requests.Clear();
        NextResult = null;
    }

    public Task<PublicBuyerAccountProvisioningResult> ProvisionBuyerAccountAsync(
        PublicBuyerAccountProvisioningRequest request,
        CancellationToken ct = default)
    {
        _requests.Add(request);
        return Task.FromResult(
            NextResult
            ?? PublicBuyerAccountProvisioningResult.Created(Guid.CreateVersion7(), isNew: true));
    }
}

internal sealed class CapturingAuditPublisher : IAuditPublisher
{
    private readonly List<CapturedAuditEvent> _events = [];

    public IReadOnlyList<CapturedAuditEvent> Events => _events;

    public void Clear() => _events.Clear();

    public void Publish(
        string eventType,
        string action,
        string description,
        Guid tenantId,
        Guid? actorUserId = null,
        string? entityType = null,
        string? entityId = null,
        string? before = null,
        string? after = null,
        string? metadata = null)
    {
        _events.Add(new CapturedAuditEvent(
            eventType,
            action,
            description,
            tenantId,
            actorUserId,
            entityType,
            entityId,
            before,
            after,
            metadata,
            DateTimeOffset.UtcNow));
    }
}

internal sealed record CapturedAuditEvent(
    string EventType,
    string Action,
    string Description,
    Guid TenantId,
    Guid? ActorUserId,
    string? EntityType,
    string? EntityId,
    string? Before,
    string? After,
    string? Metadata,
    DateTimeOffset OccurredAtUtc);

internal sealed class CapturingLegacyDocumentUploadClient : ILegacyDocumentUploadClient
{
    private readonly List<CapturedLegacyDocumentUpload> _uploads = [];

    public IReadOnlyList<CapturedLegacyDocumentUpload> Uploads => _uploads;

    public void Clear() => _uploads.Clear();

    public Task<LegacyDocumentUploadResult> UploadAsync(
        LegacyDocumentUploadRequest request,
        CancellationToken ct = default)
    {
        var documentId = Guid.CreateVersion7();
        _uploads.Add(new CapturedLegacyDocumentUpload(
            request.TenantId,
            request.ActingUserId,
            request.ReferenceId,
            request.ReferenceType,
            request.DocumentTypeId,
            request.Title,
            request.FileName,
            request.ContentType,
            request.Length,
            documentId));

        return Task.FromResult(new LegacyDocumentUploadResult
        {
            DocumentId = documentId,
            Url = $"/documents/{documentId}",
        });
    }
}

internal sealed record CapturedLegacyDocumentUpload(
    Guid TenantId,
    Guid ActingUserId,
    Guid ReferenceId,
    string ReferenceType,
    Guid DocumentTypeId,
    string Title,
    string FileName,
    string ContentType,
    long Length,
    Guid DocumentId);
