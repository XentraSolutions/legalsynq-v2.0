using System.Text;
using System.IO.Compression;
using BuildingBlocks.Exceptions;
using CareConnect.Infrastructure.Imports;
using Xunit;

namespace CareConnect.Tests.Infrastructure;

public class CsvProviderImportParserTests
{
    private readonly CsvProviderImportParser _parser = new();

    [Fact]
    public async Task ParseAsync_ParsesCanonicalAndAliasHeaders()
    {
        const string csv = """
tenantId,firstName,lastName,organization,npiNumber,email,phone,addressLine1,city,state,zip,isActive,acceptingReferrals
 11111111-1111-1111-1111-111111111111 , Jane , Smith , Smith Family Practice ,1234567890, JANE@EXAMPLE.COM ,555-0100,123 Main St,Chicago,il,60601,yes,no
""";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await _parser.ParseAsync(stream, "providers.csv");

        Assert.Equal(1, result.TotalRows);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal(" 11111111-1111-1111-1111-111111111111 ", row.TenantId);
        Assert.Equal(" Jane ", row.FirstName);
        Assert.Equal(" Smith Family Practice ", row.OrganizationName);
        Assert.Equal("1234567890", row.Npi);
        Assert.Equal("60601", row.PostalCode);
        Assert.Equal("yes", row.IsActiveRaw);
        Assert.Equal("no", row.AcceptingReferralsRaw);
    }

    [Fact]
    public async Task ParseAsync_Windows1252CsvWithNonBreakingSpace_DoesNotCorruptAmpersandText()
    {
        // Excel's plain "CSV (Comma delimited)" save format defaults to the system codepage
        // (Windows-1252) rather than UTF-8. A non-breaking space (U+00A0, byte 0xA0 in cp1252,
        // often pasted in from Word/Outlook) force-decoded as UTF-8 turns into a U+FFFD
        // replacement character.
        var organizationName = OrgNameWithNonBreakingSpace();
        var cp1252 = Encoding.GetEncoding(1252);
        var csv = "tenantId,firstName,lastName,organization,email,phone,addressLine1,city,state,zip\n" +
                   $"11111111-1111-1111-1111-111111111111,Jane,Smith,{organizationName},jane@example.com,555-0100,123 Main St,Chicago,IL,60601\n";

        await using var stream = new MemoryStream(cp1252.GetBytes(csv));

        var result = await _parser.ParseAsync(stream, "providers.csv");

        var row = Assert.Single(result.Rows);
        Assert.Equal(organizationName, row.OrganizationName);
        Assert.DoesNotContain('�', row.OrganizationName!);
    }

    [Fact]
    public async Task ParseAsync_MacRomanCsvWithNonBreakingSpace_DoesNotCorruptAmpersandText()
    {
        // Excel-for-Mac's plain "CSV" export defaults to Mac OS Roman, not UTF-8 or Windows-1252.
        // Byte 0xCA is a non-breaking space (U+00A0) in Mac OS Roman but "E-circumflex" (U+00CA)
        // in Windows-1252 -- a fixed single-fallback guess decodes this "successfully" either
        // way, just silently wrong for whichever encoding it guessed incorrectly.
        // PickLegacyEncoding resolves this by preferring whichever candidate turns the stray
        // byte into a plausible autocorrect artifact (a non-breaking space) rather than a
        // random accented letter.
        var organizationName = OrgNameWithNonBreakingSpace();
        var macRoman = Encoding.GetEncoding(10000);
        var csv = "tenantId,firstName,lastName,organization,email,phone,addressLine1,city,state,zip\n" +
                   $"11111111-1111-1111-1111-111111111111,Jane,Smith,{organizationName},jane@example.com,555-0100,123 Main St,Chicago,IL,60601\n";

        await using var stream = new MemoryStream(macRoman.GetBytes(csv));

        var result = await _parser.ParseAsync(stream, "providers.csv");

        var row = Assert.Single(result.Rows);
        Assert.Equal(organizationName, row.OrganizationName);
        Assert.DoesNotContain('\u00CA', row.OrganizationName!);
    }

    // Built with an explicit (char)0xA0 concatenation rather than a non-breaking space typed
    // directly into source, so the test byte can't drift depending on how this file gets edited.
    private static string OrgNameWithNonBreakingSpace()
        => "Spine &" + (char)0xA0 + "Orthopedic Interventionalists";

