using System.Text.RegularExpressions;
using Billing.Domain.Entities;

namespace Billing.Domain.StatementTemplates;

/// <summary>
/// Pure validation + normalization helpers for the
/// <see cref="StatementTemplate"/> aggregate. All methods either
/// return a normalized value or throw <see cref="ArgumentException"/>
/// (which the controller maps to HTTP 400).
/// </summary>
public static class StatementTemplateValidation
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int LogoUrlMaxLength = 1000;
    public const int HeaderTextMaxLength = 2000;
    public const int FooterTextMaxLength = 4000;
    public const int PaymentInstructionsMaxLength = 4000;
    public const int TermsTextMaxLength = 8000;
    public const int MemoPlaceholderMaxLength = 2000;
    public const int StatementNumberPrefixMaxLength = 20;

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

    private static readonly Regex StatementNumberPrefixRegex =
        new("^[A-Z0-9-]+$", RegexOptions.Compiled);

    private static readonly Regex AbsoluteHttpUrlRegex =
        new(@"^https?://[^\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SafeRelativeUrlRegex =
        new("^/[A-Za-z0-9._/-]*$", RegexOptions.Compiled);

    private static readonly Regex EmailShape =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name is required.", nameof(value));
        var trimmed = value.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new ArgumentException($"Name must be at most {NameMaxLength} characters.", nameof(value));
        return trimmed;
    }

    /// <summary>
    /// Trim + length-check; collapses an empty / whitespace input to
    /// <c>null</c> so a template carries either a real value or no
    /// value (no awkward empty strings round-tripping through the API).
    /// </summary>
    public static string? NormalizeOptionalText(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{paramName} must be at most {maxLength} characters.", paramName);
        return trimmed;
    }

    public static string ValidateStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Status is required.", nameof(value));
        var trimmed = value.Trim();
        if (!StatementTemplateStatus.IsValid(trimmed))
            throw new ArgumentException(
                $"Status '{trimmed}' is invalid. Allowed values: Draft, Active, Retired.",
                nameof(value));
        return trimmed;
    }

    public static string? NormalizeAccentColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!HexColorRegex.IsMatch(trimmed))
            throw new ArgumentException(
                "AccentColor must be a 6-digit hex string of the form #RRGGBB.",
                nameof(value));
        return trimmed.ToUpperInvariant();
    }

    public static string? NormalizeStatementNumberPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length > StatementNumberPrefixMaxLength)
            throw new ArgumentException(
                $"StatementNumberPrefix must be at most {StatementNumberPrefixMaxLength} characters.",
                nameof(value));
        if (!StatementNumberPrefixRegex.IsMatch(trimmed))
            throw new ArgumentException(
                "StatementNumberPrefix may only contain A–Z, 0–9, and '-'.",
                nameof(value));
        return trimmed;
    }

    public static string? NormalizeLogoUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > LogoUrlMaxLength)
            throw new ArgumentException(
                $"LogoUrl must be at most {LogoUrlMaxLength} characters.", nameof(value));
        if (!AbsoluteHttpUrlRegex.IsMatch(trimmed) && !SafeRelativeUrlRegex.IsMatch(trimmed))
            throw new ArgumentException(
                "LogoUrl must be an absolute http(s) URL or a safe relative path beginning with '/'.",
                nameof(value));
        return trimmed;
    }

    public static string? NormalizeIssuerEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > IssuerEmailMaxLength)
            throw new ArgumentException(
                $"IssuerEmail must be at most {IssuerEmailMaxLength} characters.", nameof(value));
        if (!EmailShape.IsMatch(trimmed))
            throw new ArgumentException("IssuerEmail must be a valid email address.", nameof(value));
        return trimmed;
    }

    public static string? NormalizeIssuerWebsite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > IssuerWebsiteMaxLength)
            throw new ArgumentException(
                $"IssuerWebsite must be at most {IssuerWebsiteMaxLength} characters.", nameof(value));
        if (!AbsoluteHttpUrlRegex.IsMatch(trimmed))
            throw new ArgumentException(
                "IssuerWebsite must be an absolute http(s) URL.",
                nameof(value));
        return trimmed;
    }
}
