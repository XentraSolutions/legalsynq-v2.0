namespace Commerce.Contracts.Invoicing;

/// <summary>
/// Issuer-side branding applied to every rendered Commerce invoice.
/// Returned by `GET /api/commerce/invoice-branding`; written via PUT.
/// All fields except <see cref="CompanyName"/> and <see cref="AccentColorHex"/>
/// are optional.
/// </summary>
public sealed record InvoiceBrandingResponse(
    string CompanyName,
    string? LogoUrl,
    string AccentColorHex,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    string? FooterText,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateInvoiceBrandingRequest(
    string CompanyName,
    string? LogoUrl,
    string AccentColorHex,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    string? FooterText);
