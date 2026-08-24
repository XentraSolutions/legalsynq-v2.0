using System.Globalization;
using System.Text.RegularExpressions;
using Intake.Domain.Extraction;
using Intake.Domain.Normalization;

namespace Intake.Application.Normalization;

public sealed class PersonNameNormalizer : IFactNormalizer
{
    private static readonly HashSet<string> PersonCodes =
    [
        "PATIENT_NAME",
        "PATIENT_FULL_NAME",
        "PATIENT_FIRST_NAME",
        "PATIENT_MIDDLE_NAME",
        "PATIENT_LAST_NAME",
        "ATTORNEY_NAME",
    ];

    private static readonly HashSet<string> Suffixes =
        ["JR", "JR.", "SR", "SR.", "II", "III", "IV", "V", "ESQ", "ESQ."];

    public bool CanNormalize(string factCode, string dataType) =>
        PersonCodes.Contains(factCode) &&
        string.Equals(dataType, ExtractionFactDataTypes.Name, StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        if (display.Length == 0)
            return NormalizationResult.Invalid("PERSON_NAME", ["NAME_EMPTY"]);

        var suffix = string.Empty;
        var commaParts = display.Split(',', 2, StringSplitOptions.TrimEntries);
        var lastName = string.Empty;
        var givenPart = display;
        if (commaParts.Length == 2)
        {
            lastName = commaParts[0];
            givenPart = commaParts[1];
        }

        var tokens = givenPart.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count > 0 && Suffixes.Contains(tokens[^1].ToUpperInvariant()))
        {
            suffix = tokens[^1].TrimEnd('.');
            tokens.RemoveAt(tokens.Count - 1);
        }

        string firstName;
        string? middleName;
        if (commaParts.Length == 2)
        {
            firstName = tokens.FirstOrDefault() ?? string.Empty;
            middleName = tokens.Count > 1 ? string.Join(' ', tokens.Skip(1)) : null;
        }
        else
        {
            firstName = tokens.FirstOrDefault() ?? string.Empty;
            lastName = tokens.Count > 1 ? tokens[^1] : string.Empty;
            middleName = tokens.Count > 2 ? string.Join(' ', tokens.Skip(1).SkipLast(1)) : null;
        }

        var fullName = string.Join(
            ' ',
            new[] { firstName, middleName, lastName, suffix }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var comparisonKey = NormalizationText.ComparisonKey(fullName);
        var json = NormalizationText.Json(new
        {
            fullName,
            firstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName,
            middleName,
            lastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName,
            suffix = string.IsNullOrWhiteSpace(suffix) ? null : suffix,
            comparisonKey,
        });

        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            return NormalizationResult.Partial(
                fullName,
                json,
                comparisonKey,
                "PERSON_NAME",
                ValidationStatuses.Incomplete,
                ["NAME_COMPONENTS_PARTIAL"]);

        if (commaParts.Length == 1 && tokens.Count >= 4)
            return NormalizationResult.Partial(
                fullName,
                json,
                comparisonKey,
                "PERSON_NAME",
                ValidationStatuses.Ambiguous,
                [NormalizationWarningCodes.NameComponentsPartial]);

        return NormalizationResult.Success(fullName, json, comparisonKey, "PERSON_NAME");
    }
}

public sealed class OrganizationNormalizer : IFactNormalizer
{
    private static readonly HashSet<string> OrganizationCodes =
        ["PROVIDER_NAME", "FACILITY_NAME", "LAW_FIRM_NAME", "INSURER_NAME", "INSURANCE_CARRIER_NAME"];

