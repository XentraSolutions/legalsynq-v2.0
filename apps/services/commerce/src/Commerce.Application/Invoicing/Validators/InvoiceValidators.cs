using Commerce.Contracts.Invoicing;
using FluentValidation;

namespace Commerce.Application.Invoicing.Validators;

internal static class CurrencyRule
{
    public static bool IsValid(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length != 3) return false;
        for (int i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c < 'A' || c > 'Z') return false;
        }
        return true;
    }
}

public sealed class CreateInvoiceLineRequestValidator : AbstractValidator<CreateInvoiceLineRequest>
{
    public CreateInvoiceLineRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.UnitAmountMinor).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => !x.ServicePeriodStartUtc.HasValue
                       || !x.ServicePeriodEndUtc.HasValue
                       || x.ServicePeriodEndUtc.Value > x.ServicePeriodStartUtc.Value)
            .WithMessage("ServicePeriodEndUtc must be after ServicePeriodStartUtc.");
    }
}

public sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    /// <summary>
    /// Backstop on absurd back-dating of due dates. The application
    /// service performs deeper relationship checks (account exists,
    /// subscription belongs to account, etc).
    /// </summary>
    public static readonly TimeSpan MaxPastDueDate = TimeSpan.FromDays(365 * 10);

    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.BillingAccountId).NotEmpty();
        RuleFor(x => x.Currency)
            .Must(CurrencyRule.IsValid)
            .WithMessage("Currency must be a 3-letter uppercase ASCII code.");
        RuleFor(x => x.Lines)
            .NotNull()
            .Must(l => l != null && l.Count > 0)
            .WithMessage("At least one invoice line is required.");
        RuleForEach(x => x.Lines).SetValidator(new CreateInvoiceLineRequestValidator());
        RuleFor(x => x.DueDateUtc)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow.Subtract(MaxPastDueDate))
            .WithMessage("DueDateUtc cannot be more than 10 years in the past.");
    }
}
