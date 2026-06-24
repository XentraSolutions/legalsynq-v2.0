using System.Text.Json;
using Commerce.Contracts.Payments;
using FluentValidation;

namespace Commerce.Application.Payments.Validators;

internal static class PaymentRules
{
    public static bool IsValidJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        try { using var _ = JsonDocument.Parse(raw); return true; }
        catch { return false; }
    }

    public static bool IsAbsoluteHttpUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return Uri.TryCreate(raw, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static bool IsEmailOrEmpty(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        // Pragmatic check: at least one '@' with content on each side.
        var at = raw.IndexOf('@');
        return at > 0 && at < raw.Length - 1 && !raw.Contains(' ');
    }

    /// <summary>
    /// Whitelist of method tags accepted on a manual payment. The set is
    /// intentionally small and case-insensitive — admins can extend with
    /// <c>other</c> rather than typing free text we'd then have to
    /// normalise everywhere downstream.
    /// </summary>
    public static readonly IReadOnlySet<string> ManualPaymentMethods =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cash", "check", "ach", "wire", "card", "other"
        };

    public static bool IsAllowedManualMethod(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return ManualPaymentMethods.Contains(raw.Trim());
    }
}

public sealed class CreateCheckoutSessionRequestValidator : AbstractValidator<CreateCheckoutSessionRequest>
{
    public CreateCheckoutSessionRequestValidator()
    {
        RuleFor(x => x.BillingAccountId).NotEmpty();
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.LineItems)
            .NotNull()
            .Must(l => l != null && l.Count > 0)
            .WithMessage("At least one LineItem is required.");
        RuleForEach(x => x.LineItems).ChildRules(li =>
        {
            li.RuleFor(i => i.ProviderPriceId).NotEmpty().MaximumLength(128);
            li.RuleFor(i => i.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.SuccessUrl)
            .Must(PaymentRules.IsAbsoluteHttpUrl)
            .WithMessage("SuccessUrl must be an absolute http(s) URL when provided.");
        RuleFor(x => x.CancelUrl)
            .Must(PaymentRules.IsAbsoluteHttpUrl)
            .WithMessage("CancelUrl must be an absolute http(s) URL when provided.");
        RuleFor(x => x.CustomerEmail)
            .Must(PaymentRules.IsEmailOrEmpty)
            .WithMessage("CustomerEmail must be a valid email when provided.");
        RuleFor(x => x.CustomerName).MaximumLength(200);
        RuleFor(x => x.MetadataJson)
            .Must(PaymentRules.IsValidJson)
            .WithMessage("MetadataJson must be valid JSON when provided.");
    }
}

public sealed class RecordManualPaymentRequestValidator : AbstractValidator<RecordManualPaymentRequest>
{
    public RecordManualPaymentRequestValidator()
    {
        RuleFor(x => x.AmountMinor)
            .GreaterThan(0)
            .WithMessage("AmountMinor must be greater than zero.");
        RuleFor(x => x.PaidAtUtc)
            .NotEqual(default(DateTime))
            .WithMessage("PaidAtUtc is required.");
        RuleFor(x => x.Method)
            .Must(PaymentRules.IsAllowedManualMethod)
            .WithMessage("Method must be one of: cash, check, ach, wire, card, other.")
            .MaximumLength(32);
        RuleFor(x => x.TransactionReference).MaximumLength(128);
        RuleFor(x => x.RecordedByLabel).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
