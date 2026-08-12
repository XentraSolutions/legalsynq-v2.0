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
    public void Company_api_surface_remains_the_expected_twenty_routes()
    {
        var patterns = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => pattern is not null &&
                (pattern.StartsWith("/api/liens/selling/companies", StringComparison.Ordinal) ||
                 pattern is "/api/liens/selling/contacts/export" ||
                 pattern is "/api/liens/selling/lookups/contact-person-types" or
                     "/api/liens/selling/lookups/company-types"))
            .ToList();

        patterns.Should().BeEquivalentTo(new[]
        {
            "/api/liens/selling/lookups/company-types",
            "/api/liens/selling/lookups/contact-person-types",
            "/api/liens/selling/lookups/contact-person-types",
            "/api/liens/selling/companies",
            "/api/liens/selling/companies",
            "/api/liens/selling/companies/export",
            "/api/liens/selling/companies/{companyId:guid}",
            "/api/liens/selling/companies/{companyId:guid}",
            "/api/liens/selling/companies/{companyId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/reassign",
            "/api/liens/selling/companies/{companyId:guid}/reactivate",
            "/api/liens/selling/companies/{companyId:guid}/contacts",
            "/api/liens/selling/contacts/export",
            "/api/liens/selling/companies/{companyId:guid}/contacts/export",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/contacts",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}/reassign",
            "/api/liens/selling/companies/{companyId:guid}/contacts/{contactId:guid}/reactivate",
        });
        patterns.Should().HaveCount(20);
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
            roles.Items.Should().OnlyContain(value => value.IsSystem);
            roleCount += roles.Items.Count;
        }

        roleCount.Should().Be(28);
    }

    [Fact]
    public async Task Custom_contact_person_type_is_scoped_and_assignable_within_its_seller_organization()
    {
        var payload = new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            code = "SettlementCoordinator",
            name = "Settlement Coordinator",
        };
        var idempotencyKey = Guid.CreateVersion7().ToString();

        async Task<HttpResponseMessage> CreateAsync()
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "/api/liens/selling/lookups/contact-person-types");
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(payload);
            return await _client.SendAsync(request);
        }

        using var createResponse = await CreateAsync();
        using var replayResponse = await CreateAsync();
        createResponse.StatusCode.Should().Be(
            HttpStatusCode.Created, await createResponse.Content.ReadAsStringAsync());
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replayResponse.Content.ReadAsStringAsync()).Should().Be(
            await createResponse.Content.ReadAsStringAsync());

        var created = await createResponse.Content.ReadFromJsonAsync<ContactPersonTypeResponse>();
        created.Should().NotBeNull();
        created!.CompanyTypeId.Should().Be(CompanyDirectoryReferenceData.LawFirmId);
        created.Code.Should().Be("SettlementCoordinator");
        created.SortOrder.Should().Be(8);
        created.IsSystem.Should().BeFalse();

        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Custom Role Law",
        });
        var contact = await CreateContactAsync(
            company.Id, created.Id, "Taylor", "Coordinator");
        contact.ContactPersonTypeId.Should().Be(created.Id);
        contact.ContactPersonTypeCode.Should().Be(created.Code);

        var scopedTypes = await _client.GetFromJsonAsync<ItemsEnvelope<ContactPersonTypeResponse>>(
            $"/api/liens/selling/lookups/contact-person-types?companyTypeId={CompanyDirectoryReferenceData.LawFirmId}");
        scopedTypes!.Items.Should().HaveCount(8);
        scopedTypes.Items.Should().ContainSingle(value => value.Id == created.Id);

        SetAuthorization(Guid.CreateVersion7());
        var otherOrgTypes = await _client.GetFromJsonAsync<ItemsEnvelope<ContactPersonTypeResponse>>(
            $"/api/liens/selling/lookups/contact-person-types?companyTypeId={CompanyDirectoryReferenceData.LawFirmId}");
        otherOrgTypes!.Items.Should().HaveCount(7);
        otherOrgTypes.Items.Should().NotContain(value => value.Id == created.Id);

        var otherOrgCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Other Organization Law",
        });
        var blockedContact = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{otherOrgCompany.Id}/contacts",
            new
            {
                contactPersonTypeId = created.Id,
                firstName = "Blocked",
                lastName = "Contact",
            });
        blockedContact.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<LiensDbContext>()
            .ContactPersonTypes.AsNoTracking().SingleAsync(value => value.Id == created.Id);
        persisted.TenantId.Should().Be(SeedHelper.TenantId);
        persisted.OrgId.Should().Be(SeedHelper.OrgId);
        scope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>().Events
            .Where(value => value.EventType == "liens.company.contact_type.created" &&
                            value.EntityId == created.Id.ToString())
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Contact_person_type_creation_validates_code_and_rejects_visible_duplicates()
    {
        var duplicate = await SendMutationAsync(
            HttpMethod.Post,
            "/api/liens/selling/lookups/contact-person-types",
            new
            {
                companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
                code = "attorney",
                name = "Duplicate Attorney",
            });
        var invalidCode = await SendMutationAsync(
            HttpMethod.Post,
            "/api/liens/selling/lookups/contact-person-types",
            new
            {
                companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
                code = "Bad Role!",
                name = "Bad Role",
            });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        invalidCode.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Company_export_returns_filtered_scoped_csv_download()
    {
        var exported = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "=Northstar, Law",
            email = "office@northstar.test",
        });
        await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.MedicalProviderId,
            name = "Other Medical Group",
        });

        var response = await _client.GetAsync(
            $"/api/liens/selling/companies/export?search=Northstar&companyTypeId={CompanyDirectoryReferenceData.LawFirmId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("selling-companies.csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith("Id,CompanyTypeId,CompanyTypeCode,CompanyTypeName,LinkedTenantId,Name");
        csv.Should().Contain(exported.Id.ToString());
        csv.Should().Contain("\"'=Northstar, Law\"");
        csv.Should().NotContain("Other Medical Group");

        SetAuthorization(Guid.CreateVersion7());
        var otherOrganizationCsv = await _client.GetStringAsync("/api/liens/selling/companies/export");
        otherOrganizationCsv.Should().NotContain(exported.Id.ToString());
    }

    [Fact]
    public async Task Contact_person_exports_support_directory_and_company_scopes()
    {
        var northstar = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Northstar Law",
        });
        var other = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.FundingCompanyId,
            name = "Other Capital",
        });
        var northstarContact = await CreateContactAsync(
            northstar.Id,
            CompanyDirectoryReferenceData.ContactPersonTypes
                .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId).Id,
            "Avery",
            "Stone");
        var otherContact = await CreateContactAsync(
            other.Id,
            CompanyDirectoryReferenceData.ContactPersonTypes
                .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.FundingCompanyId).Id,
            "Morgan",
            "Reed");

        var allResponse = await _client.GetAsync("/api/liens/selling/contacts/export?search=Avery");
        allResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        allResponse.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("selling-contact-persons.csv");
        var allCsv = await allResponse.Content.ReadAsStringAsync();
        allCsv.Should().StartWith("Id,CompanyId,CompanyName,CompanyTypeId,CompanyTypeCode");
        allCsv.Should().Contain(northstarContact.Id.ToString());
        allCsv.Should().Contain("Northstar Law");
        allCsv.Should().NotContain(otherContact.Id.ToString());

        var companyResponse = await _client.GetAsync(
            $"/api/liens/selling/companies/{northstar.Id}/contacts/export");
        companyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        companyResponse.Content.Headers.ContentDisposition!.FileNameStar.Should().Be(
            $"selling-company-{northstar.Id:D}-contact-persons.csv");
        var companyCsv = await companyResponse.Content.ReadAsStringAsync();
        companyCsv.Should().Contain(northstarContact.Id.ToString());
        companyCsv.Should().NotContain(otherContact.Id.ToString());
    }

    [Fact]
    public async Task Contact_person_export_by_company_returns_not_found_outside_scope()
    {
        var response = await _client.GetAsync(
            $"/api/liens/selling/companies/{Guid.CreateVersion7()}/contacts/export");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task Company_reassignment_moves_contacts_and_canonical_references_to_same_type_target()
    {
        var source = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Source Law",
        });
        var target = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Target Law",
        });
        var caseManagerRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .Single(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId &&
                             value.Code == "CaseManager");
        var sourceContact = await CreateContactAsync(
            source.Id, caseManagerRole.Id, "Avery", "Stone");

        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = await setupDb.Cases.FindAsync(SeedHelper.CaseId);
            caseEntity!.LinkCanonicalCaseParties(source.Id, sourceContact.Id);
            await setupDb.SaveChangesAsync();
        }

        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{source.Id}/reassign",
            new { targetCompanyId = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<CompanyReassignmentResponse>();
        result.Should().NotBeNull();
        result!.TargetCompanyId.Should().Be(target.Id);
        result.ReassignedContactPersonCount.Should().Be(1);
        result.ReassignedCaseCount.Should().Be(1);
        result.TotalReassignedCount.Should().Be(2);

        using var verificationScope = _factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedContact = await db.CompanyContactPersons.AsNoTracking()
            .SingleAsync(value => value.Id == sourceContact.Id);
        persistedContact.CompanyId.Should().Be(target.Id);
        var persistedCase = await db.Cases.AsNoTracking()
            .SingleAsync(value => value.Id == SeedHelper.CaseId);
        persistedCase.HandlingLawFirmCompanyId.Should().Be(target.Id);
        persistedCase.CaseManagerContactPersonId.Should().Be(sourceContact.Id);
        verificationScope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>().Events
            .Where(value => value.EventType == "liens.company.reassigned" &&
                            value.EntityId == source.Id.ToString())
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Company_reassignment_rejects_target_with_different_company_type()
    {
        var source = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Source Law",
        });
        var target = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.FundingCompanyId,
            name = "Target Funding",
        });

        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{source.Id}/reassign",
            new { targetCompanyId = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contact_reassignment_moves_canonical_usage_and_its_paired_company_reference()
    {
        var sourceCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Source Law",
        });
        var targetCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Target Law",
        });
        var caseManagerRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .Single(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId &&
                             value.Code == "CaseManager");
        var sourceContact = await CreateContactAsync(
            sourceCompany.Id, caseManagerRole.Id, "Source", "Manager");
        var targetContact = await CreateContactAsync(
            targetCompany.Id, caseManagerRole.Id, "Target", "Manager");

        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var caseEntity = await setupDb.Cases.FindAsync(SeedHelper.CaseId);
            caseEntity!.LinkCanonicalCaseParties(sourceCompany.Id, sourceContact.Id);
            await setupDb.SaveChangesAsync();
        }

        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{sourceCompany.Id}/contacts/{sourceContact.Id}/reassign",
            new { targetContactPersonId = targetContact.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<CompanyContactPersonReassignmentResponse>();
        result.Should().NotBeNull();
        result!.TargetContactPersonId.Should().Be(targetContact.Id);
        result.TargetCompanyId.Should().Be(targetCompany.Id);
        result.ReassignedCaseCount.Should().Be(1);
        result.TotalReassignedCount.Should().Be(1);

        using var verificationScope = _factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persistedCase = await db.Cases.AsNoTracking()
            .SingleAsync(value => value.Id == SeedHelper.CaseId);
        persistedCase.CaseManagerContactPersonId.Should().Be(targetContact.Id);
        persistedCase.HandlingLawFirmCompanyId.Should().Be(targetCompany.Id);
        var persistedSource = await db.CompanyContactPersons.AsNoTracking()
            .SingleAsync(value => value.Id == sourceContact.Id);
        persistedSource.CompanyId.Should().Be(sourceCompany.Id);
        verificationScope.ServiceProvider.GetRequiredService<CapturingAuditPublisher>().Events
            .Where(value => value.EventType == "liens.company.contact.reassigned" &&
                            value.EntityId == sourceContact.Id.ToString())
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Contact_reassignment_rejects_target_with_different_role()
    {
        var company = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Role Guard Law",
        });
        var caseManagerRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .Single(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId &&
                             value.Code == "CaseManager");
        var attorneyRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .Single(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId &&
                             value.Code == "Attorney");
        var source = await CreateContactAsync(company.Id, caseManagerRole.Id, "Source", "Manager");
        var target = await CreateContactAsync(company.Id, attorneyRole.Id, "Target", "Attorney");

        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{company.Id}/contacts/{source.Id}/reassign",
            new { targetContactPersonId = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contact_reassignment_rejects_target_with_different_company_type()
    {
        var sourceCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Source Law",
        });
        var targetCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.FundingCompanyId,
            name = "Target Funding",
        });
        var sourceRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId);
        var targetRole = CompanyDirectoryReferenceData.ContactPersonTypes
            .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.FundingCompanyId);
        var source = await CreateContactAsync(sourceCompany.Id, sourceRole.Id, "Source", "Person");
        var target = await CreateContactAsync(targetCompany.Id, targetRole.Id, "Target", "Person");

        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{sourceCompany.Id}/contacts/{source.Id}/reassign",
            new { targetContactPersonId = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reassignment_targets_are_scoped_to_the_authenticated_seller_organization()
    {
        var role = CompanyDirectoryReferenceData.ContactPersonTypes
            .First(value => value.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId);
        var sourceCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Scoped Source Law",
        });
        var sourceContact = await CreateContactAsync(
            sourceCompany.Id, role.Id, "Source", "Contact");

        SetAuthorization(Guid.CreateVersion7());
        var outOfScopeCompany = await CreateCompanyAsync(new
        {
            companyTypeId = CompanyDirectoryReferenceData.LawFirmId,
            name = "Out of Scope Law",
        });
        var outOfScopeContact = await CreateContactAsync(
            outOfScopeCompany.Id, role.Id, "Out", "OfScope");
        SetAuthorization(SeedHelper.OrgId);

        var companyResponse = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{sourceCompany.Id}/reassign",
            new { targetCompanyId = outOfScopeCompany.Id });
        var contactResponse = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{sourceCompany.Id}/contacts/{sourceContact.Id}/reassign",
            new { targetContactPersonId = outOfScopeContact.Id });

        companyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        contactResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task<CompanyContactPersonResponse> CreateContactAsync(
        Guid companyId, Guid contactPersonTypeId, string firstName, string lastName)
    {
        var response = await SendMutationAsync(
            HttpMethod.Post,
            $"/api/liens/selling/companies/{companyId}/contacts",
            new { contactPersonTypeId, firstName, lastName });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CompanyContactPersonResponse>())!;
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
