using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.DTOs;
using Liens.Domain;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Tests.Tests;

public sealed class SellingCompanyEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingCompanyEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        _client = _factory.CreateClient();
        SetAuthorization(SeedHelper.OrgId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Company_api_surface_remains_the_expected_fourteen_routes()
    {
        var patterns = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => pattern is not null &&
                (pattern.StartsWith("/api/liens/selling/companies", StringComparison.Ordinal) ||
                 pattern is "/api/liens/selling/lookups/contact-person-types" or
                     "/api/liens/selling/lookups/company-types"))
            .ToList();

        patterns.Should().BeEquivalentTo(new[]
        {
            "/api/liens/selling/lookups/company-types",
            "/api/liens/selling/lookups/contact-person-types",
            "/api/liens/selling/companies",
            "/api/liens/selling/companies",
            "/api/liens/selling/companies/{companyId:guid}",
            "/api/liens/selling/companies/{companyId:guid}",
            "/api/liens/selling/companies/{companyId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/reactivate",
            "/api/liens/selling/companies/{companyId:guid}/contacts",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/contacts",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}/reactivate",
        });
        patterns.Should().HaveCount(14);
    }

    [Fact]
    public async Task Company_type_and_role_lookups_return_seeded_matrix()
    {
        var typesResponse = await _client.GetFromJsonAsync<ItemsEnvelope<CompanyTypeResponse>>(
            "/api/liens/selling/lookups/company-types");

        typesResponse.Should().NotBeNull();
        typesResponse!.Items.Should().HaveCount(4);
        typesResponse.Items.Select(value => value.Code).Should().Equal(
            "LawFirm", "FundingCompany", "MedicalProvider", "MedicalFacility");

        var roleCount = 0;
        foreach (var type in typesResponse.Items)
        {
            var roles = await _client.GetFromJsonAsync<ItemsEnvelope<ContactPersonTypeResponse>>(
                $"/api/liens/selling/lookups/contact-person-types?companyTypeId={type.Id}");
            roles.Should().NotBeNull();
            roles!.Items.Should().HaveCount(7);
            roles.Items.Should().OnlyContain(value => value.CompanyTypeId == type.Id);
            roleCount += roles.Items.Count;
        }

        roleCount.Should().Be(28);
    }

    [Fact]
    public async Task Company_and_contact_support_scoped_lifecycle()
    {
        var linkedTenantId = Guid.CreateVersion7();
        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            linkedTenantId,
            name = "Northstar Law",
            addressLine1 = "100 Main Street",
            city = "Los Angeles",
            state = "CA",
            postalCode = "90001",
            phone = "+1-555-0100",
            email = "office@northstar.test",
        });

        company.LinkedTenantId.Should().Be(linkedTenantId);
        company.CompanyTypeCode.Should().Be("LawFirm");

        var contactResponse = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{company.Id}/contacts",
            new
            {
                contactPersonTypeId = CompanyDirectoryReferenceData.ContactPersonTypes[0].Id,
                firstName = "Avery",
                lastName = "Stone",
                email = "avery@northstar.test",
            });
        contactResponse.StatusCode.Should().Be(HttpStatusCode.Created, await contactResponse.Content.ReadAsStringAsync());
        var contact = await contactResponse.Content.ReadFromJsonAsync<CompanyContactPersonResponse>();
        contact.Should().NotBeNull();
        contact!.ContactPersonTypeCode.Should().Be("Attorney");

        var deactivateContact = await SendMutationAsync(
            HttpMethod.Delete,
            $"/api/liens/selling/companies/{company.Id}/contacts/{contact.Id}");
        deactivateContact.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivateContact.Content.ReadFromJsonAsync<CompanyContactPersonResponse>())!.IsActive.Should().BeFalse();

        var reactivateContact = await SendMutationAsync(
            HttpMethod.Put,
            $"/api/liens/selling/companies/{company.Id}/contacts/{contact.Id}/reactivate");
        reactivateContact.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reactivateContact.Content.ReadFromJsonAsync<CompanyContactPersonResponse>())!.IsActive.Should().BeTrue();

        var deactivateCompany = await SendMutationAsync(
            HttpMethod.Delete,
            $"/api/liens/selling/companies/{company.Id}");
        deactivateCompany.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivateCompany.Content.ReadFromJsonAsync<CompanyResponse>())!.IsActive.Should().BeFalse();

        var blockedContact = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{company.Id}/contacts",
            new
            {
                contactPersonTypeId = CompanyDirectoryReferenceData.ContactPersonTypes[1].Id,
                firstName = "Jamie",
                lastName = "Park",
            });
        blockedContact.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reactivateCompany = await SendMutationAsync(
            HttpMethod.Put,
            $"/api/liens/selling/companies/{company.Id}/reactivate");
        reactivateCompany.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reactivateCompany.Content.ReadFromJsonAsync<CompanyResponse>())!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Contact_role_must_belong_to_company_type()
    {
        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Role Guard Law",
        });
        var fundingRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.FundingCompanyId);

        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{company.Id}/contacts",
            new
            {
                contactPersonTypeId = fundingRole.Id,
                firstName = "Wrong",
                lastName = "Role",
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Company_access_is_scoped_to_tenant_organization()
    {
        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.MedicalProviderId,
            name = "Scoped Medical Group",
        });
        SetAuthorization(Guid.CreateVersion7());

        var response = await _client.GetAsync($"/api/liens/selling/companies/{company.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(
                Guid.CreateVersion7(), SeedHelper.UserId, SeedHelper.OrgId));
        var crossTenantResponse = await _client.GetAsync($"/api/liens/selling/companies/{company.Id}");
        crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_company_name_within_type_and_scope_returns_conflict()
    {
        var payload = new
        {
            companyTypeId = CompanyDirectoryReferenceData.FundingCompanyId,
            name = "Capital Partners",
        };
        await CreateCompanyAsync(payload);

        var duplicate = await SendMutationAsync(HttpMethod.Post, "/api/liens/selling/companies", new
        {
            companyTypeId = CompanyDirectoryReferenceData.FundingCompanyId,
            name = "  capital partners  ",
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Company_mutation_requires_idempotency_key()
    {
        var response = await _client.PostAsJsonAsync("/api/liens/selling/companies", new
        {
            companyTypeId = CompanyDirectoryReferenceData.MedicalFacilityId,
            name = "No Key Facility",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Company_update_replay_preserves_response_and_publishes_audit_once()
    {
        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.FundingCompanyId,
            name = "Replay Capital",
        });
        var key = Guid.CreateVersion7().ToString();
        var payload = new
        {
            name = "Replay Capital Updated",
            city = "Austin",
            state = "TX",
        };

        async Task<HttpResponseMessage> SendAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"/api/liens/selling/companies/{company.Id}");
            request.Headers.Add("Idempotency-Key", key);
            request.Content = JsonContent.Create(payload);
            return await _client.SendAsync(request);
        }

        using var first = await SendAsync();
        using var replay = await SendAsync();
        var firstBody = await first.Content.ReadAsStringAsync();
        var replayBody = await replay.Content.ReadAsStringAsync();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        replayBody.Should().Be(firstBody);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persisted = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == company.Id);
        persisted.Name.Should().Be(payload.name);
        persisted.City.Should().Be(payload.city);
        scope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>().Events
            .Where(e => e.EventType == "liens.company.updated" && e.EntityId == company.Id.ToString())
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Company_create_requires_sale_create_permission()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateToken(
                SeedHelper.TenantId,
                SeedHelper.UserId,
                [LiensPermissions.LienSaleRead],
                SeedHelper.OrgId));

        var response = await SendMutationAsync(HttpMethod.Post, "/api/liens/selling/companies", new
        {
            companyTypeId = CompanyDirectoryReferenceData.MedicalFacilityId,
            name = "Read Only Facility",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Company_contact_persists_redundant_tenant_scope()
    {
        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.MedicalFacilityId,
            name = "Tenant Scoped Facility",
        });
        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{company.Id}/contacts",
            new
            {
                contactPersonTypeId = CompanyDirectoryReferenceData.ContactPersonTypes
                    .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.MedicalFacilityId).Id,
                firstName = "Taylor",
                lastName = "Reed",
            });
        response.EnsureSuccessStatusCode();
        var contact = await response.Content.ReadFromJsonAsync<CompanyContactPersonResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persisted = await db.CompanyContactPersons.FindAsync(contact!.Id);
        persisted!.TenantId.Should().Be(SeedHelper.TenantId);
    }

    private async Task<CompanyResponse> CreateCompanyAsync(object payload)
    {
        var response = await SendMutationAsync(HttpMethod.Post, "/api/liens/selling/companies", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CompanyResponse>())!;
    }

    private async Task<HttpResponseMessage> SendMutationAsync(
        HttpMethod method, string url, object? payload = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await _client.SendAsync(request);
    }

    private void SetAuthorization(Guid orgId)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTokenHelper.CreateFullAccessToken(
                SeedHelper.TenantId, SeedHelper.UserId, orgId));
    }

    private sealed class ItemsEnvelope<T>
    {
        public List<T> Items { get; init; } = [];
    }
}
