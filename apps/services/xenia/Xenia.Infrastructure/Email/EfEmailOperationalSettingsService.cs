using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>EF-backed operational settings service. Creates defaults on first access.</summary>
internal sealed class EfEmailOperationalSettingsService : IEmailOperationalSettingsService
{
    private readonly XeniaDbContext _db;
    private readonly ILogger<EfEmailOperationalSettingsService> _logger;

    public EfEmailOperationalSettingsService(
        XeniaDbContext db,
        ILogger<EfEmailOperationalSettingsService> logger)
    {
        _db    = db;
        _logger= logger;
    }

    public async Task<EmailOperationalSettings> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var settings = await _db.EmailOperationalSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (settings is not null) return settings;

        settings = EmailOperationalSettings.CreateDefaults(tenantId);
        _db.EmailOperationalSettings.Add(settings);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request created it first
            _db.ChangeTracker.Clear();
            settings = await _db.EmailOperationalSettings
                .FirstAsync(s => s.TenantId == tenantId, ct);
        }

        return settings;
    }

    public async Task<EmailOperationalSettings> UpdateAsync(
        Guid tenantId,
        UpdateOperationalSettingsRequest request,
        string? updatedBy,
        CancellationToken ct = default)
    {
        var settings = await _db.EmailOperationalSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (settings is null)
        {
            settings = EmailOperationalSettings.CreateDefaults(tenantId);
            _db.EmailOperationalSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }

        if (settings.Version != request.ExpectedVersion)
            throw new InvalidOperationException(
                $"Concurrency conflict: expected version {request.ExpectedVersion}, got {settings.Version}.");

        settings.Update(
            request.DefaultDashboardRangeDays,
            request.SourceFailureAlertThreshold,
            request.StaleSyncThresholdMinutes,
            request.LockWarningThresholdMinutes,
            request.MaximumRetryCount,
            request.CancellationTimeoutSeconds,
            request.MetricsEnabled,
            request.NotificationAlertsEnabled,
            request.DefaultRunPageSize,
            request.DefaultMessagePageSize,
            request.OperationalPollingIntervalSeconds,
            request.MessageMetadataRetentionDays,
            request.MessageBodyRetentionDays,
            request.ValidationHistoryRetentionDays,
            request.IngestionRunRetentionDays,
            request.AlertRetentionDays,
            request.AttachmentReferenceRetentionDays,
            request.PurgeBatchSize,
            request.RetentionDryRunDefault,
            request.LegalHoldEnabled,
            request.RetentionEnabled,
            updatedBy);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Operational settings updated for tenant={TenantId} by={UpdatedBy}",
            tenantId, updatedBy);

        return settings;
    }
}
