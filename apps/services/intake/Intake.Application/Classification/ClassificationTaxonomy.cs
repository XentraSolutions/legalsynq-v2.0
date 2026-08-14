using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Domain.Classification;

namespace Intake.Application.Classification;

public sealed record ClassificationTaxonomyClass(
    string Code,
    string Label,
    string Description);

public static class ClassificationTaxonomy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ClassificationTaxonomyClass> Parse(
        ClassificationTaxonomyDefinition taxonomy)
    {
        try
        {
            if (taxonomy.ClassesJson.Length > 32_000)
                throw new JsonException("Taxonomy definitions may not exceed 32000 characters.");
            var classes = JsonSerializer.Deserialize<List<ClassificationTaxonomyClass>>(
                taxonomy.ClassesJson,
                JsonOptions);
            if (classes is null || classes.Count is < 2 or > 64 ||
                classes.Any(item =>
                    string.IsNullOrWhiteSpace(item.Code) ||
                    string.IsNullOrWhiteSpace(item.Label) ||
                    item.Code.Length > 64 ||
                    item.Label.Length > 160 ||
                    item.Description.Length > 500))
            {
                throw new JsonException("Taxonomy must contain 2 to 64 valid classes.");
            }

            if (classes.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != classes.Count)
                throw new JsonException("Taxonomy class codes must be unique.");

            return classes;
        }
        catch (JsonException exception)
        {
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.TaxonomyInvalid,
                $"Classification taxonomy '{taxonomy.Code}' v{taxonomy.Version} is invalid: {exception.Message}");
        }
    }
}