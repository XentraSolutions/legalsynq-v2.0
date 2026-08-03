using System.Text;
using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.VisualBasic.FileIO;

namespace CareConnect.Infrastructure.Imports;

public sealed class CsvProviderImportParser : IProviderImportParser
{
    private const int MaxRows = 2000;

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tenantid"] = "tenantId",
        ["title"] = "title",
        ["providertitle"] = "title",
        ["firstname"] = "firstName",
        ["lastname"] = "lastName",
        ["organizationname"] = "organizationName",
        ["organization"] = "organizationName",
        ["practice"] = "organizationName",
        ["npi"] = "npi",
        ["npinumber"] = "npi",
        ["email"] = "email",
        ["phone"] = "phone",
        ["addressline1"] = "addressLine1",
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
        "tenantId", "firstName", "lastName", "email", "phone", "addressLine1", "city", "state", "postalCode"
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

            rows.Add(new ProviderImportParsedRow(
                RowNumber: rowNumber,
                SourceKey: $"row-{rowNumber}",
                TenantId: GetField("tenantId"),
                Title: GetField("title"),
                FirstName: GetField("firstName"),
                LastName: GetField("lastName"),
                OrganizationName: GetField("organizationName"),
                Npi: GetField("npi"),
                Email: GetField("email"),
                Phone: GetField("phone"),
                AddressLine1: GetField("addressLine1"),
                City: GetField("city"),
                State: GetField("state"),
                PostalCode: GetField("postalCode"),
                IsActiveRaw: GetField("isActive"),
                AcceptingReferralsRaw: GetField("acceptingReferrals"),
                CategoryCodesRaw: GetField("categoryCodes"),
                PrimaryCategoryCode: GetField("primaryCategoryCode"),
                SpecialtyCodesRaw: GetField("specialtyCodes"),
                PrimarySpecialtyCode: GetField("primarySpecialtyCode"),
                LatitudeRaw: GetField("latitude"),
                LongitudeRaw: GetField("longitude"),
                GeoPointSource: GetField("geoPointSource")));

            string? GetField(string canonicalHeader)
            {
                if (!headerMap.TryGetValue(canonicalHeader, out var index)) return null;
                if (index >= fields.Length) return null;
                return fields[index];
            }
        }

        return Task.FromResult(new ProviderImportParseResult(fileName, rows.Count, rows));
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
}
