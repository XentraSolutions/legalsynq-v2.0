using System.Text.Json;
using Commerce.Contracts.Billing;
using Commerce.Domain.Billing;
using FluentValidation;

namespace Commerce.Application.Billing.Validators;

internal static class BillingRules
{
    public static bool IsCurrency(string? c)
        => c is { Length: 3 } && c.All(char.IsLetter) && c == c.ToUpperInvariant();

    public static bool IsCountry(string? c)
        => c is null
           || (c.Length == 2 && c.All(char.IsLetter) && c == c.ToUpperInvariant());

    public static bool IsEmail(string? e)
    {
        if (string.IsNullOrWhiteSpace(e)) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(e);
            return addr.Address == e.Trim();
        }
        catch { return false; }
    }

    public static bool IsValidJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        try { using var _ = JsonDocument.Parse(raw); return true; }
        catch { return false; }
    }
}

public sealed class CreateBillingAccountRequestValidator : AbstractValidator<CreateBillingAccountRequest>
{
    public CreateBillingAccountRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).MaximumLength(400);
        RuleFor(x => x.DefaultCurrency)
            .NotEmpty()
            .Must(BillingRules.IsCurrency)
            .WithMessage("DefaultCurrency must be exactly 3 uppercase ASCII letters, e.g. USD.");
    }
}

public sealed class UpdateBillingAccountRequestValidator : AbstractValidator<UpdateBillingAccountRequest>
{
    public UpdateBillingAccountRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).MaximumLength(400);
        RuleFor(x => x.DefaultCurrency)
            .NotEmpty()
            .Must(BillingRules.IsCurrency)
            .WithMessage("DefaultCurrency must be exactly 3 uppercase ASCII letters, e.g. USD.");
    }
}

public sealed class CreateExternalRefRequestValidator : AbstractValidator<CreateExternalRefRequest>
{
    public CreateExternalRefRequestValidator()
    {
        RuleFor(x => x.HostPlatformKey)
            .NotEmpty()
            .Must(HostPlatformKey.IsValid)
            .WithMessage("HostPlatformKey must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.ExternalTenantId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExternalCustomerRef).MaximumLength(128);
    }
}

public sealed class UpdateExternalRefRequestValidator : AbstractValidator<UpdateExternalRefRequest>
{
    public UpdateExternalRefRequestValidator()
    {
        RuleFor(x => x.HostPlatformKey)
            .NotEmpty()
            .Must(HostPlatformKey.IsValid)
            .WithMessage("HostPlatformKey must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.ExternalTenantId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExternalCustomerRef).MaximumLength(128);
    }
}

public sealed class CreateBillingContactRequestValidator : AbstractValidator<CreateBillingContactRequest>
{
    public CreateBillingContactRequestValidator()
    {
        RuleFor(x => x.ContactType).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .NotEmpty().MaximumLength(320)
            .Must(BillingRules.IsEmail).WithMessage("Email must be a valid email address.");
        RuleFor(x => x.Phone).MaximumLength(64);
    }
}

public sealed class UpdateBillingContactRequestValidator : AbstractValidator<UpdateBillingContactRequest>
{
    public UpdateBillingContactRequestValidator()
    {
        RuleFor(x => x.ContactType).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .NotEmpty().MaximumLength(320)
            .Must(BillingRules.IsEmail).WithMessage("Email must be a valid email address.");
        RuleFor(x => x.Phone).MaximumLength(64);
    }
}

public sealed class UpdateBillingProfileRequestValidator : AbstractValidator<UpdateBillingProfileRequest>
{
    public UpdateBillingProfileRequestValidator()
    {
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(120);
        RuleFor(x => x.StateRegion).MaximumLength(120);
        RuleFor(x => x.PostalCode).MaximumLength(40);
        RuleFor(x => x.Country)
            .Must(BillingRules.IsCountry)
            .WithMessage("Country must be exactly 2 uppercase ASCII letters, e.g. US.");
        RuleFor(x => x.TaxId).MaximumLength(64);
    }
}
