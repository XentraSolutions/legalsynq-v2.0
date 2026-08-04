using System.Text;
using System.IO.Compression;
using System.Xml.Linq;
using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.VisualBasic.FileIO;

namespace CareConnect.Infrastructure.Imports;

public sealed class CsvProviderImportParser : IProviderImportParser
{
    private const int MaxRows = 2000;
    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DocumentRelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tenantid"] = "tenantId",
        ["title"] = "title",
        ["providertitle"] = "title",
        ["medicalprovider"] = "providerName",
        ["providername"] = "providerName",
        ["medicalfacility"] = "facilityName",
        ["facility"] = "facilityName",
        ["facilityname"] = "facilityName",
        ["firstname"] = "firstName",
        ["lastname"] = "lastName",
        ["organizationname"] = "organizationName",
        ["organization"] = "organizationName",
        ["practice"] = "organizationName",
        ["npi"] = "npi",
        ["npinumber"] = "npi",
        ["email"] = "email",
        ["phone"] = "phone",
        ["phonenumber"] = "phone",
        ["addressline1"] = "addressLine1",
        ["address1"] = "addressLine1",
        ["addressline2"] = "addressLine2",
        ["address2"] = "addressLine2",
        ["city"] = "city",
        ["state"] = "state",
        ["postalcode"] = "postalCode",
        ["zip"] = "postalCode",
        ["zipcode"] = "postalCode",
        ["isactive"] = "isActive",
        ["acceptingreferrals"] = "acceptingReferrals",
        ["category"] = "categoryCodes",
        ["categories"] = "categoryCodes",
        ["categorycode"] = "categoryCodes",
        ["categorycodes"] = "categoryCodes",
        ["providertype"] = "categoryCodes",
        ["providertypes"] = "categoryCodes",
        ["primarycategory"] = "primaryCategoryCode",
        ["primarycategorycode"] = "primaryCategoryCode",
        ["primaryprovidertype"] = "primaryCategoryCode",
        ["specialty"] = "specialtyCodes",
        ["specialties"] = "specialtyCodes",
        ["specialtycode"] = "specialtyCodes",
        ["specialtycodes"] = "specialtyCodes",
        ["providerspecialty"] = "specialtyCodes",
        ["providerspecialties"] = "specialtyCodes",
        ["primaryspecialty"] = "primarySpecialtyCode",
        ["primaryspecialtycode"] = "primarySpecialtyCode",
        ["latitude"] = "latitude",
        ["lat"] = "latitude",
        ["longitude"] = "longitude",
        ["lng"] = "longitude",
        ["lon"] = "longitude",
        ["geopointsource"] = "geoPointSource",
        ["geosource"] = "geoPointSource",
        ["coordinatesource"] = "geoPointSource",
    };

    private static readonly string[] RequiredHeaders =
    [
        "email", "phone", "addressLine1", "city", "state", "postalCode"
    ];

    public Task<ProviderImportParseResult> ParseAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (bytes.Length == 0)
            throw new ValidationException("Validation failed.",
                new() { ["file"] = ["The import file is empty."] });

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ParseXlsx(bytes, fileName, ct));

        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Validation failed.",
                new() { ["file"] = ["Only CSV or XLSX files are supported for provider imports."] });

        return Task.FromResult(ParseCsv(bytes, fileName, ct));
    }

    private static ProviderImportParseResult ParseCsv(byte[] bytes, string fileName, CancellationToken ct)
    {
        using var parser = new TextFieldParser(new MemoryStream(bytes), Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
            throw new ValidationException("Validation failed.",
                new() { ["file"] = ["The import file is empty."] });

        string[]? rawHeaders;
        try
        {
            rawHeaders = parser.ReadFields();
        }
        catch (MalformedLineException ex)
        {
            throw new ValidationException("Validation failed.",
                new() { ["file"] = [$"The CSV header row is malformed: {ex.Message}"] });
        }

        if (rawHeaders is null || rawHeaders.Length == 0)
            throw new ValidationException("Validation failed.",
                new() { ["file"] = ["The import file is missing a header row."] });

        var headerMap = BuildHeaderMap(rawHeaders);
        var rows = new List<ProviderImportParsedRow>();
        var rowNumber = 1;

        while (!parser.EndOfData)
        {
            ct.ThrowIfCancellationRequested();
            rowNumber++;

            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException ex)
            {
                throw new ValidationException("Validation failed.",
                    new() { ["file"] = [$"The CSV row {rowNumber} is malformed: {ex.Message}"] });
            }

            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
                continue;

            if (rows.Count >= MaxRows)
                throw new ValidationException("Validation failed.",
                    new() { ["file"] = [$"The import file exceeds the maximum of {MaxRows} rows."] });

            rows.Add(CreateParsedRow(rowNumber, GetField));

            string? GetField(string canonicalHeader)
            {
                if (!headerMap.TryGetValue(canonicalHeader, out var index)) return null;
                if (index >= fields.Length) return null;
                return fields[index];
            }
        }

        return new ProviderImportParseResult(fileName, rows.Count, rows);
    }

    private static ProviderImportParseResult ParseXlsx(byte[] bytes, string fileName, CancellationToken ct)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var worksheet = ResolveFirstWorksheetEntry(archive);
        var sharedStrings = ReadSharedStrings(archive);

        using var sheetStream = worksheet.Open();
        var document = XDocument.Load(sheetStream);
        var rows = new List<ProviderImportParsedRow>();
        Dictionary<string, int>? headerMap = null;

        foreach (var row in document.Descendants(SpreadsheetNs + "row"))
        {
            ct.ThrowIfCancellationRequested();

            var cellValues = new Dictionary<int, string>();
            var fallbackColumn = 0;

            foreach (var cell in row.Elements(SpreadsheetNs + "c"))
            {
                var column = TryGetColumnIndex((string?)cell.Attribute("r")) ?? fallbackColumn;
                cellValues[column] = ReadCellText(cell, sharedStrings);
                fallbackColumn = column + 1;
            }

            if (cellValues.Count == 0 || cellValues.Values.All(string.IsNullOrWhiteSpace))
                continue;

            var rowNumber = int.TryParse((string?)row.Attribute("r"), out var parsedRowNumber)
                ? parsedRowNumber
                : rows.Count + 2;

            if (headerMap is null)
            {
                var maxColumn = cellValues.Keys.Max();
                var headers = Enumerable.Range(0, maxColumn + 1)
                    .Select(column => cellValues.TryGetValue(column, out var value) ? value : string.Empty)
                    .ToArray();
                headerMap = BuildHeaderMap(headers);
                continue;
            }

            if (rows.Count >= MaxRows)
                throw new ValidationException("Validation failed.",
                    new() { ["file"] = [$"The import file exceeds the maximum of {MaxRows} rows."] });

            rows.Add(CreateParsedRow(rowNumber, GetField));

            string? GetField(string canonicalHeader)
            {
                if (!headerMap.TryGetValue(canonicalHeader, out var index)) return null;
                return cellValues.TryGetValue(index, out var value) ? value : null;
            }
        }

        if (headerMap is null)
            throw new ValidationException("Validation failed.",
                new() { ["file"] = ["The import file is missing a header row."] });

        return new ProviderImportParseResult(fileName, rows.Count, rows);
    }

    private static ProviderImportParsedRow CreateParsedRow(int rowNumber, Func<string, string?> getField)
    {
        return new ProviderImportParsedRow(
            RowNumber: rowNumber,
            SourceKey: $"row-{rowNumber}",
            TenantId: getField("tenantId"),
            Title: getField("title"),
            ProviderName: getField("providerName"),
            FacilityName: getField("facilityName"),
            FirstName: getField("firstName"),
            LastName: getField("lastName"),
            OrganizationName: getField("organizationName"),
            Npi: getField("npi"),
            Email: getField("email"),
            Phone: getField("phone"),
            AddressLine1: CombineAddressLines(getField("addressLine1"), getField("addressLine2")),
            City: getField("city"),
            State: getField("state"),
            PostalCode: getField("postalCode"),
            IsActiveRaw: getField("isActive"),
            AcceptingReferralsRaw: getField("acceptingReferrals"),
            CategoryCodesRaw: getField("categoryCodes"),
            PrimaryCategoryCode: getField("primaryCategoryCode"),
            SpecialtyCodesRaw: getField("specialtyCodes"),
            PrimarySpecialtyCode: getField("primarySpecialtyCode"),
            LatitudeRaw: getField("latitude"),
            LongitudeRaw: getField("longitude"),
            GeoPointSource: getField("geoPointSource"));
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] rawHeaders)
    {
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();

        for (var i = 0; i < rawHeaders.Length; i++)
        {
            var normalized = NormalizeHeader(rawHeaders[i]);
            if (normalized.Length == 0) continue;

            if (!HeaderAliases.TryGetValue(normalized, out var canonical))
                continue;

            if (!headerMap.TryAdd(canonical, i))
                duplicates.Add(canonical);
        }

        if (duplicates.Count > 0)
            throw new ValidationException("Validation failed.",
                new() { ["headers"] = [$"Duplicate headers are not allowed: {string.Join(", ", duplicates.Distinct(StringComparer.OrdinalIgnoreCase))}."] });

        var missing = RequiredHeaders.Where(required => !headerMap.ContainsKey(required)).ToArray();
        if (missing.Length > 0)
            throw new ValidationException("Validation failed.",
                new() { ["headers"] = [$"Missing required headers: {string.Join(", ", missing)}."] });

        return headerMap;
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string? CombineAddressLines(string? addressLine1, string? addressLine2)
    {
        var first = NormalizeOptional(addressLine1);
        var second = NormalizeOptional(addressLine2);
        return (first, second) switch
        {
            (null, null) => null,
            (not null, null) => first,
            (null, not null) => second,
            _ => $"{first} {second}"
        };
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ZipArchiveEntry ResolveFirstWorksheetEntry(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var workbookRelsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (workbookEntry is not null && workbookRelsEntry is not null)
        {
            using var workbookStream = workbookEntry.Open();
            using var relsStream = workbookRelsEntry.Open();
            var workbook = XDocument.Load(workbookStream);
            var rels = XDocument.Load(relsStream);
            var firstSheet = workbook.Descendants(SpreadsheetNs + "sheet").FirstOrDefault();
            var relId = (string?)firstSheet?.Attribute(DocumentRelationshipsNs + "id");

            if (!string.IsNullOrWhiteSpace(relId))
            {
                var target = rels.Descendants(PackageRelationshipsNs + "Relationship")
                    .Where(rel => string.Equals((string?)rel.Attribute("Id"), relId, StringComparison.Ordinal))
                    .Select(rel => (string?)rel.Attribute("Target"))
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(target))
                {
                    var worksheetPath = ResolveWorkbookRelativePath(target);
                    var entry = archive.GetEntry(worksheetPath);
                    if (entry is not null)
                        return entry;
                }
            }
        }

        return archive.Entries
            .Where(entry =>
                entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new ValidationException("Validation failed.",
                new() { ["file"] = ["The XLSX file does not contain a worksheet."] });
    }

    private static string ResolveWorkbookRelativePath(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"xl/{normalized}";
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is null) return [];

        using var stream = sharedStringsEntry.Open();
        var document = XDocument.Load(stream);
        return document
            .Descendants(SpreadsheetNs + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNs + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string ReadCellText(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        var value = (string?)cell.Element(SpreadsheetNs + "v");

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value, out var sharedStringIndex) &&
            sharedStringIndex >= 0 &&
            sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            return string.Concat(cell.Descendants(SpreadsheetNs + "t").Select(text => text.Value));

        if (string.Equals(type, "b", StringComparison.OrdinalIgnoreCase))
            return value == "1" ? "true" : "false";

        return value ?? string.Concat(cell.Descendants(SpreadsheetNs + "t").Select(text => text.Value));
    }

    private static int? TryGetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference)) return null;

        var column = 0;
        var hasLetters = false;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch)) break;
            hasLetters = true;
            column = (column * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return hasLetters ? column - 1 : null;
    }
}
