using Commerce.Domain.Common;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Payments;

/// <summary>
/// Commerce-owned financial record of a payment derived from one or
/// more provider events. Distinct from <see cref="PaymentProviderSubscription"/>
/// which is a provider lifecycle mapping.
/// </summary>
public sealed class Payment : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public string? ProviderPaymentId { get; private set; }
    public string? ProviderCustomerId { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public PaymentStatus Status { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }

    /// <summary>
    /// Free-form method tag for manually-recorded payments
    /// (e.g. <c>cash</c>, <c>check</c>, <c>ach</c>, <c>wire</c>,
    /// <c>card</c>, <c>other</c>). Null for provider-driven payments.
    /// </summary>
    public string? Method { get; private set; }

    /// <summary>
    /// Optional admin-supplied free text recorded alongside a manual
    /// payment (memo, check number context, dispute notes, etc.).
    /// Capped at 2000 characters.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Optional human label identifying who recorded the manual payment
    /// (e.g. an operator name or initials). The Commerce service does
    /// not currently model admin identities, so this is a free text
    /// audit hint rather than a foreign key.
    /// </summary>
    public string? RecordedByLabel { get; private set; }

    /// <summary>
    /// Optional out-of-band reference for a manual payment (check number,
    /// wire confirmation, ACH trace id, etc.). Stored separately from
    /// <see cref="ProviderPaymentId"/> so that admins can legitimately
    /// reuse the same reference (corrections, partials, refilings)
    /// without colliding with the provider-level uniqueness constraint
    /// on (Provider, ProviderPaymentId).
    /// </summary>
    public string? TransactionReference { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Payment() { }

    public static Payment Create(
        Guid billingAccountId,
        Guid? invoiceId,
        Guid? subscriptionId,
        PaymentProviderType provider,
        string? providerPaymentId,
        string? providerCustomerId,
        long amountMinor,
        string currency,
        PaymentStatus status,
        DateTime nowUtc,
        string? method = null,
        string? notes = null,
        string? recordedByLabel = null)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new InvalidOperationException("Currency must be a 3-letter code.");
        if (amountMinor < 0)
            throw new InvalidOperationException("AmountMinor cannot be negative.");

        return new Payment
        {
            Id = Guid.CreateVersion7(),
            BillingAccountId = billingAccountId,
            InvoiceId = invoiceId,
            SubscriptionId = subscriptionId,
            Provider = provider,
            ProviderPaymentId = string.IsNullOrWhiteSpace(providerPaymentId) ? null : providerPaymentId.Trim(),
            ProviderCustomerId = string.IsNullOrWhiteSpace(providerCustomerId) ? null : providerCustomerId.Trim(),
            AmountMinor = amountMinor,
            Currency = currency.ToUpperInvariant(),
            Status = status,
            PaidAtUtc = status == PaymentStatus.Succeeded ? nowUtc : (DateTime?)null,
            Method = Trim(method, 32),
            Notes = Trim(notes, 2000),
            RecordedByLabel = Trim(recordedByLabel, 200),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Convenience factory for manually-recorded payments: provider is
    /// fixed to <see cref="PaymentProviderType.Manual"/>, status is
    /// always <see cref="PaymentStatus.Succeeded"/>, and the caller
    /// supplies the wall-clock <paramref name="paidAtUtc"/> separately
    /// from the persistence-time <paramref name="nowUtc"/> so backdated
    /// receipts are supported.
    /// </summary>
    public static Payment CreateManual(
        Guid billingAccountId,
        Guid? invoiceId,
        Guid? subscriptionId,
        long amountMinor,
        string currency,
        DateTime paidAtUtc,
        string? method,
        string? transactionReference,
        string? recordedByLabel,
        string? notes,
        DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new InvalidOperationException("Currency must be a 3-letter code.");
        if (amountMinor <= 0)
            throw new InvalidOperationException("Manual payment amount must be greater than zero.");
        if (paidAtUtc == default)
            throw new InvalidOperationException("PaidAtUtc is required for manual payments.");

        return new Payment
        {
            Id = Guid.CreateVersion7(),
            BillingAccountId = billingAccountId,
            InvoiceId = invoiceId,
            SubscriptionId = subscriptionId,
            Provider = PaymentProviderType.Manual,
            // Intentionally NOT mapped to ProviderPaymentId — that column
            // participates in a unique index on (Provider, ProviderPaymentId)
            // intended for provider-issued ids (Stripe payment intent etc.),
            // and admins legitimately re-enter the same check number / wire
            // reference across partial payments and corrections.
            ProviderPaymentId = null,
            ProviderCustomerId = null,
            AmountMinor = amountMinor,
            Currency = currency.ToUpperInvariant(),
            Status = PaymentStatus.Succeeded,
            PaidAtUtc = paidAtUtc,
            Method = Trim(method, 32),
            Notes = Trim(notes, 2000),
            RecordedByLabel = Trim(recordedByLabel, 200),
            TransactionReference = Trim(transactionReference, 128),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void MarkSucceeded(DateTime nowUtc)
    {
        Status = PaymentStatus.Succeeded;
        PaidAtUtc ??= nowUtc;
        FailureCode = null;
        FailureMessage = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFailed(string? code, string? message, DateTime nowUtc)
    {
        Status = PaymentStatus.Failed;
        FailureCode = Trim(code, 64);
        FailureMessage = Trim(message, 500);
        UpdatedAtUtc = nowUtc;
    }

    public void AttachInvoice(Guid invoiceId, DateTime nowUtc)
    {
        if (invoiceId == Guid.Empty) return;
        InvoiceId = invoiceId;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateAmount(long amountMinor, string currency, DateTime nowUtc)
    {
        if (amountMinor < 0) return;
        AmountMinor = amountMinor;
        if (!string.IsNullOrWhiteSpace(currency) && currency.Length == 3)
            Currency = currency.ToUpperInvariant();
        UpdatedAtUtc = nowUtc;
    }

    private static string? Trim(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
