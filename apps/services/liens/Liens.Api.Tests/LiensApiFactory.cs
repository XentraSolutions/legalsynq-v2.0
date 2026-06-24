using Liens.Application.Interfaces;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            services.AddDbContext<LiensDbContext>(o => o.UseInMemoryDatabase(DbName));

            // Stub out IFlowInstanceResolver so no Flow HTTP calls happen.
            services.RemoveAll<IFlowInstanceResolver>();
            services.AddScoped<IFlowInstanceResolver, NoOpFlowInstanceResolver>();

            services.RemoveAll<INotificationPublisher>();
            services.AddSingleton<CapturingNotificationPublisher>();
            services.AddSingleton<INotificationPublisher>(sp => sp.GetRequiredService<CapturingNotificationPublisher>());

            services.RemoveAll<IAuditPublisher>();
            services.AddSingleton<CapturingAuditPublisher>();
            services.AddSingleton<IAuditPublisher>(sp => sp.GetRequiredService<CapturingAuditPublisher>());
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

    public IReadOnlyList<CapturedEmail> Emails => _emails;

    public void Clear() => _emails.Clear();

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
        CancellationToken ct = default)
    {
        var notificationId = Guid.CreateVersion7();
        _emails.Add(new CapturedEmail(
            notificationType,
            tenantId,
            recipientEmail,
            subject,
            body,
            metadata,
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
    Guid NotificationId);

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
