using System.Text.Json;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Infrastructure.Payments.Stripe;

/// <summary>
/// Parses a raw Stripe webhook JSON body into a <see cref="NormalizedProviderEvent"/>.
/// The translator only inspects fields that Stripe documents as part
/// of its event envelope; unknown event types are returned as
/// <see cref="NormalizedProviderEventKind.Unsupported"/> so the
/// service layer can mark them as Ignored.
/// </summary>
internal static class StripeEventTranslator
{
    public static NormalizedProviderEvent Translate(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var providerEventId = TryGetString(root, "id") ?? string.Empty;
        var eventType = TryGetString(root, "type") ?? string.Empty;

        var data = root.TryGetProperty("data", out var dataEl) ? dataEl : default;
        var obj = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("object", out var objEl)
            ? objEl
            : default;

        var kind = MapKind(eventType);

        string? customerId = null, subId = null, sessionId = null, pmId = null;
        string? brand = null, last4 = null;
        int? expMonth = null, expYear = null;
        Guid? billingAccountId = null, subscriptionGuid = null;
        string? piId = null, invId = null, currency = null, failCode = null, failMsg = null;
        long? amountMinor = null;
        ProviderSubscriptionStatus? providerSubStatus = null;
        DateTime? occurredAt = null;

        var createdEpoch = TryGetLong(root, "created");
        if (createdEpoch.HasValue)
            occurredAt = DateTimeOffset.FromUnixTimeSeconds(createdEpoch.Value).UtcDateTime;

        if (obj.ValueKind == JsonValueKind.Object)
        {
            customerId = TryGetString(obj, "customer");
            switch (kind)
            {
                case NormalizedProviderEventKind.CheckoutSessionCompleted:
                case NormalizedProviderEventKind.CheckoutSessionExpired:
                    sessionId = TryGetString(obj, "id");
                    subId = TryGetString(obj, "subscription");
                    break;
                case NormalizedProviderEventKind.SubscriptionCreated:
                case NormalizedProviderEventKind.SubscriptionUpdated:
                case NormalizedProviderEventKind.SubscriptionDeleted:
                    subId = TryGetString(obj, "id");
                    providerSubStatus = MapSubscriptionStatus(TryGetString(obj, "status"));
                    if (kind == NormalizedProviderEventKind.SubscriptionDeleted)
                        providerSubStatus = ProviderSubscriptionStatus.Cancelled;
                    break;
                case NormalizedProviderEventKind.PaymentMethodAttached:
                    pmId = TryGetString(obj, "id");
                    if (obj.TryGetProperty("card", out var card) && card.ValueKind == JsonValueKind.Object)
                    {
                        brand = TryGetString(card, "brand");
                        last4 = TryGetString(card, "last4");
                        expMonth = TryGetInt(card, "exp_month");
                        expYear = TryGetInt(card, "exp_year");
                    }
                    break;
                case NormalizedProviderEventKind.PaymentIntentSucceeded:
                case NormalizedProviderEventKind.PaymentIntentFailed:
                    piId = TryGetString(obj, "id");
                    amountMinor = TryGetLong(obj, "amount_received") ?? TryGetLong(obj, "amount");
                    currency = NormalizeCurrency(TryGetString(obj, "currency"));
                    invId = TryGetString(obj, "invoice");
                    if (kind == NormalizedProviderEventKind.PaymentIntentFailed
                        && obj.TryGetProperty("last_payment_error", out var lpe)
                        && lpe.ValueKind == JsonValueKind.Object)
                    {
                        failCode = TryGetString(lpe, "code") ?? TryGetString(lpe, "type");
                        failMsg = TryGetString(lpe, "message");
                    }
                    break;
                case NormalizedProviderEventKind.InvoicePaymentSucceeded:
                case NormalizedProviderEventKind.InvoicePaymentFailed:
                    invId = TryGetString(obj, "id");
                    subId = TryGetString(obj, "subscription");
                    piId = TryGetString(obj, "payment_intent");
                    amountMinor = TryGetLong(obj, "amount_paid") ?? TryGetLong(obj, "amount_due");
                    currency = NormalizeCurrency(TryGetString(obj, "currency"));
                    break;
            }

            if (obj.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                billingAccountId = TryGetGuid(meta, "billing_account_id");
                subscriptionGuid = TryGetGuid(meta, "subscription_id");
            }
        }

        return new NormalizedProviderEvent(
            PaymentProviderType.Stripe,
            providerEventId,
            eventType,
            kind,
            customerId,
            subId,
            sessionId,
            pmId,
            brand,
            last4,
            expMonth,
            expYear,
            billingAccountId,
            subscriptionGuid,
            piId,
            invId,
            amountMinor,
            currency,
            failCode,
            failMsg,
            occurredAt,
            providerSubStatus);
    }

    private static NormalizedProviderEventKind MapKind(string eventType) => eventType switch
    {
        "checkout.session.completed" => NormalizedProviderEventKind.CheckoutSessionCompleted,
        "checkout.session.expired" => NormalizedProviderEventKind.CheckoutSessionExpired,
        "customer.subscription.created" => NormalizedProviderEventKind.SubscriptionCreated,
        "customer.subscription.updated" => NormalizedProviderEventKind.SubscriptionUpdated,
        "customer.subscription.deleted" => NormalizedProviderEventKind.SubscriptionDeleted,
        "payment_method.attached" => NormalizedProviderEventKind.PaymentMethodAttached,
        "payment_intent.succeeded" => NormalizedProviderEventKind.PaymentIntentSucceeded,
        "payment_intent.payment_failed" => NormalizedProviderEventKind.PaymentIntentFailed,
        "invoice.payment_succeeded" => NormalizedProviderEventKind.InvoicePaymentSucceeded,
        "invoice.payment_failed" => NormalizedProviderEventKind.InvoicePaymentFailed,
        _ => NormalizedProviderEventKind.Unsupported
    };

    private static ProviderSubscriptionStatus? MapSubscriptionStatus(string? raw) => raw switch
    {
        null or "" => null,
        "active" or "trialing" => ProviderSubscriptionStatus.Active,
        "canceled" or "cancelled" => ProviderSubscriptionStatus.Cancelled,
        "past_due" or "unpaid" or "incomplete" => ProviderSubscriptionStatus.Failed,
        "incomplete_expired" => ProviderSubscriptionStatus.Failed,
        _ => ProviderSubscriptionStatus.Unknown
    };

    private static string? NormalizeCurrency(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToUpperInvariant();

    private static string? TryGetString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
    }

    private static long? TryGetLong(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i) ? i : null;
    }

    private static Guid? TryGetGuid(JsonElement el, string name)
    {
        var s = TryGetString(el, name);
        return Guid.TryParse(s, out var g) ? g : null;
    }
}