    private static readonly (Regex Pattern, string Replacement)[] Suffixes =
    [
        (new Regex(@"(?:,?\s*)L\.?L\.?C\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "LLC"),
        (new Regex(@"(?:,?\s*)P\.?L\.?L\.?C\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PLLC"),
        (new Regex(@"(?:,?\s*)P\.?C\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PC"),
        (new Regex(@"(?:,?\s*)I\.?N\.?C\.?\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "INC"),
        (new Regex(@"(?:,?\s*)C\.?O\.?R\.?P\.?O\.?R\.?A\.?T\.?I\.?O\.?N\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "CORPORATION"),
        (new Regex(@"(?:,?\s*)C\.?O\.?R\.?P\.?$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "CORP"),
    ];

    public bool CanNormalize(string factCode, string dataType) =>
        OrganizationCodes.Contains(factCode) &&
        string.Equals(dataType, ExtractionFactDataTypes.Name, StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        if (display.Length == 0)
            return NormalizationResult.Invalid("ORGANIZATION", ["ORGANIZATION_EMPTY"]);

        foreach (var (pattern, replacement) in Suffixes)
        {
            if (!pattern.IsMatch(display))
                continue;
            display = pattern.Replace(display, $", {replacement}");
            break;
        }

        var comparisonKey = NormalizationText.ComparisonKey(display);
        var json = NormalizationText.Json(new { displayName = display, comparisonKey });
        return NormalizationResult.Success(display, json, comparisonKey, "ORGANIZATION");
    }
}

public sealed class DateNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) =>
        string.Equals(dataType, ExtractionFactDataTypes.Date, StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        if (display.Length == 0)
            return NormalizationResult.Invalid("DATE", ["DATE_EMPTY"]);

        var numericDate = Regex.IsMatch(display, @"^\d{1,2}[/-]\d{1,2}[/-]\d{2,4}$");
        var parsed = DateOnly.TryParse(
            display,
            input.Options.DateCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var date);
        if (!parsed)
            return NormalizationResult.Invalid("DATE", ["DATE_INVALID"]);

        var canonical = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var comparisonKey = canonical.Replace("-", string.Empty, StringComparison.Ordinal);
        var warnings = numericDate ? [NormalizationWarningCodes.DateCultureApplied] : Array.Empty<string>();
        if (numericDate &&
            !input.Options.AllowAmbiguousDateNormalization &&
            !CanBeResolvedByCulture(display, input.Options.DateCulture))
        {
            var json = NormalizationText.Json(new { date = canonical, parseMethod = input.Options.DateCulture.Name });
            return NormalizationResult.Ambiguous(
                canonical,
                json,
                comparisonKey,
                "DATE",
                [NormalizationWarningCodes.DateAmbiguous]);
        }

        return new FactNormalizationResult(
            canonical,
            NormalizationText.Json(new
            {
                date = canonical,
                parseMethod = input.Options.DateCulture.Name,
                sourceWasCultureAmbiguous = numericDate,
            }),
            comparisonKey,
            NormalizationStatuses.Normalized,
            ValidationStatuses.Valid,
            warnings,
            "DATE",
            "1",
            date);
    }

    private static bool CanBeResolvedByCulture(string value, CultureInfo culture) =>
        DateOnly.TryParseExact(value, ["M/d/yyyy", "MM/dd/yyyy", "M-d-yyyy", "MM-dd-yyyy"],
            culture, DateTimeStyles.AllowWhiteSpaces, out _);
}

public sealed class MoneyNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) =>
        string.Equals(dataType, ExtractionFactDataTypes.Money, StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        if (display.Length == 0)
            return NormalizationResult.Invalid("MONEY", ["MONEY_EMPTY"]);

        var negative = display.StartsWith("-") || (display.StartsWith("(") && display.EndsWith(")"));
        var currency = DetectCurrency(display);
        var numeric = Regex.Replace(display, @"[^\d.,+-]", string.Empty);
        numeric = numeric.Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim('+', '-');
        if (negative)
            numeric = "-" + numeric;
        if (!decimal.TryParse(
                numeric,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
            return NormalizationResult.Invalid("MONEY", ["MONEY_INVALID"]);

        var currencyWasAssumed = currency is null;
        currency ??= input.Options.DefaultCurrencyCode.ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            return NormalizationResult.Invalid("MONEY", ["CURRENCY_INVALID"]);

        var canonicalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        var comparisonKey = $"{currency}:{canonicalAmount}";
        var warnings = currencyWasAssumed
            ? new[] { NormalizationWarningCodes.CurrencyAssumed }
            : Array.Empty<string>();
        return NormalizationResult.Success(
            canonicalAmount,
            NormalizationText.Json(new { amount = canonicalAmount, currencyCode = currency }),
            comparisonKey,
            "MONEY",
            warnings);
    }

    private static string? DetectCurrency(string value)
    {
        if (value.Contains('$', StringComparison.Ordinal) ||
            Regex.IsMatch(value, @"\bUSD\b", RegexOptions.IgnoreCase))
            return "USD";
        if (Regex.IsMatch(value, @"\bEUR\b|€", RegexOptions.IgnoreCase))
            return "EUR";
        if (Regex.IsMatch(value, @"\bGBP\b|£", RegexOptions.IgnoreCase))
            return "GBP";
        if (Regex.IsMatch(value, @"\bCAD\b", RegexOptions.IgnoreCase))
            return "CAD";
        return Regex.Match(value, @"\b[A-Z]{3}\b", RegexOptions.IgnoreCase).Value.ToUpperInvariant()
            is { Length: 3 } code ? code : null;
    }
}

public sealed class PhoneNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) =>
        factCode.EndsWith("_PHONE", StringComparison.Ordinal) ||
        string.Equals(dataType, "PHONE", StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        var extensionMatch = Regex.Match(display, @"(?:ext\.?|x)\s*(\d+)\s*$", RegexOptions.IgnoreCase);
        var extension = extensionMatch.Success ? extensionMatch.Groups[1].Value : null;
        var numberPart = extensionMatch.Success
            ? display[..extensionMatch.Index].Trim()
            : display;
        var digits = new string(numberPart.Where(char.IsDigit).ToArray());
        var assumed = false;
        string countryCode;
        string nationalNumber;
        if (numberPart.TrimStart().StartsWith("+", StringComparison.Ordinal))
        {
            if (digits.Length < 8)
                return NormalizationResult.Invalid("PHONE", ["PHONE_INVALID"]);
            countryCode = digits.Length > 10 ? digits[..(digits.Length - 10)] : "1";
            nationalNumber = digits[^10..];
        }
        else if (input.Options.DefaultCountryCode.Equals("US", StringComparison.OrdinalIgnoreCase) &&
                 digits.Length is 10 or 11)
        {
            assumed = digits.Length == 10;
            if (digits.Length == 11 && !digits.StartsWith('1'))
                return NormalizationResult.Invalid("PHONE", ["PHONE_INVALID"]);
            countryCode = "1";
            nationalNumber = digits[^10..];
        }
        else
        {
            return NormalizationResult.Partial(
                display,
                NormalizationText.Json(new { raw = display, extension }),
                NormalizationText.ComparisonKey(display),
                "PHONE",
                ValidationStatuses.Incomplete,
                ["PHONE_COUNTRY_REQUIRED"]);
        }

        var e164 = $"+{countryCode}{nationalNumber}";
        var warnings = assumed
            ? new[] { NormalizationWarningCodes.PhoneCountryAssumed }
            : Array.Empty<string>();
        return NormalizationResult.Success(
            e164,
            NormalizationText.Json(new
            {
                countryCode,
                nationalNumber,
                extension,
                e164,
                comparisonKey = countryCode + nationalNumber,
            }),
            countryCode + nationalNumber,
            "PHONE",
            warnings);
    }
}

public sealed class EmailNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) =>
        factCode.EndsWith("_EMAIL", StringComparison.Ordinal) ||
        string.Equals(dataType, "EMAIL", StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = input.RawValue.Trim();
        var at = display.LastIndexOf('@');
        if (at <= 0 || at == display.Length - 1 ||
            display.Any(char.IsWhiteSpace) ||
            !Regex.IsMatch(display[(at + 1)..], @"^[^.\s@]+(?:\.[^.\s@]+)+$"))
            return NormalizationResult.Invalid("EMAIL", [NormalizationWarningCodes.EmailInvalid]);

        var normalized = display[..at] + "@" + display[(at + 1)..].ToLowerInvariant();
        var comparisonKey = normalized.ToLowerInvariant();
        return NormalizationResult.Success(
            normalized,
            NormalizationText.Json(new { email = normalized, comparisonKey }),
            comparisonKey,
            "EMAIL");
    }
}

public sealed class AddressNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) =>
        string.Equals(dataType, ExtractionFactDataTypes.Address, StringComparison.Ordinal) ||
        factCode.EndsWith("_ADDRESS", StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        if (display.Length == 0)
            return NormalizationResult.Invalid("ADDRESS", ["ADDRESS_EMPTY"]);

        var match = Regex.Match(
            display,
            @"^(?<line1>[^,]+),\s*(?<city>[^,]+),\s*(?<state>[A-Za-z]{2})\s+(?<postal>\d{5}(?:-\d{4})?)(?:,\s*(?<country>[A-Za-z]{2}))?$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
            return NormalizationResult.Partial(
                display,
                NormalizationText.Json(new { addressLine1 = display }),
                NormalizationText.ComparisonKey(display),
                "ADDRESS",
                ValidationStatuses.Ambiguous,
                [NormalizationWarningCodes.AddressIncomplete]);

        var state = match.Groups["state"].Value.ToUpperInvariant();
        var postal = match.Groups["postal"].Value;
        var country = match.Groups["country"].Success
            ? match.Groups["country"].Value.ToUpperInvariant()
            : input.Options.DefaultCountryCode.ToUpperInvariant();
        if (country.Length != 2)
            return NormalizationResult.Invalid("ADDRESS", ["COUNTRY_INVALID"]);

        var normalized = string.Join(
            ", ",
            match.Groups["line1"].Value,
            match.Groups["city"].Value,
            $"{state} {postal}");
        var comparisonKey = NormalizationText.ComparisonKey(
            $"{match.Groups["line1"].Value} {match.Groups["city"].Value} {state} {postal} {country}");
        return NormalizationResult.Success(
            normalized,
            NormalizationText.Json(new
            {
                addressLine1 = match.Groups["line1"].Value,
                addressLine2 = (string?)null,
                city = match.Groups["city"].Value,
                state,
                postalCode = postal,
                country,
                comparisonKey,
            }),
            comparisonKey,
            "ADDRESS");
    }
}

public sealed class IdentifierNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) =>
        string.Equals(dataType, ExtractionFactDataTypes.Identifier, StringComparison.Ordinal);

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue)
            .Trim(' ', '\t', ',', ';', ':');
        if (display.Length == 0)
            return NormalizationResult.Invalid("IDENTIFIER", ["IDENTIFIER_EMPTY"]);

        var comparisonKey = NormalizationText.ComparisonKey(display);
        var warnings = comparisonKey.Length == 0
            ? new[] { NormalizationWarningCodes.IdentifierFormatUnrecognized }
            : Array.Empty<string>();
        return NormalizationResult.Success(
            display.ToUpperInvariant(),
            NormalizationText.Json(new { value = display.ToUpperInvariant(), comparisonKey }),
            comparisonKey,
            "IDENTIFIER",
            warnings);
    }
}

public sealed class TextNormalizer : IFactNormalizer
{
    public bool CanNormalize(string factCode, string dataType) => true;

    public FactNormalizationResult Normalize(FactNormalizationInput input)
    {
        var display = NormalizationText.Display(input.RawValue);
        if (display.Length == 0)
            return NormalizationResult.Invalid("TEXT", ["TEXT_EMPTY"]);

        var comparisonKey = NormalizationText.ComparisonKey(display);
        return NormalizationResult.Success(
            display,
            NormalizationText.Json(new { text = display, comparisonKey }),
            comparisonKey,
            "TEXT");
    }
}