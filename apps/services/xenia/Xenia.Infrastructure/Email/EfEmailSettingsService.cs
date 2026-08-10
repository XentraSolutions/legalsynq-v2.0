using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF Core-backed implementation of IEmailSettingsService.
///
/// All operations are scoped to the tenant resolved from the JWT.
/// GetOrCreateAsync ensures idempotent initialization with platform defaults.
/// </summary>
internal sealed class EfEmailSettingsService : IEmailSettingsService
{
    private readonly XeniaDbContext _db;
    private readonly IAuditAdapter _auditAdapter;
    private readonly ILogger<EfEmailSettingsService> _logger;

    public EfEmailSettingsService(
        XeniaDbContext db,
        IAuditAdapter auditAdapter,
        ILogger<EfEmailSettingsService> logger)
    {
        _db = db;
        _auditAdapter = auditAdapter;
        _logger = logger;
    }

    public async Task<EmailSettingsDto> GetOrCreateAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var existing = await _db.EmailSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (existing is not null)
            return EmailSettingsDto.FromEntity(existing);

        var settings = EmailSettings.CreateDefault(tenantId);
        _db.EmailSettings.Add(settings);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created default EmailSettings for tenant {TenantId}", tenantId);

        return EmailSettingsDto.FromEntity(settings);
    }

    public async Task<EmailSettingsDto> UpdateAsync(
        Guid tenantId,
        Guid? actorId,
        UpdateEmailSettingsRequest request,
        CancellationToken ct = default)
    {
        var settings = await _db.EmailSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (settings is null)
        {
            settings = EmailSettings.CreateDefault(tenantId);
            _db.EmailSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }

        try
        {
            settings.Update(
                request.ConnectionTimeoutSeconds,
                request.AllowedProviderTypes,
                request.ValidationRetryLimit,
                request.ValidationHistoryRetentionDays,
                request.AllowedPorts,
                request.RequireTls,
                request.AllowCustomHosts,
                request.SsrfPolicyMode,
                request.DefaultSourceEnabled,
                request.ExpectedVersion,
                actorId);
        }
        catch (InvalidOperationException)
        {
            throw; // concurrency conflict — let caller handle
        }

        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_settings.update",
            ResourceType = "email_settings",
            ResourceId = settings.Id.ToString(),
            Result = "success",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = null,
            OccurredAt = DateTime.UtcNow,
        }, ct);

        _logger.LogInformation(
            "EmailSettings updated. TenantId={TenantId} Version={Version}", tenantId, settings.Version);

        return EmailSettingsDto.FromEntity(settings);
    }

    private async Task TryAuditAsync(XeniaAuditEvent ev, CancellationToken ct)
    {
        try { await _auditAdapter.RecordEventAsync(ev, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Audit event recording failed silently. Action={Action}", ev.Action);
        }
    }
}
