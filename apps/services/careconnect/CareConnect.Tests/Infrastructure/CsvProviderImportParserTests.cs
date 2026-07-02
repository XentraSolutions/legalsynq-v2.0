using System.Text;
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
    public async Task ParseAsync_MissingRequiredHeader_ThrowsValidationException()
    {
        const string csv = """
tenantId,firstName,lastName,email,phone,addressLine1,city,state
11111111-1111-1111-1111-111111111111,Jane,Smith,jane@example.com,555-0100,123 Main St,Chicago,IL
""";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, "providers.csv"));
        Assert.Contains("headers", ex.Errors.Keys);
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
}
