using Commerce.Domain.Common;

namespace Commerce.Domain.Invoicing;

/// <summary>
/// Singleton row holding the issuer-side branding applied to every invoice
/// rendered by the admin UI: company name, logo URL, accent colour, postal
/// address, contact info and a free-form footer line. There is exactly one
/// row in the database keyed by <see cref="SingletonId"/>; the service
/// auto-creates an empty default on first read so callers never have to
/// distinguish "missing" from "empty".
/// </summary>
public sealed class InvoiceBranding : Entity<Guid>
{
    /// <summary>Fixed primary key for the singleton row.</summary>
    public static readonly Guid SingletonId =
        new("0bbb0bbb-0000-0000-0000-000000000001");

    public string CompanyName { get; private set; } = "Commerce";
    public string? LogoUrl { get; private set; }
    public string AccentColorHex { get; private set; } = "#0F766E";

    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? StateRegion { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }

    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Website { get; private set; }

    public string? FooterText { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private InvoiceBranding() { }

    public static InvoiceBranding CreateDefault(DateTime nowUtc)
    {
        return new InvoiceBranding
        {
            Id = SingletonId,
            CompanyName = "Commerce",
            AccentColorHex = "#0F766E",
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(
        string companyName,
        string? logoUrl,
        string accentColorHex,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? stateRegion,
        string? postalCode,
        string? country,
        string? contactEmail,
        string? contactPhone,
        string? website,
        string? footerText,
        DateTime nowUtc)
    {
        CompanyName = (companyName ?? string.Empty).Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        AccentColorHex = (accentColorHex ?? "#0F766E").Trim();
        AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1.Trim();
        AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim();
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        StateRegion = string.IsNullOrWhiteSpace(stateRegion) ? null : stateRegion.Trim();
        PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim().ToLowerInvariant();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        FooterText = string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim();
        UpdatedAtUtc = nowUtc;
    }
}
