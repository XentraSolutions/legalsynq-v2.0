using System.Globalization;

namespace Intake.Application.Normalization;

public sealed record FactNormalizationOptions(
    string DefaultCountryCode,
    string DefaultCurrencyCode,
    CultureInfo DateCulture,
    bool AllowAmbiguousDateNormalization,
    string UnicodeForm,
    string NormalizationVersion);

public sealed record FactNormalizationInput(
    string FactCode,
    string DataType,
    string RawValue,
    FactNormalizationOptions Options);

public sealed record FactNormalizationResult(
    string? NormalizedValue,
    string? NormalizedJson,
    string? ComparisonKey,
    string NormalizationStatus,
    string ValidationStatus,
    IReadOnlyList<string> WarningCodes,
    string NormalizationMethod,
    string NormalizationVersion,
    DateOnly? ParsedDate = null);

public interface IFactNormalizer
{
    bool CanNormalize(string factCode, string dataType);

    FactNormalizationResult Normalize(FactNormalizationInput input);
}

public interface IFactNormalizerRegistry
{
    bool TryResolve(string factCode, string dataType, out IFactNormalizer normalizer);
}