    [Fact]
    public async Task ParseAsync_ParsesXlsxWorkbookStyleHeadersWithoutTenantId()
    {
        await using var stream = BuildXlsx(
            [
                "Specialty",
                "Medical Facility",
                "Medical Provider",
                "Address 1",
                "Address 2",
                "City",
                "State",
                "ZIP",
                "Email",
                "Phone Number",
                "NPI"
            ],
            [
                "Pain",
                "Precision Pain Center",
                "Dr. Stuart Baird",
                "7380 W. Sahara Ave",
                "Ste. 160",
                "Las Vegas",
                "NV",
                "89117",
                "info@precisionpaincenter.com",
                "702-781-1700",
                "1336383504"
            ],
            [
                "Pain",
                "Precision Pain Center",
                "Dr. Stuart Baird",
                "1000 Wigwam Pkwy Ste. 100",
                "",
                "Henderson",
                "NV",
                "89074",
                "info@precisionpaincenter.com",
                "702-781-1700",
                "1336383504"
            ]);

        var result = await _parser.ParseAsync(stream, "providers.xlsx");

        Assert.Equal(2, result.TotalRows);
        var first = result.Rows[0];
        Assert.Null(first.TenantId);
        Assert.Equal("Pain", first.SpecialtyCodesRaw);
        Assert.Equal("Precision Pain Center", first.FacilityName);
        Assert.Equal("Dr. Stuart Baird", first.ProviderName);
        Assert.Equal("7380 W. Sahara Ave Ste. 160", first.AddressLine1);
        Assert.Equal("89117", first.PostalCode);
        Assert.Equal("1336383504", first.Npi);

        var second = result.Rows[1];
        Assert.Equal("1000 Wigwam Pkwy Ste. 100", second.AddressLine1);
        Assert.Equal("89074", second.PostalCode);
        Assert.Equal("1336383504", second.Npi);
    }

    [Fact]
    public async Task ParseAsync_MissingRequiredHeader_ThrowsValidationException()
    {
        const string csv = """
tenantId,firstName,lastName,email,phone,addressLine1,city
11111111-1111-1111-1111-111111111111,Jane,Smith,jane@example.com,555-0100,123 Main St,Chicago
""";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, "providers.csv"));
        Assert.Contains("headers", ex.Errors.Keys);
    }

    [Fact]
    public async Task ParseAsync_MissingPostalCodeHeader_ParsesMobileProviderRow()
    {
        const string csv = """
tenantId,firstName,lastName,email,phone,addressLine1,city,state,mobile,serviceRadius
11111111-1111-1111-1111-111111111111,Jane,Smith,jane@example.com,555-0100,Greater Chicago Metro,Chicago,IL,true,30
""";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await _parser.ParseAsync(stream, "providers.csv");

        var row = Assert.Single(result.Rows);
        Assert.Null(row.PostalCode);
        Assert.Equal("true", row.IsMobileRaw);
        Assert.Equal("30", row.ServiceRadiusMilesRaw);
    }

    [Fact]
    public async Task ParseAsync_DuplicateCanonicalHeader_ThrowsValidationException()
    {
        const string csv = """
tenantId,firstName,lastName,email,phone,addressLine1,city,state,postalCode,zip
11111111-1111-1111-1111-111111111111,Jane,Smith,jane@example.com,555-0100,123 Main St,Chicago,IL,60601,60601
""";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, "providers.csv"));
        Assert.Contains("headers", ex.Errors.Keys);
    }

    private static MemoryStream BuildXlsx(params string[][] rows)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
</Types>
""");
            WriteEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
""");
            WriteEntry(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Sheet1" sheetId="1" r:id="rId1"/>
  </sheets>
</workbook>
""");
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
</Relationships>
""");
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
        }

        stream.Position = 0;
        return stream;
    }

    private static string BuildWorksheetXml(string[][] rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        builder.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var rowNumber = rowIndex + 1;
            builder.Append($"""<row r="{rowNumber}">""");
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var cellRef = $"{ColumnName(columnIndex)}{rowNumber}";
                var value = System.Security.SecurityElement.Escape(rows[rowIndex][columnIndex]) ?? string.Empty;
                builder.Append($"""<c r="{cellRef}" t="inlineStr"><is><t>{value}</t></is></c>""");
            }
            builder.AppendLine("</row>");
        }

        builder.AppendLine("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static string ColumnName(int zeroBasedColumn)
    {
        var value = zeroBasedColumn + 1;
        var name = string.Empty;
        while (value > 0)
        {
            value--;
            name = (char)('A' + value % 26) + name;
            value /= 26;
        }
        return name;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
