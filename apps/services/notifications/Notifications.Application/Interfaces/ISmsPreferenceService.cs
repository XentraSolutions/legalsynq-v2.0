namespace Notifications.Application.Interfaces;

public interface ISmsPreferenceService
{
    /// <summary>
    /// Get the current SMS preference state for a phone number within a tenant.
    /// Returns "unknown" when no preference record exists.
    /// </summary>
    Task<string> GetPreferenceStateAsync(Guid tenantId, string phone);

    /// <summary>
    /// Manually set SMS preference for a phone number.
    /// Audits the change with source = "manual_update".
    /// </summary>
    Task<SmsPreferenceDto> SetPreferenceAsync(Guid tenantId, string phone, string state, string? reason, string? actorUserId);

    /// <summary>
    /// Process an inbound SMS keyword (STOP, START, HELP, etc.) received via Twilio webhook.
    /// Updates preference state and emits audit events.
    /// </summary>
    /// <param name="tenantId">Tenant owning the SMS number; null if could not be resolved from webhook.</param>
    /// <param name="fromPhone">Sender's E.164 phone number.</param>
    /// <param name="keyword">Classified keyword category: "opt_out", "opt_in", or "help".</param>
    /// <param name="rawKeyword">Exact text from the inbound message body.</param>
    /// <param name="providerMessageId">Twilio MessageSid for traceability.</param>
    Task ProcessInboundKeywordAsync(Guid? tenantId, string fromPhone, string keyword, string rawKeyword, string? providerMessageId);

    /// <summary>
    /// Classify raw inbound message body as an SMS compliance keyword.
    /// Returns "opt_out", "opt_in", "help", or null if not a recognized compliance keyword.
    /// Matching is exact (after trim and case-fold), not substring.
    /// </summary>
    string? ClassifyKeyword(string? rawBody);

    /// <summary>
    /// List preferences for a tenant (operator/admin use).
    /// </summary>
    Task<List<SmsPreferenceDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0);
}
