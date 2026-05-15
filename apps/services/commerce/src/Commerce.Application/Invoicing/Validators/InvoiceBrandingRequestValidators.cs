using System.Text.RegularExpressions;
using Commerce.Contracts.Invoicing;
using FluentValidation;

namespace Commerce.Application.Invoicing.Validators;

/// <summary>
/// Validates the <see cref="UpdateInvoiceBrandingRequest"/> body. Keeps rules
/// modest because most fields are advisory display strings, but enforces:
/// CompanyName non-empty (it is rendered as the issuer block heading), accent
/// colour is a 7-char `#RRGGBB` hex (the value is later inlined into a CSS
/// context, so syntax is enforced), URLs are absolute http(s), email shape is
/// lightly checked, and string lengths bound the column widths.
/// </summary>
public sealed class UpdateInvoiceBrandingRequestValidator : AbstractValidator<UpdateInvoiceBrandingRequest>
{
    private static readonly Regex HexColor = new(
        "^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public UpdateInvoiceBrandingRequestValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.AccentColorHex)
            .NotEmpty()
            .Must(c => c is not null && HexColor.IsMatch(c))
            .WithMessage("AccentColorHex must be a 7-character hex value such as #0F766E.");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(750_000)
            .Must(BeLogoUrlOrNull)
            .WithMessage("LogoUrl must be an absolute http(s) URL or an inline image data URI.");

        RuleFor(x => x.Website)
            .MaximumLength(1000)
            .Must(BeAbsoluteHttpUrlOrNull)
            .WithMessage("Website must be an absolute http or https URL.");

        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(120);
        RuleFor(x => x.StateRegion).MaximumLength(120);
        RuleFor(x => x.PostalCode).MaximumLength(40);
        RuleFor(x => x.Country)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Length == 2 && c.All(char.IsLetter)))
            .WithMessage("Country must be a 2-letter ISO code (e.g. US, GB) or empty.");

        RuleFor(x => x.ContactEmail)
            .MaximumLength(320)
            .Must(BeEmailOrNull)
            .WithMessage("ContactEmail must look like an email address.");

        RuleFor(x => x.ContactPhone).MaximumLength(64);
        RuleFor(x => x.FooterText).MaximumLength(1000);
    }

    private static bool BeAbsoluteHttpUrlOrNull(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        return u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps;
    }

    private static readonly Regex DataImageUri = new(
        @"^data:image\/(png|jpeg|jpg|gif|webp|svg\+xml);base64,[A-Za-z0-9+/=]+$",
        RegexOptions.Compiled);

    private static bool BeLogoUrlOrNull(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return DataImageUri.IsMatch(url);
        }
        return BeAbsoluteHttpUrlOrNull(url);
    }

    private static bool BeEmailOrNull(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch { return false; }
    }
}
