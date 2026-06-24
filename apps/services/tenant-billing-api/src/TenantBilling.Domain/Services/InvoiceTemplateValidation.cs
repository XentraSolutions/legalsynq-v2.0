using System.Text.RegularExpressions;
using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

/// <summary>
/// Pure validation + normalization helpers for the
/// <see cref="Entities.InvoiceTemplate"/> aggregate. All methods either
/// return a normalized value or throw <see cref="ArgumentException"/>
/// (which the controller maps to HTTP 400). Keeping these in one file
/// makes it easy to reason about the shape rules from the spec.
/// </summary>
public static class InvoiceTemplateValidation
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int LogoUrlMaxLength = 1000;
    public const int HeaderTextMaxLength = 2000;
    public const int FooterTextMaxLength = 4000;
    public const int PaymentInstructionsMaxLength = 4000;
    public const int TermsTextMaxLength = 8000;
    public const int MemoPlaceholderMaxLength = 2000;
    public const int InvoiceNumberPrefixMinLength = 1;
    public const int InvoiceNumberPrefixMaxLength = 20;
    public const int InvoiceNumberFormatMaxLength = 100;
    public const int DefaultDueDaysMin = 0;
    public const int DefaultDueDaysMax = 365;

    // ---- INV-TPL-04: issuer-block bounds ----
    //
    // Sized so the same column widths can hold human-friendly identity
    // and address text without truncation, but small enough that a
    // template snapshot row stays comfortably under MySQL's row-size
    // budget. Country max length 100 follows existing
    // bag-of-text address conventions in this service (single
    // BillingAddress column was 1000); 2-character ISO codes fit
    // easily and we don't enforce ISO 3166 here because templates
    // can legitimately read "United States of America".
    public const int IssuerDisplayNameMaxLength = 200;
    public const int IssuerLegalNameMaxLength = 250;
    public const int IssuerAddressLineMaxLength = 250;
    public const int IssuerCityMaxLength = 100;
    public const int IssuerStateRegionMaxLength = 100;
    public const int IssuerPostalCodeMaxLength = 100;
    public const int IssuerCountryMaxLength = 100;
    public const int IssuerEmailMaxLength = 320;
    public const int IssuerPhoneMaxLength = 50;
    public const int IssuerTaxIdMaxLength = 100;
    public const int IssuerWebsiteMaxLength = 500;

    private static readonly Regex HexColorRegex =
        new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private static readonly Regex InvoiceNumberPrefixRegex =
        new("^[A-Z0-9-]+$", RegexOptions.Compiled);

    private static readonly Regex AbsoluteHttpUrlRegex =
        new(@"^https?://[^\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SafeRelativeUrlRegex =
        new("^/[A-Za-z0-9._/-]*$", RegexOptions.Compiled);

    // Pragmatic email-shape check — same shape used by CustomerService
    // for non-HTTP callers. The DTO layer also enforces [EmailAddress].
    private static readonly Regex EmailShape =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Trim + length-check the human Name. Empty / whitespace is
    /// rejected because list/admin UIs need a label per template.
    /// </summary>
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name is required.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new ArgumentException($"Name must be at most {NameMaxLength} characters.", nameof(value));
        return trimmed;
    }

    public static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{fieldName} must be at most {maxLength} characters.", fieldName);
        return trimmed;
    }

    /// <summary>
    /// Normalize the accent color to <c>#RRGGBB</c> uppercase. Accepts
    /// any case as input. 3-character shorthand like <c>#FFF</c> is
    /// rejected — operators should standardize on the 6-char form so
    /// stored colors are unambiguous.
    /// </summary>
    public static string? NormalizeAccentColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!HexColorRegex.IsMatch(trimmed))
            throw new ArgumentException(
                "AccentColor must be a 6-digit hex color, e.g. '#1F4FFF'.",
                nameof(value));
        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// LogoUrl: absolute http(s) URL, OR a safe relative path starting
    /// with '/' and using only URL-safe characters. Length-bounded.
    /// We do not fetch / inspect the resource — that is a later block.
    /// </summary>
    public static string? NormalizeLogoUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > LogoUrlMaxLength)
            throw new ArgumentException($"LogoUrl must be at most {LogoUrlMaxLength} characters.", nameof(value));

        if (AbsoluteHttpUrlRegex.IsMatch(trimmed) || SafeRelativeUrlRegex.IsMatch(trimmed))
            return trimmed;

        throw new ArgumentException(
            "LogoUrl must be an absolute http/https URL or a safe relative path beginning with '/'.",
            nameof(value));
    }

    /// <summary>
    /// Trim, uppercase, and shape-check the invoice-number prefix.
    /// Allowed characters: <c>A-Z 0-9 -</c>; length 1-20.
    /// </summary>
    public static string? NormalizeInvoiceNumberPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var upper = value.Trim().ToUpperInvariant();
        if (upper.Length < InvoiceNumberPrefixMinLength || upper.Length > InvoiceNumberPrefixMaxLength)
            throw new ArgumentException(
                $"InvoiceNumberPrefix must be {InvoiceNumberPrefixMinLength}-{InvoiceNumberPrefixMaxLength} characters.",
                nameof(value));
        if (!InvoiceNumberPrefixRegex.IsMatch(upper))
            throw new ArgumentException(
                "InvoiceNumberPrefix may only contain uppercase letters, digits, and '-'.",
                nameof(value));
        return upper;
    }

    /// <summary>
    /// Length-bounds the format string. We deliberately do NOT enforce
    /// allowed-placeholder syntax here because invoice-number generation
    /// continues to use the existing <c>INV-YYYY-NNNNNN</c> sequence in
    /// INV-TPL-01 — the format is stored as configuration only, and a
    /// later block will introduce a parser. Restricting placeholders
    /// now would be guesswork.
    /// </summary>
    public static string? NormalizeInvoiceNumberFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > InvoiceNumberFormatMaxLength)
            throw new ArgumentException(
                $"InvoiceNumberFormat must be at most {InvoiceNumberFormatMaxLength} characters.",
                nameof(value));
        return trimmed;
    }

    public static int? ValidateDefaultDueDays(int? value)
    {
        if (value is null) return null;
        if (value.Value < DefaultDueDaysMin || value.Value > DefaultDueDaysMax)
            throw new ArgumentException(
                $"DefaultDueDays must be between {DefaultDueDaysMin} and {DefaultDueDaysMax}.",
                nameof(value));
        return value;
    }

    public static string ValidateStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Status is required.", nameof(value));
        var trimmed = value.Trim();
        if (!InvoiceTemplateStatus.IsValid(trimmed))
            throw new ArgumentException(
                $"Status '{value}' is not recognized. Allowed: Draft, Active, Retired.",
                nameof(value));
        return trimmed;
    }

    public static string ValidateOwnerType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OwnerType is required.", nameof(value));
        var trimmed = value.Trim();
        if (!InvoiceTemplateOwnerType.IsValid(trimmed))
            throw new ArgumentException(
                $"OwnerType '{value}' is not recognized. Allowed: Platform, Tenant.",
                nameof(value));
        return trimmed;
    }

    // ---- INV-TPL-04: issuer normalizers ----

    /// <summary>
    /// Trim + lowercase + shape-check an issuer email. Returns null
    /// when the input is blank. Same shape rule as CustomerService so
    /// templates and customers cannot disagree about what counts as a
    /// valid email address.
    /// </summary>
    public static string? NormalizeIssuerEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > IssuerEmailMaxLength)
            throw new ArgumentException(
                $"IssuerEmail must be at most {IssuerEmailMaxLength} characters.",
                nameof(value));

        var lowered = trimmed.ToLowerInvariant();
        if (!EmailShape.IsMatch(lowered))
            throw new ArgumentException(
                "IssuerEmail must be a valid email address.",
                nameof(value));
        return lowered;
    }

    /// <summary>
    /// Issuer website: must be an absolute http(s) URL when supplied.
    /// Relative paths are NOT accepted here — an issuer website is
    /// public-facing and a relative URL would break the From block on
    /// any external surface (HTML email body, downloaded PDF, etc).
    /// </summary>
    public static string? NormalizeIssuerWebsite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > IssuerWebsiteMaxLength)
            throw new ArgumentException(
                $"IssuerWebsite must be at most {IssuerWebsiteMaxLength} characters.",
                nameof(value));
        if (!AbsoluteHttpUrlRegex.IsMatch(trimmed))
            throw new ArgumentException(
                "IssuerWebsite must be an absolute http or https URL.",
                nameof(value));
        return trimmed;
    }
}
