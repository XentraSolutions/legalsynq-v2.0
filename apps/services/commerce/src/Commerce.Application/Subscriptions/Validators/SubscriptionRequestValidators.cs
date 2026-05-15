using System.Text.Json;
using Commerce.Contracts.Subscriptions;
using FluentValidation;

namespace Commerce.Application.Subscriptions.Validators;

internal static class SubscriptionRules
{
    public static bool IsValidJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        try { using var _ = JsonDocument.Parse(raw); return true; }
        catch { return false; }
    }

    /// <summary>
    /// EffectiveAtUtc cannot be unreasonably in the past. We allow up to
    /// 1 day backdating to tolerate clock skew but reject older values.
    /// </summary>
    public static bool IsReasonablyRecent(DateTime? effectiveAtUtc)
        => !effectiveAtUtc.HasValue
           || effectiveAtUtc.Value >= DateTime.UtcNow.AddDays(-1);
}

public sealed class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.BillingAccountId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.PriceId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.TrialDays!.Value)
            .InclusiveBetween(0, 365)
            .When(x => x.TrialDays.HasValue);
        RuleFor(x => x.MetadataJson)
            .Must(SubscriptionRules.IsValidJson)
            .WithMessage("MetadataJson must be valid JSON when provided.");
    }
}

public sealed class ChangeSubscriptionPlanRequestValidator : AbstractValidator<ChangeSubscriptionPlanRequest>
{
    public ChangeSubscriptionPlanRequestValidator()
    {
        RuleFor(x => x.NewPlanId).NotEmpty();
        RuleFor(x => x.NewPriceId).NotEmpty();
        RuleFor(x => x.Quantity!.Value).GreaterThanOrEqualTo(1).When(x => x.Quantity.HasValue);
        RuleFor(x => x.ProrationBehavior).IsInEnum();
        RuleFor(x => x.EffectiveAtUtc)
            .Must(SubscriptionRules.IsReasonablyRecent)
            .WithMessage("EffectiveAtUtc cannot be more than 1 day in the past.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.MetadataJson)
            .Must(SubscriptionRules.IsValidJson)
            .WithMessage("MetadataJson must be valid JSON when provided.");
    }
}

public sealed class CancelSubscriptionRequestValidator : AbstractValidator<CancelSubscriptionRequest>
{
    public CancelSubscriptionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RenewSubscriptionRequestValidator : AbstractValidator<RenewSubscriptionRequest>
{
    public RenewSubscriptionRequestValidator()
    {
        // No required fields; explicit anchor optional.
    }
}
