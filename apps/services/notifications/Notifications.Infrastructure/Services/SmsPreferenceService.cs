using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Notifications.Infrastructure.Services;

public class SmsPreferenceServiceImpl : ISmsPreferenceService
{
    private readonly ISmsPreferenceRepository _repo;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<SmsPreferenceServiceImpl> _logger;

    private static readonly HashSet<string> OptOutKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "STOP", "STOPALL", "UNSUBSCRIBE", "CANCEL", "END", "QUIT" };

    private static readonly HashSet<string> OptInKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "START", "YES", "UNSTOP" };

    private const string HelpKeyword = "HELP";

    public SmsPreferenceServiceImpl(
        ISmsPreferenceRepository repo,
        IAuditEventClient auditClient,
        ILogger<SmsPreferenceServiceImpl> logger)
    {
        _repo        = repo;
        _auditClient = auditClient;
        _logger      = logger;
    }

    public string? ClassifyKeyword(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;
        var trimmed = rawBody.Trim();
        if (OptOutKeywords.Contains(trimmed)) return "opt_out";
        if (OptInKeywords.Contains(trimmed))  return "opt_in";
        if (string.Equals(trimmed, HelpKeyword, StringComparison.OrdinalIgnoreCase)) return "help";
        return null;
    }

    public async Task<string> GetPreferenceStateAsync(Guid tenantId, string phone)
    {
        var normalized = NormalizePhone(phone);
        var pref = await _repo.FindAsync(tenantId, normalized);
        return pref?.PreferenceState ?? "unknown";
    }

    public async Task<SmsPreferenceDto> SetPreferenceAsync(Guid tenantId, string phone, string state, string? reason, string? actorUserId)
    {
        if (state is not ("opted_in" or "opted_out"))
            throw new ArgumentException($"Invalid preference state: {state}. Must be 'opted_in' or 'opted_out'.", nameof(state));

        var normalized = NormalizePhone(phone);
        var pref = await _repo.UpsertAsync(new SmsContactPreference
        {
            TenantId        = tenantId,
            Phone           = normalized,
            PreferenceState = state,
            Source          = "manual_update",
            Reason          = reason ?? $"Manually set to {state} by operator",
            UpdatedBy       = actorUserId,
        });

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "sms.preference.manual_update",
                Action       = "sms.preference.manual_update",
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS preference manually set to '{state}' for phone {MaskPhone(normalized)}",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString() },
                Entity       = new AuditEventEntityDto { Type = "SMS_PREFERENCE", Id = pref.Id.ToString() },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone            = MaskPhone(normalized),
                    preference_state = state,
                    source           = "manual_update",
                    updated_by       = actorUserId,
                    reason           = reason,
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS preference manual update"); }

        var auditEventType = state == "opted_in" ? "sms.preference.opted_in" : "sms.preference.opted_out";
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = auditEventType,
                Action       = auditEventType,
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS preference state changed to '{state}' via manual update",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString() },
                Entity       = new AuditEventEntityDto { Type = "SMS_PREFERENCE", Id = pref.Id.ToString() },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone            = MaskPhone(normalized),
                    preference_state = state,
                    source           = "manual_update",
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS preference state change event"); }

        return MapToDto(pref);
    }

    public async Task ProcessInboundKeywordAsync(Guid? tenantId, string fromPhone, string keyword, string rawKeyword, string? providerMessageId)
    {
        var normalized = NormalizePhone(fromPhone);
        var maskedPhone = MaskPhone(normalized);

        string newState;
        string auditEventType;
        string source;

        switch (keyword)
        {
            case "opt_out":
                newState      = "opted_out";
                auditEventType = "sms.preference.opted_out";
                source        = "inbound_stop_keyword";
                break;
            case "opt_in":
                newState      = "opted_in";
                auditEventType = "sms.preference.opted_in";
                source        = "inbound_start_keyword";
                break;
            case "help":
                // HELP: audit only, no state change
                _logger.LogInformation("SMS HELP keyword received from {Phone}", maskedPhone);
                try
                {
                    await _auditClient.IngestAsync(new IngestAuditEventRequest
                    {
                        EventType    = "sms.preference.help_requested",
                        Action       = "sms.preference.help_requested",
                        SourceSystem = "notifications",
                        Outcome      = "success",
                        Description  = $"SMS HELP keyword received from {maskedPhone}",
                        Scope        = new AuditEventScopeDto { TenantId = tenantId.HasValue ? tenantId.Value.ToString() : string.Empty },
                        Metadata     = JsonSerializer.Serialize(new
                        {
                            phone               = maskedPhone,
                            keyword             = rawKeyword,
                            provider_message_id = providerMessageId,
                        }),
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS HELP keyword"); }
                return;
            default:
                _logger.LogWarning("ProcessInboundKeywordAsync called with unrecognized keyword category: {Keyword}", keyword);
                return;
        }

        try
        {
            await _repo.UpsertAsync(new SmsContactPreference
            {
                TenantId          = tenantId,
                Phone             = normalized,
                PreferenceState   = newState,
                Source            = source,
                Reason            = $"Inbound SMS keyword: {rawKeyword}",
                KeywordReceived   = rawKeyword,
                ProviderMessageId = providerMessageId,
            });

            _logger.LogInformation("SMS preference set to {State} for {Phone} via inbound keyword {Keyword}",
                newState, maskedPhone, rawKeyword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist SMS preference from inbound keyword for {Phone}", maskedPhone);
        }

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = auditEventType,
                Action       = auditEventType,
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS {newState.Replace('_', ' ')} via inbound keyword '{rawKeyword}' from {maskedPhone}",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.HasValue ? tenantId.Value.ToString() : string.Empty },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone               = maskedPhone,
                    preference_state    = newState,
                    keyword             = rawKeyword,
                    source              = source,
                    provider_message_id = providerMessageId,
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS preference change from inbound keyword"); }
    }

    public async Task<List<SmsPreferenceDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0)
    {
        var items = await _repo.GetByTenantAsync(tenantId, limit, offset);
        return items.Select(MapToDto).ToList();
    }

    internal static string NormalizePhone(string phone)
        => System.Text.RegularExpressions.Regex.Replace(phone.Trim(), @"[^\d+]", "");

    private static string MaskPhone(string normalized)
        => normalized.Length > 3 ? normalized[..3] + "***" : "***";

    private static SmsPreferenceDto MapToDto(SmsContactPreference p) => new()
    {
        Id              = p.Id,
        TenantId        = p.TenantId,
        Phone           = p.Phone,
        PreferenceState = p.PreferenceState,
        Source          = p.Source,
        Reason          = p.Reason,
        KeywordReceived = p.KeywordReceived,
        UpdatedBy       = p.UpdatedBy,
        CreatedAt       = p.CreatedAt,
        UpdatedAt       = p.UpdatedAt,
    };
}
