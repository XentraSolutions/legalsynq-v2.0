namespace Commerce.Application.Common.Exceptions;

/// <summary>
/// The requested payment provider exists but is currently disabled
/// in configuration. The caller should treat this as a controlled
/// 503/409 situation, not a 500.
/// </summary>
public sealed class PaymentProviderDisabledException : CatalogException
{
    public string Provider { get; }
    public PaymentProviderDisabledException(string provider)
        : base($"Payment provider '{provider}' is disabled.")
    {
        Provider = provider;
    }
}

/// <summary>
/// The provider is enabled but a required setting (e.g. SecretKey,
/// WebhookSecret) is missing or empty.
/// </summary>
public sealed class PaymentProviderConfigurationException : CatalogException
{
    public string Provider { get; }
    public string Setting { get; }
    public PaymentProviderConfigurationException(string provider, string setting)
        : base($"Payment provider '{provider}' is enabled but configuration '{setting}' is missing or empty.")
    {
        Provider = provider;
        Setting = setting;
    }
}

/// <summary>
/// Raw provider API failure; surfaced as 502 by the middleware. The
/// raw provider error text is sanitized before storage.
/// </summary>
public sealed class PaymentProviderException : CatalogException
{
    public string Provider { get; }
    public PaymentProviderException(string provider, string message)
        : base($"Payment provider '{provider}' returned an error: {message}")
    {
        Provider = provider;
    }
}

/// <summary>
/// Webhook signature verification failed. Mapped to HTTP 400.
/// </summary>
public sealed class InvalidWebhookSignatureException : CatalogException
{
    public string Provider { get; }
    public InvalidWebhookSignatureException(string provider)
        : base($"Webhook signature verification failed for provider '{provider}'.")
    {
        Provider = provider;
    }
}

/// <summary>
/// Webhook delivered an event id that has already been recorded.
/// Returned as 200 OK at the controller layer (not as an error
/// response) but exists as a typed signal for services and tests.
/// </summary>
public sealed class DuplicateProviderEventException : CatalogException
{
    public string Provider { get; }
    public string ProviderEventId { get; }
    public DuplicateProviderEventException(string provider, string providerEventId)
        : base($"Provider '{provider}' event '{providerEventId}' already processed.")
    {
        Provider = provider;
        ProviderEventId = providerEventId;
    }
}
