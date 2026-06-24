using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Liens.Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyFacilityEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyFacilityEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET list ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFacilityList_returns200()
    {
        var resp = await _client.GetAsync("/facility/list/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFacilityById_returns200()
    {
        var resp = await _client.GetAsync($"/facility/list/{SeedHelper.FacilityId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchFacilitiesV3_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/facility/list/v3",
            new { page = 1, limit = 10 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── CREATE / UPDATE / DELETE ──────────────────────────────────────────────

    [Fact]
    public async Task CreateFacility_returns201()
    {
        var resp = await _client.PostAsJsonAsync("/facility/create", new
        {
            name  = "New Test Clinic",
            city  = "San Francisco",
            state = "CA",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateFacility_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/facility/update", new
        {
            id   = SeedHelper.FacilityId,
            name = "Sunrise Clinic Updated",
            city = "Los Angeles",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteFacility_returns204()
    {
        var createResp = await _client.PostAsJsonAsync("/facility/create",
            new { name = "Temp Facility" });
        var body = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        body.Should().NotBeNull();

        var deleteResp = await _client.DeleteAsync($"/facility/delete/{body!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Contact person CRUD ───────────────────────────────────────────────────

    [Fact]
    public async Task GetContactPerson_returns200()
    {
        var resp = await _client.GetAsync($"/facility/get-contactperson/{SeedHelper.FacilityId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateContactPerson_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/facility/contactperson", new
        {
            facilityId = SeedHelper.FacilityId,
            firstName  = "Bob",
            lastName   = "Technician",
            position   = "Technician",
            email      = "bob@clinic.com",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateContactPerson_returns200()
    {
        var resp = await _client.PostAsJsonAsync("/facility/update-contactperson", new
        {
            id         = SeedHelper.FacilityContactId,
            facilityId = SeedHelper.FacilityId,
            firstName  = "Alice",
            lastName   = "Nurse Updated",
            position   = "Senior Nurse",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteContactPerson_returns204()
    {
        // Create a throwaway contact person to delete.
        var createResp = await _client.PostAsJsonAsync("/facility/contactperson", new
        {
            facilityId = SeedHelper.FacilityId,
            firstName  = "Temp",
            lastName   = "Person",
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        body.Should().NotBeNull();

        var deleteResp = await _client.DeleteAsync(
            $"/facility/delete-contactperson/{body!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetFacilityList_without_auth_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/facility/list/");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record IdResponse(Guid Id);
}
