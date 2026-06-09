using Commerce.Domain.Payments.Enums;

namespace Commerce.Contracts.Payments;

public sealed record CheckoutLineItem(
    string ProviderPriceId,
    int Quantity = 1);

public sealed record CreateCheckoutSessionRequest(
    Guid BillingAccountId,
    Guid SubscriptionId,
    IReadOnlyList<CheckoutLineItem> LineItems,
    string? SuccessUrl = null,
    string? CancelUrl = null,
    string? CustomerEmail = null,
    string? CustomerName = null,
    string? MetadataJson = null);

public sealed record CheckoutSessionResponse(
    PaymentProviderType Provider,
    string CheckoutSessionId,
    string CheckoutUrl,
    string ProviderCustomerId,
    DateTime? ExpiresAtUtc);

public sealed record PaymentProviderCustomerResponse(
    Guid Id,
    Guid BillingAccountId,
    PaymentProviderType Provider,
    string ProviderCustomerId,
    string? Email,
    string? Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PaymentMethodReferenceResponse(
    Guid Id,
    Guid BillingAccountId,
    PaymentProviderType Provider,
    string ProviderPaymentMethodId,
    string? ProviderCustomerId,
    string? Brand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PaymentProviderEventLogResponse(
    Guid Id,
    PaymentProviderType Provider,
    string ProviderEventId,
    string EventType,
    PaymentProviderEventProcessingStatus ProcessingStatus,
    string? ErrorMessage,
    DateTime? ProcessedAtUtc,
    DateTime CreatedAtUtc);
