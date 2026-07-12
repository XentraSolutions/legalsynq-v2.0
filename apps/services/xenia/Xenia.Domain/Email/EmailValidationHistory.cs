using Xenia.Domain.Common;

namespace Xenia.Domain.Email;

/// <summary>
/// Immutable record of a single email source validation attempt.
///
/// Stored for audit and diagnostic purposes. Never contains raw provider
/// error payloads, passwords, tokens, or connection strings.
/// </summary>
public sealed class EmailValidationHistory : AuditableEntityBase
{
    public const int ValidationTypeMaxLength = 50;
    public const int ErrorCodeMaxLength = 100;
    public const int ErrorSummaryMaxLength = 500;
    public const int CorrelationIdMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailSourceId { get; private set; }
    public EmailProviderType ProviderType { get; private set; }

    /// <summary>Type of validation performed: "connectivity", "credentials", "configuration".</summary>
    public string ValidationType { get; private set; } = string.Empty;

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int? DurationMs { get; private set; }

    public EmailValidationResult Result { get; private set; }

    /// <summary>Safe error code — must not expose credentials or internal topology.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>Sanitized human-readable error summary. No raw provider messages.</summary>
    public string? ErrorSummary { get; private set; }

    public string? CorrelationId { get; private set; }
    public Guid? ActorId { get; private set; }

    private EmailValidationHistory() { }

    public static EmailValidationHistory Create(
        Guid id,
        Guid tenantId,
        Guid emailSourceId,
        EmailProviderType providerType,
        string validationType,
        DateTime startedAt,
        DateTime completedAt,
        int durationMs,
        EmailValidationResult result,
        string? errorCode,
        string? errorSummary,
        string? correlationId,
        Guid? actorId)
    {
        return new EmailValidationHistory
        {
            Id = id,
            TenantId = tenantId,
            EmailSourceId = emailSourceId,
            ProviderType = providerType,
            ValidationType = validationType.Length > ValidationTypeMaxLength
                ? validationType[..ValidationTypeMaxLength] : validationType,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = durationMs,
            Result = result,
            ErrorCode = errorCode?.Length > ErrorCodeMaxLength ? errorCode[..ErrorCodeMaxLength] : errorCode,
            ErrorSummary = errorSummary?.Length > ErrorSummaryMaxLength ? errorSummary[..ErrorSummaryMaxLength] : errorSummary,
            CorrelationId = correlationId?.Length > CorrelationIdMaxLength ? correlationId[..CorrelationIdMaxLength] : correlationId,
            ActorId = actorId,
        };
    }
}
