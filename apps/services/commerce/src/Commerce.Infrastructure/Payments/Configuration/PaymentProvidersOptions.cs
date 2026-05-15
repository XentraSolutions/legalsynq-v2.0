namespace Commerce.Infrastructure.Payments.Configuration;

public sealed class PaymentProvidersOptions
{
    public const string SectionName = "PaymentProviders";

    public StripeOptions Stripe { get; set; } = new();
}

public sealed class StripeOptions
{
    public bool Enabled { get; set; } = false;
    public string? SecretKey { get; set; }
    public string? PublishableKey { get; set; }
    public string? WebhookSecret { get; set; }
    public string? DefaultSuccessUrl { get; set; }
    public string? DefaultCancelUrl { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.stripe.com";

    /// <summary>Tolerance in seconds for the webhook timestamp window.</summary>
    public int SignatureToleranceSeconds { get; set; } = 300;
}
