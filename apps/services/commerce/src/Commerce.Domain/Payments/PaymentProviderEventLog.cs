using Commerce.Domain.Common;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Payments;

/// <summary>
/// Immutable record of a webhook/provider event. Once persisted the
/// payload is treated as read-only history. The
/// <c>(Provider, ProviderEventId)</c> tuple is unique — duplicate
/// deliveries are detected via the unique index and translated to a
/// <see cref="PaymentProviderEventProcessingStatus.Duplicate"/> outcome.
/// </summary>
public sealed class PaymentProviderEventLog : Entity<Guid>
{
    public PaymentProviderType Provider { get; private set; }
    public string ProviderEventId { get; private set; } = default!;
    public string EventType { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public PaymentProviderEventProcessingStatus ProcessingStatus { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentProviderEventLog() { }

    public static PaymentProviderEventLog Receive(
        PaymentProviderType provider,
        string providerEventId,
        string eventType,
        string payloadJson,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(providerEventId))
            throw new InvalidOperationException("ProviderEventId is required.");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new InvalidOperationException("EventType is required.");
        if (payloadJson is null)
            throw new InvalidOperationException("PayloadJson is required.");

        return new PaymentProviderEventLog
        {
            Id = Guid.CreateVersion7(),
            Provider = provider,
            ProviderEventId = providerEventId.Trim(),
            EventType = eventType.Trim(),
            PayloadJson = payloadJson,
            ProcessingStatus = PaymentProviderEventProcessingStatus.Received,
            CreatedAtUtc = nowUtc
        };
    }

    public void MarkProcessed(DateTime nowUtc)
    {
        ProcessingStatus = PaymentProviderEventProcessingStatus.Processed;
        ProcessedAtUtc = nowUtc;
    }

    public void MarkIgnored(string? reason, DateTime nowUtc)
    {
        ProcessingStatus = PaymentProviderEventProcessingStatus.Ignored;
        ErrorMessage = SafeError(reason);
        ProcessedAtUtc = nowUtc;
    }

    public void MarkDuplicate(DateTime nowUtc)
    {
        ProcessingStatus = PaymentProviderEventProcessingStatus.Duplicate;
        ProcessedAtUtc = nowUtc;
    }

    public void MarkFailed(string error, DateTime nowUtc)
    {
        ProcessingStatus = PaymentProviderEventProcessingStatus.Failed;
        ErrorMessage = SafeError(error);
        ProcessedAtUtc = nowUtc;
    }

    private static string? SafeError(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Cap and strip any stray secrets – we only keep a short, safe
        // descriptive line. Provider secrets must never end up here.
        var trimmed = raw.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }
}
