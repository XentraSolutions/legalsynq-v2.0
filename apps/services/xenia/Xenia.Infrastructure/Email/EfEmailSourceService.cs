using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF Core-backed implementation of IEmailSourceService.
///
/// ALL queries are filtered by TenantId resolved from the JWT — never from caller input.
/// A source belonging to another tenant returns null / false, indistinguishable from not found.
/// </summary>
internal sealed class EfEmailSourceService : IEmailSourceService
{
    private readonly XeniaDbContext _db;
    private readonly IEmailConnectorRegistry _connectorRegistry;
    private readonly IAuditAdapter _auditAdapter;
    private readonly ILogger<EfEmailSourceService> _logger;

    public EfEmailSourceService(
        XeniaDbContext db,
        IEmailConnectorRegistry connectorRegistry,
        IAuditAdapter auditAdapter,
        ILogger<EfEmailSourceService> logger)
    {
        _db = db;
        _connectorRegistry = connectorRegistry;
        _auditAdapter = auditAdapter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmailSourceDto>> GetSourcesAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var sources = await _db.EmailSources
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.DisplayName)
            .ToListAsync(ct);

        return sources.Select(EmailSourceDto.FromEntity).ToList();
    }

    public async Task<EmailSourceDto?> GetSourceAsync(
        Guid tenantId, Guid sourceId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        return source is null ? null : EmailSourceDto.FromEntity(source);
    }

    public async Task<EmailSourceDto> CreateSourceAsync(
        Guid tenantId,
        Guid? actorId,
        CreateEmailSourceRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<EmailProviderType>(request.ProviderType, ignoreCase: true, out var providerType))
            throw new ArgumentException($"Unknown provider type: '{request.ProviderType}'.");

        if (!Enum.TryParse<EmailAuthType>(request.AuthType, ignoreCase: true, out var authType))
            throw new ArgumentException($"Unknown auth type: '{request.AuthType}'.");

        if (!EmailProviderDefinitions.IsAuthTypeSupported(providerType, authType))
            throw new ArgumentException(
                $"Auth type '{authType}' is not supported by provider '{providerType}'.");

        ValidateNoPlaintextCredentials(request.SecretReferenceId, "SecretReferenceId");

        var source = new EmailSource(
            Guid.CreateVersion7(),
            tenantId,
            request.DisplayName,
            request.EmailAddress,
            providerType,
            authType,
            actorId);

        source.SetDescription(request.Description);
        source.SetConnectionDetails(
            request.IncomingHost,
            request.IncomingPort,
            request.UseTls,
            request.MailboxFolder,
            request.Username);
        source.SetSecretReference(request.SecretReferenceId);
        source.SetOAuthConnectionRef(request.OAuthConnectionRef);

        if (!request.Enabled)
            source.Disable(actorId);

        _db.EmailSources.Add(source);

        if (!string.IsNullOrWhiteSpace(request.ProviderConfigurationJson))
        {
            var settings = new EmailProviderSettings(
                Guid.CreateVersion7(), tenantId, source.Id, providerType);
            settings.SetConfiguration(request.ProviderConfigurationJson);
            _db.EmailProviderSettings.Add(settings);
        }

        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_source.create",
            ResourceType = "email_source",
            ResourceId = source.Id.ToString(),
            Result = "success",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = null,
            OccurredAt = DateTime.UtcNow,
        }, ct);

        _logger.LogInformation(
            "EmailSource created. TenantId={TenantId} SourceId={SourceId} Provider={Provider}",
            tenantId, source.Id, providerType);

        return EmailSourceDto.FromEntity(source);
    }

    public async Task<EmailSourceDto?> UpdateSourceAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        UpdateEmailSourceRequest request,
        CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        if (source is null) return null;

        if (request.ExpectedRowVersion.HasValue && source.RowVersion != request.ExpectedRowVersion.Value)
            throw new InvalidOperationException(
                $"Concurrency conflict: expected row version {request.ExpectedRowVersion} but found {source.RowVersion}. Reload and retry.");

        ValidateNoPlaintextCredentials(request.SecretReferenceId, "SecretReferenceId");

        if (request.DisplayName is not null)
            source.UpdateDisplayName(request.DisplayName);

        if (request.Description is not null || request.DisplayName is not null)
            source.SetDescription(request.Description);

        source.SetConnectionDetails(
            request.IncomingHost ?? source.IncomingHost,
            request.IncomingPort ?? source.IncomingPort,
            request.UseTls ?? source.UseTls,
            request.MailboxFolder ?? source.MailboxFolder,
            request.Username ?? source.Username);

        if (request.SecretReferenceId is not null)
            source.SetSecretReference(request.SecretReferenceId);

        if (request.OAuthConnectionRef is not null)
            source.SetOAuthConnectionRef(request.OAuthConnectionRef);

        source.SetUpdatedBy(actorId);

        if (!string.IsNullOrWhiteSpace(request.ProviderConfigurationJson))
        {
            var settings = await _db.EmailProviderSettings
                .FirstOrDefaultAsync(p => p.EmailSourceId == sourceId, ct);

            if (settings is null)
            {
                settings = new EmailProviderSettings(
                    Guid.CreateVersion7(), tenantId, sourceId, source.ProviderType);
                _db.EmailProviderSettings.Add(settings);
            }

            settings.SetConfiguration(request.ProviderConfigurationJson);
        }

        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_source.update",
            ResourceType = "email_source",
            ResourceId = sourceId.ToString(),
            Result = "success",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = null,
            OccurredAt = DateTime.UtcNow,
        }, ct);

        return EmailSourceDto.FromEntity(source);
    }

    public async Task<bool> DeleteSourceAsync(
        Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        if (source is null) return false;

        // Soft delete — retain validation history and audit records
        source.SoftDelete(actorId);
        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_source.delete",
            ResourceType = "email_source",
            ResourceId = sourceId.ToString(),
            Result = "success",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = null,
            OccurredAt = DateTime.UtcNow,
            Detail = "soft_delete",
        }, ct);

        return true;
    }

    public async Task<bool> EnableSourceAsync(
        Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        if (source is null) return false;

        source.Enable(actorId);
        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_source.enable",
            ResourceType = "email_source",
            ResourceId = sourceId.ToString(),
            Result = "success",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = null,
            OccurredAt = DateTime.UtcNow,
        }, ct);

        return true;
    }

    public async Task<bool> DisableSourceAsync(
        Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        if (source is null) return false;

        source.Disable(actorId);
        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_source.disable",
            ResourceType = "email_source",
            ResourceId = sourceId.ToString(),
            Result = "success",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = null,
            OccurredAt = DateTime.UtcNow,
        }, ct);

        return true;
    }

    public async Task<EmailValidationResultDto> ValidateSourceAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sourceId && !s.IsDeleted, ct);

        if (source is null)
        {
            return new EmailValidationResultDto
            {
                SourceId = sourceId,
                Success = false,
                Result = EmailValidationResult.ConfigurationInvalid.ToString(),
                DurationMs = 0,
                ErrorCode = "SOURCE_NOT_FOUND",
                SafeErrorSummary = "Email source not found.",
                ValidatedAt = DateTime.UtcNow,
            };
        }

        source.RecordValidationStarted(actorId);
        await _db.SaveChangesAsync(ct);

        var startedAt = DateTime.UtcNow;
        ConnectorValidationResult connResult;

        try
        {
            if (!_connectorRegistry.HasConnector(source.ProviderType))
            {
                connResult = ConnectorValidationResult.Fail(
                    EmailValidationResult.ValidatorUnavailable,
                    "CONNECTOR_NOT_REGISTERED",
                    $"No connector is registered for provider '{source.ProviderType}'.", 0);
            }
            else
            {
                var connector = _connectorRegistry.GetConnector(source.ProviderType);
                var settings = await _db.EmailProviderSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.EmailSourceId == sourceId, ct);

                var context = new EmailSourceConnectorContext
                {
                    SourceId = source.Id,
                    TenantId = source.TenantId,
                    EmailAddress = source.EmailAddress,
                    AuthType = source.AuthType,
                    Username = source.Username,
                    IncomingHost = source.IncomingHost,
                    IncomingPort = source.IncomingPort,
                    UseTls = source.UseTls,
                    MailboxFolder = source.MailboxFolder,
                    SecretReferenceId = source.SecretReferenceId,
                    OAuthConnectionRef = source.OAuthConnectionRef,
                    CorrelationId = correlationId,
                    ProviderConfigurationJson = settings?.ConfigurationJson,
                };

                connResult = await connector.TestConnectionAsync(context, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unexpected error during email source validation. SourceId={SourceId}", sourceId);
            connResult = ConnectorValidationResult.Fail(
                EmailValidationResult.InternalError,
                "INTERNAL_ERROR",
                "An unexpected error occurred during validation.", 0);
        }

        var completedAt = DateTime.UtcNow;
        var durationMs = (int)(completedAt - startedAt).TotalMilliseconds;

        var previousHealth = source.HealthStatus;

        if (connResult.Success)
            source.RecordValidationSuccess(connResult.DurationMs > 0 ? connResult.DurationMs : durationMs, actorId);
        else
            source.RecordValidationFailure(
                connResult.ErrorCode ?? "UNKNOWN",
                connResult.SafeErrorSummary ?? "Validation failed.",
                connResult.DurationMs > 0 ? connResult.DurationMs : durationMs,
                actorId);

        var healthChanged = source.HealthStatus != previousHealth;

        var history = EmailValidationHistory.Create(
            Guid.CreateVersion7(),
            tenantId,
            sourceId,
            source.ProviderType,
            "connectivity",
            startedAt,
            completedAt,
            durationMs,
            connResult.Result,
            connResult.ErrorCode,
            connResult.SafeErrorSummary,
            correlationId,
            actorId);

        _db.EmailValidationHistory.Add(history);
        await _db.SaveChangesAsync(ct);

        await TryAuditAsync(new XeniaAuditEvent
        {
            Action = "email_source.validate",
            ResourceType = "email_source",
            ResourceId = sourceId.ToString(),
            Result = connResult.Success ? "success" : "failure",
            TenantId = tenantId,
            ActorId = actorId,
            CorrelationId = correlationId,
            OccurredAt = completedAt,
            Detail = connResult.ErrorCode,
        }, ct);

        if (healthChanged)
        {
            await TryAuditAsync(new XeniaAuditEvent
            {
                Action        = "xenia.email.source.health_changed",
                ResourceType  = "email_source",
                ResourceId    = sourceId.ToString(),
                Result        = "changed",
                TenantId      = tenantId,
                ActorId       = actorId,
                CorrelationId = correlationId,
                OccurredAt    = completedAt,
                Detail        = $"previous={previousHealth} current={source.HealthStatus}",
            }, ct);
        }

        return new EmailValidationResultDto
        {
            SourceId = sourceId,
            Success = connResult.Success,
            Result = connResult.Result.ToString(),
            DurationMs = durationMs,
            ErrorCode = connResult.ErrorCode,
            SafeErrorSummary = connResult.SafeErrorSummary,
            ValidatedAt = completedAt,
        };
    }

    public async Task<IReadOnlyList<ValidationHistoryDto>> GetValidationHistoryAsync(
        Guid tenantId,
        Guid sourceId,
        int limit,
        CancellationToken ct = default)
    {
        var cap = Math.Min(Math.Max(limit, 1), 100);

        var entries = await _db.EmailValidationHistory
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.EmailSourceId == sourceId)
            .OrderByDescending(h => h.StartedAt)
            .Take(cap)
            .ToListAsync(ct);

        return entries.Select(h => new ValidationHistoryDto
        {
            Id = h.Id,
            EmailSourceId = h.EmailSourceId,
            ProviderType = h.ProviderType.ToString(),
            ValidationType = h.ValidationType,
            StartedAt = h.StartedAt,
            CompletedAt = h.CompletedAt,
            DurationMs = h.DurationMs,
            Result = h.Result.ToString(),
            ErrorCode = h.ErrorCode,
            ErrorSummary = h.ErrorSummary,
            CreatedAtUtc = h.CreatedAtUtc,
        }).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ValidateNoPlaintextCredentials(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (value.Length > 500)
            throw new ArgumentException($"{fieldName} must not exceed 500 characters.");

        // Heuristic: if a value looks like a raw password (long random string without colon-prefix convention)
        // we cannot fully block it, but we can reject obviously problematic formats.
        // The secret reference service will confirm at resolution time that it's a valid reference, not a secret.
        // No additional plaintext detection here — responsibility is on caller and secret service contract.
    }

    private async Task TryAuditAsync(XeniaAuditEvent ev, CancellationToken ct)
    {
        try { await _auditAdapter.RecordEventAsync(ev, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit event recording failed silently. Action={Action}", ev.Action);
        }
    }
}
