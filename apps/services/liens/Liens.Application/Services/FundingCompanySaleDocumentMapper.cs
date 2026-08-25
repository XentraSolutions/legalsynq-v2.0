using System.Globalization;
using System.Text.Json;
using Liens.Domain.Entities;

namespace Liens.Application.Services;

public static class FundingCompanySaleDocumentMapper
{
    private static readonly string[] FundingCompanyDocumentTaskTypes =
    [
        "LegacyCaseDocument",
        "LegacyLienDocument",
        "LegacyMedicalDocument",
        "SellingDocumentReference",
    ];

    public static bool IsFundingCompanyDocument(ServicingItem item)
        => IsFundingCompanyDocumentTaskType(item.TaskType);

    public static bool IsFundingCompanyDocumentTaskType(string? taskType)
        => !string.IsNullOrWhiteSpace(taskType) &&
           FundingCompanyDocumentTaskTypes.Contains(taskType, StringComparer.Ordinal);

    public static FundingCompanySaleDocument? Map(
        ServicingItem item,
        IReadOnlyDictionary<Guid, string> documentCategoryNames)
    {
        if (!IsFundingCompanyDocument(item))
            return null;

        var fields = ParseDocumentNoteFields(item.Notes);
        var fileName = FirstNonEmpty(
            fields.GetValueOrDefault("originalFileName"),
            fields.GetValueOrDefault("displayName"),
            fields.GetValueOrDefault("filename"),
            item.Description);

        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var category = FirstNonEmpty(
            ResolveDocumentCategoryName(fields, documentCategoryNames),
            fields.GetValueOrDefault("documentCategory"),
            fields.GetValueOrDefault("category"),
            HumanizeDocumentType(fields.GetValueOrDefault("documentType")),
            HumanizeDocumentTaskType(item.TaskType));

        var sizeOrType = FirstNonEmpty(
            fields.GetValueOrDefault("size"),
            fields.GetValueOrDefault("fileSize"),
            fields.GetValueOrDefault("contentLength"),
            ResolveFileExtension(fileName));

        return new FundingCompanySaleDocument(
            item.Id,
            TryResolveDocumentId(fields, out var documentId) ? documentId : null,
            fileName.Trim(),
            category,
            FormatDocumentSize(sizeOrType),
            item.CreatedAtUtc);
    }

    public static Dictionary<Guid, string> BuildDocumentCategoryNameLookup(
        IEnumerable<(Guid Id, string Name)> values)
    {
        return values
            .Where(value => value.Id != Guid.Empty && !string.IsNullOrWhiteSpace(value.Name))
            .GroupBy(value => value.Id)
            .ToDictionary(group => group.Key, group => group.First().Name.Trim());
    }

    private static string? ResolveDocumentCategoryName(
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyDictionary<Guid, string> documentCategoryNames)
    {
        return Guid.TryParse(fields.GetValueOrDefault("documentTypeId"), out var documentTypeId) &&
            documentCategoryNames.TryGetValue(documentTypeId, out var categoryName)
            ? categoryName
            : null;
    }

    private static Dictionary<string, string> ParseDocumentNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        try
        {
            using var document = JsonDocument.Parse(notes);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var value = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        result[property.Name] = value;
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to the legacy key/value parser below.
        }

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool TryResolveDocumentId(
        IReadOnlyDictionary<string, string> fields,
        out Guid documentId)
    {
        if (Guid.TryParse(fields.GetValueOrDefault("documentId"), out documentId))
            return true;

        var url = fields.GetValueOrDefault("url");
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var segment = url.Trim().TrimEnd('/').Split('/').LastOrDefault();
        return Guid.TryParse(segment, out documentId);
    }

    private static string? ResolveFileExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.').ToUpperInvariant();
    }

    private static string? HumanizeDocumentType(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return null;

        var knownLabel = documentType.Trim() switch
        {
            "WHO" => "Signed Lien / LOP (Letter of Protection)",
            "HOWMUCH" => "Itemized Bill / HCFA-1500 Form",
            "MedicalBill" => "Itemized Bill / HCFA-1500 Form",
            "MedicalRecord" => "Clinical Chart Notes / Medical Records",
            "PoliceReport" => "Case Underwriting / Police Report",
            _ => null,
        };
        if (knownLabel is not null)
            return knownLabel;

        var normalized = documentType.Trim()
            .Replace('_', ' ')
            .Replace('-', ' ');
        var label = new List<char>(normalized.Length + 8);
        for (var i = 0; i < normalized.Length; i++)
        {
            var current = normalized[i];
            if (i > 0 &&
                !char.IsWhiteSpace(current) &&
                char.IsUpper(current) &&
                char.IsLower(normalized[i - 1]))
            {
                label.Add(' ');
            }

            label.Add(current);
        }

        return new string(label.ToArray()).Trim();
    }

    private static string FormatDocumentSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            return trimmed;

        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024m * 1024m):0.#} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024m:0.#} KB";

        return $"{bytes} B";
    }

    private static string HumanizeDocumentTaskType(string taskType)
        => taskType switch
        {
            "LegacyCaseDocument" => "Case Document",
            "LegacyLienDocument" => "Lien Document",
            "LegacyMedicalDocument" => "Medical Document",
            "SellingDocumentReference" => "Supporting Document",
            _ => "Document",
        };
}

public sealed record FundingCompanySaleDocument(
    Guid ReferenceId,
    Guid? DocumentId,
    string FileName,
    string? Category,
    string SizeOrType,
    DateTime CreatedAtUtc);
