using Intake.Domain.Normalization;

namespace Intake.Application.Normalization;

public sealed class FactNormalizerRegistry(IEnumerable<IFactNormalizer> normalizers)
    : IFactNormalizerRegistry
{
    private readonly IReadOnlyList<IFactNormalizer> normalizers = normalizers.ToArray();

    public bool TryResolve(string factCode, string dataType, out IFactNormalizer normalizer)
    {
        normalizer = normalizers.FirstOrDefault(
            candidate => candidate.CanNormalize(factCode, dataType))!;
        return normalizer is not null;
    }
}

internal static class NormalizationResult
{
    public static FactNormalizationResult Success(
        string value,
        string json,
        string comparisonKey,
        string method,
        IReadOnlyList<string>? warnings = null,
        DateOnly? parsedDate = null) =>
        new(
            value,
            json,
            comparisonKey,
            NormalizationStatuses.Normalized,
            ValidationStatuses.Valid,
            warnings ?? [],
            method,
            "1",
            parsedDate);

    public static FactNormalizationResult Partial(
        string value,
        string json,
        string comparisonKey,
        string method,
        string validationStatus,
        IReadOnlyList<string> warnings) =>
        new(
            value,
            json,
            comparisonKey,
            NormalizationStatuses.Partial,
            validationStatus,
            warnings,
            method,
            "1");

    public static FactNormalizationResult Invalid(
        string method,
        IReadOnlyList<string> warnings,
        string? value = null,
        string? json = null,
        string? comparisonKey = null) =>
        new(
            value,
            json,
            comparisonKey,
            NormalizationStatuses.Invalid,
            ValidationStatuses.InvalidFormat,
            warnings,
            method,
            "1");

    public static FactNormalizationResult Ambiguous(
        string value,
        string json,
        string comparisonKey,
        string method,
        IReadOnlyList<string> warnings) =>
        new(
            value,
            json,
            comparisonKey,
            NormalizationStatuses.Ambiguous,
            ValidationStatuses.Ambiguous,
            warnings,
            method,
            "1");
}