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

    // Legacy single-byte code pages (Windows-1252, Mac OS Roman, ...) aren't registered by
    // default outside .NET Framework. See DetectTextEncoding/PickLegacyEncoding for why we need
    // both and how we choose between them. Declared as the first static field initializer (not
    // an explicit static constructor) because an explicit static constructor's body always runs
    // *after* every static field initializer regardless of source order — LegacyEncodingCandidates
    // below calls Encoding.GetEncoding at class-init time and needs the provider registered first.
    private static readonly bool CodePagesRegistered = RegisterCodePagesProvider();

    private static bool RegisterCodePagesProvider()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return true;
    }

    // Without an explicit static constructor, the compiler marks this type `beforefieldinit`,
    // letting the CLR defer running the field initializers above until the first *static field*
    // of this type is touched — not necessarily before the first instance is constructed. An
    // explicit static constructor (even an empty one) removes that laziness, guaranteeing the
    // CodePagesEncodingProvider registration above has already run by the time any
    // CsvProviderImportParser instance is constructed.
    static CsvProviderImportParser()
    {
    }

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
        ["mobile"] = "isMobile",
        ["ismobile"] = "isMobile",
        ["mobileprovider"] = "isMobile",
        ["serviceradius"] = "serviceRadiusMiles",
        ["serviceradiusmiles"] = "serviceRadiusMiles",
        ["radius"] = "serviceRadiusMiles",
        ["radiusmiles"] = "serviceRadiusMiles",
    };

    private static readonly string[] RequiredHeaders =
    [
        "email", "phone", "addressLine1", "city", "state"
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
        using var parser = new TextFieldParser(new MemoryStream(bytes), DetectTextEncoding(bytes))
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
            GeoPointSource: getField("geoPointSource"),
            IsMobileRaw: getField("isMobile"),
            ServiceRadiusMilesRaw: getField("serviceRadiusMiles"));
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

    /// <summary>
    /// CSV has no built-in encoding marker. Honor a UTF-8/UTF-16 BOM when present; otherwise
    /// try strict UTF-8 first (the vast majority of modern exports) and only fall back to
    /// Windows-1252 — the default codepage for Excel's plain "CSV" save format — if the bytes
    /// aren't valid UTF-8. This avoids silently mangling non-ASCII bytes (smart quotes, em
    /// dashes, non-breaking spaces) into U+FFFD replacement characters.
    /// </summary>
    private static Encoding DetectTextEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        try
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            return PickLegacyEncoding(bytes);
        }
    }

    // Windows-1252 (Excel-on-Windows plain "CSV" export default) and Mac OS Roman (Excel-for-Mac
    // plain "CSV" export default) are both single-byte codepages where every byte 0x00-0xFF maps
    // to *some* character, so neither one ever fails to decode — a wrong guess "succeeds" while
    // silently corrupting the text instead of throwing. A general statistical charset detector
    // doesn't help here either: these import files are overwhelmingly plain ASCII business text
    // with only one or two stray high bytes (almost always a non-breaking space, curly quote, or
    // dash pasted in from Word/Outlook/Excel autocorrect), and that little signal isn't enough
    // for a statistical detector to beat its own "this is just ASCII" default — verified directly
    // against UTF.Unknown (a chardet port), which mis-detects exactly this shape of file.
    // Instead, decode the stray bytes under each candidate and prefer whichever candidate turns
    // them into a plausible autocorrect artifact rather than an arbitrary accented letter — real
    // corruption in real business data comes from those autocorrect substitutions, not from a
    // company name containing a random "Ê".
    private static readonly Encoding[] LegacyEncodingCandidates =
    [
        Encoding.GetEncoding(1252),  // Windows-1252
        Encoding.GetEncoding(10000), // Mac OS Roman
    ];

    private static readonly HashSet<char> CommonTypographyArtifacts =
    [
        ' ', // non-breaking space
        '‘', '’', '“', '”', // curly single/double quotes
        '–', '—', // en dash, em dash
        '…', // ellipsis
        '•', // bullet
    ];

    private static Encoding PickLegacyEncoding(byte[] bytes)
    {
        var best = LegacyEncodingCandidates[0];
        var bestScore = -1;

        foreach (var candidate in LegacyEncodingCandidates)
        {
            var score = 0;
            foreach (var b in bytes)
            {
                if (b < 0x80) continue;
                if (CommonTypographyArtifacts.Contains(candidate.GetString([b])[0]))
                    score++;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
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
