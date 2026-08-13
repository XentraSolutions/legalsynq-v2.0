using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public sealed class SellingV2EndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public SellingV2EndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_lien_starts_in_pending_without_exposing_seller_draft()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/liens/selling/liens")
        {
            Content = JsonContent.Create(new { sellerStatus = "Pending", source = "Single" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("sellerStatus").GetString().Should().Be(SellingLienStatus.Pending);
        json.RootElement.GetProperty("sellerStatus").GetString().Should().NotBe("Draft");
    }

    [Fact]
    public async Task Save_medical_pricing_persists_multiple_rows_with_unique_task_numbers()
    {
        var lienId = await CreateSellingLienAsync();

        var response = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/medical-pricing", new
        {
            askAmount = 5000m,
            billingAmount = 3000m,
            rows = new[]
            {
                new { medicalCode = "45385", description = "Colonoscopy", billingAmount = 3000m, medicareCost = 675m, targetSaleAmount = 1000m },
                new { medicalCode = "96372", description = "Therapeutic injection", billingAmount = 0m, medicareCost = 476m, targetSaleAmount = 4000m },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var pricingRows = db.ServicingItems
            .Where(item => item.LienId == lienId && item.TaskType == "SellingMedicalPricing")
            .ToList();

        pricingRows.Should().HaveCount(2);
        pricingRows.Select(item => item.TaskNumber).Should().OnlyHaveUniqueItems();
        pricingRows.Select(item => item.TaskNumber).Should().OnlyContain(taskNumber => taskNumber.Length == 36);
        pricingRows.Select(item => item.Description).Should().BeEquivalentTo("45385", "96372");
    }

    [Fact]
    public async Task Pending_lien_confirm_sale_sets_offered_and_submitted_not_sold()
    {
        var (_, buyerContactId) = await SeedConfirmSaleContactsAsync(
            "buyer.prepared-confirm@capital.test",
            "seller.prepared-confirm@smithlaw.test");
        var lienId = await CreateSellingLienAsync();

        var lienInfo = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/lien-information", new
        {
            sellerStatus = "Pending",
            initialServiceDate = "2026-07-19",
            listingVisibility = "Private",
            notes = "V2 test",
        });
        lienInfo.EnsureSuccessStatusCode();

        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var lien = await setupDb.Liens.FindAsync(lienId);
            lien!.AttachCase(SeedHelper.CaseId, SeedHelper.UserId);
            await setupDb.SaveChangesAsync();
        }

        var pricing = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/medical-pricing", new
        {
            askAmount = 1250m,
            billingAmount = 1800m,
            rows = new[] { new { medicalCode = "99213", billingAmount = 600m, medicareCost = 180m, targetSaleAmount = 350m } },
        });
        pricing.EnsureSuccessStatusCode();

        var documents = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/documents", new
        {
            documents = new[] { new { documentId = Guid.CreateVersion7(), documentType = "MedicalBill", displayName = "bill.pdf" } },
        });
        documents.EnsureSuccessStatusCode();

        var prepare = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/prepare-sale")
        {
            Content = JsonContent.Create(new
            {
                buyerContactId,
                askAmount = 1250m,
                listingVisibility = "Private",
            }),
        };
        prepare.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        (await _client.SendAsync(prepare)).EnsureSuccessStatusCode();

        using (var preparedScope = _factory.Services.CreateScope())
        {
            var preparedDb = preparedScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var preparedLien = await preparedDb.Liens.FindAsync(lienId);
            preparedLien!.SellerStatus.Should().Be(SellingLienStatus.Pending);
        }

        var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/confirm-sale")
        {
            Content = JsonContent.Create(new { confirmationAccepted = true, sendBuyerNotification = false }),
        };
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var confirmResponse = await _client.SendAsync(confirm);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK, await confirmResponse.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persisted = await db.Liens.FindAsync(lienId);
        var buyerContact = await db.Contacts.FindAsync(buyerContactId);
        persisted!.Status.Should().Be(LienStatus.Offered);
        persisted.SellerStatus.Should().Be(SellingLienStatus.SubmittedForSale);
        persisted.FundingCompanyId.Should().Be(buyerContact!.OrgId);
        persisted.FundingCompanyContactId.Should().Be(buyerContactId);
        persisted.OfferPrice.Should().Be(1250m);
        persisted.SoldAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Prepare_sale_without_buyer_contact_keeps_pending_when_confirmation_fails()
    {
        var lienId = await CreateSellingLienAsync();

        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/lien-information", new
        {
            sellerStatus = "Pending", initialServiceDate = "2026-07-19", listingVisibility = "Private",
        })).EnsureSuccessStatusCode();

        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var lien = await setupDb.Liens.FindAsync(lienId);
            lien!.AttachCase(SeedHelper.CaseId, SeedHelper.UserId);
            await setupDb.SaveChangesAsync();
        }

        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/medical-pricing", new
        {
            askAmount = 1250m, billingAmount = 1800m,
            rows = new[] { new { medicalCode = "99213", billingAmount = 600m, medicareCost = 180m, targetSaleAmount = 350m } },
        })).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/documents", new
        {
            documents = new[] { new { documentId = Guid.CreateVersion7(), documentType = "MedicalBill", displayName = "bill.pdf" } },
        })).EnsureSuccessStatusCode();

        using var prepare = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/prepare-sale")
        {
            Content = JsonContent.Create(new { buyerContactId = Guid.Empty, askAmount = 1250m, listingVisibility = "Private" }),
        };
        prepare.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        (await _client.SendAsync(prepare)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persisted = await db.Liens.FindAsync(lienId);
        persisted!.SellerStatus.Should().Be(SellingLienStatus.Pending);
        persisted.FundingCompanyId.Should().BeNull();
        persisted.FundingCompanyContactId.Should().BeNull();

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/confirm-sale")
        {
            Content = JsonContent.Create(new { confirmationAccepted = true, sendBuyerNotification = true }),
        };
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());

        var confirmResponse = await _client.SendAsync(confirm);

        confirmResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, await confirmResponse.Content.ReadAsStringAsync());
        await db.Entry(persisted).ReloadAsync();
        persisted.SellerStatus.Should().Be(SellingLienStatus.Pending);
        persisted.Status.Should().Be(LienStatus.Draft);
    }

    [Fact]
    public async Task Seller_lien_detail_does_not_cross_organization_boundary()
    {
        var lienId = await CreateSellingLienAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId, Guid.CreateVersion7()));

        var response = await _client.GetAsync($"/api/liens/selling/liens/{lienId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Seller_lien_detail_includes_funding_contact_and_case_assignments()
    {
        var fundingContactId = Guid.CreateVersion7();
        var caseManagerId = Guid.CreateVersion7();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var fundingContact = Contact.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, ContactType.Lead,
                "Fiona", "Funder", SeedHelper.UserId, email: "fiona@capital-fund.test");
            SetId(fundingContact, fundingContactId);
            var caseManager = Contact.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, ContactType.CaseManager,
                "Casey", "Manager", SeedHelper.UserId, lawFirmId: SeedHelper.LawFirmId);
            SetId(caseManager, caseManagerId);
            db.Contacts.AddRange(fundingContact, caseManager);
            await db.SaveChangesAsync();
        }

        var lienId = await CreateSellingLienAsync();
        var caseInformation = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/case-information", new
        {
            fundingCompanyId = SeedHelper.FundingCompanyId,
            fundingCompanyContactId = fundingContactId,
            handlingLawFirmId = SeedHelper.LawFirmId,
            caseManagerId,
            caseId = SeedHelper.CaseId,
            createCaseIfMissing = false,
        });
        caseInformation.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/api/liens/selling/liens/{lienId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fundingCompany = payload.RootElement.GetProperty("fundingCompany");
        fundingCompany.GetProperty("contactPerson").GetString().Should().Be("Fiona Funder");
        fundingCompany.GetProperty("emailAddress").GetString().Should().Be("fiona@capital-fund.test");
        var caseInfo = payload.RootElement.GetProperty("caseInformation");
        caseInfo.GetProperty("caseManagerId").GetGuid().Should().Be(caseManagerId);
        caseInfo.GetProperty("caseManagerName").GetString().Should().Be("Casey Manager");
        caseInfo.GetProperty("lawFirmId").GetGuid().Should().Be(SeedHelper.LawFirmId);
        caseInfo.GetProperty("lawFirm").GetString().Should().Be("Smith & Associates LLP");
    }

    [Fact]
    public async Task Case_information_accepts_and_reads_back_company_directory_references()
    {
        Company fundingCompany;
        CompanyContactPerson fundingContact;
        Company medicalProvider;
        Company lawFirm;
        CompanyContactPerson caseManager;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var fundingRoleId = CompanyDirectoryReferenceData.ContactPersonTypes
                .First(role => role.CompanyTypeId == CompanyDirectoryReferenceData.FundingCompanyId)
                .Id;
            var caseManagerRoleId = CompanyDirectoryReferenceData.ContactPersonTypes
                .Single(role => role.CompanyTypeId == CompanyDirectoryReferenceData.LawFirmId && role.Code == "CaseManager")
                .Id;

            fundingCompany = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.FundingCompanyId,
                "Directory Capital LLC",
                SeedHelper.UserId);
            fundingContact = CompanyContactPerson.Create(
                SeedHelper.TenantId,
                fundingCompany.Id,
                fundingRoleId,
                "Diana",
                "Funder",
                SeedHelper.UserId,
                email: "diana@directory-capital.test");
            medicalProvider = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.MedicalProviderId,
                "Directory Medical Group",
                SeedHelper.UserId);
            lawFirm = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.LawFirmId,
                "Directory Law LLP",
                SeedHelper.UserId);
            caseManager = CompanyContactPerson.Create(
                SeedHelper.TenantId,
                lawFirm.Id,
                caseManagerRoleId,
                "Cameron",
                "Manager",
                SeedHelper.UserId,
                email: "cameron@directory-law.test");
            db.AddRange(fundingCompany, fundingContact, medicalProvider, lawFirm, caseManager);
            await db.SaveChangesAsync();
        }

        var lienId = await CreateSellingLienAsync();
        var response = await _client.PutAsJsonAsync(
            $"/api/liens/selling/liens/{lienId}/case-information",
            new
            {
                fundingCompanyId = fundingCompany.Id,
                fundingCompanyContactId = fundingContact.Id,
                medicalProviderId = medicalProvider.Id,
                handlingLawFirmId = lawFirm.Id,
                caseManagerId = caseManager.Id,
                createCaseIfMissing = true,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var savedPayload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        savedPayload.RootElement.GetProperty("fundingCompanyId").GetGuid().Should().Be(fundingCompany.Id);
        savedPayload.RootElement.GetProperty("fundingCompanyContactId").GetGuid().Should().Be(fundingContact.Id);
        savedPayload.RootElement.GetProperty("medicalProviderId").GetGuid().Should().Be(medicalProvider.Id);
        savedPayload.RootElement.GetProperty("handlingLawFirmId").GetGuid().Should().Be(lawFirm.Id);
        savedPayload.RootElement.GetProperty("caseManagerId").GetGuid().Should().Be(caseManager.Id);
        var caseId = savedPayload.RootElement.GetProperty("caseId").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var persistedLien = await db.Liens.SingleAsync(item => item.Id == lienId);
            persistedLien.FundingCompanyCompanyId.Should().Be(fundingCompany.Id);
            persistedLien.FundingCompanyContactPersonId.Should().Be(fundingContact.Id);
            persistedLien.MedicalProviderCompanyId.Should().Be(medicalProvider.Id);
            persistedLien.FundingCompanyId.Should().BeNull();
            persistedLien.FundingCompanyContactId.Should().BeNull();

            var persistedCase = await db.Cases.SingleAsync(item => item.Id == caseId);
            persistedCase.HandlingLawFirmCompanyId.Should().Be(lawFirm.Id);
            persistedCase.CaseManagerContactPersonId.Should().Be(caseManager.Id);
        }

        var detailResponse = await _client.GetAsync($"/api/liens/selling/liens/{lienId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
        using var detailPayload = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var companyDetail = detailPayload.RootElement.GetProperty("fundingCompany");
        companyDetail.GetProperty("id").GetGuid().Should().Be(fundingCompany.Id);
        companyDetail.GetProperty("name").GetString().Should().Be("Directory Capital LLC");
        companyDetail.GetProperty("contactPerson").GetString().Should().Be("Diana Funder");
        companyDetail.GetProperty("emailAddress").GetString().Should().Be("diana@directory-capital.test");
        var medicalProviderDetail = detailPayload.RootElement.GetProperty("medicalProvider");
        medicalProviderDetail.GetProperty("id").GetGuid().Should().Be(medicalProvider.Id);
        medicalProviderDetail.GetProperty("name").GetString().Should().Be("Directory Medical Group");
        var caseDetail = detailPayload.RootElement.GetProperty("caseInformation");
        caseDetail.GetProperty("lawFirmId").GetGuid().Should().Be(lawFirm.Id);
        caseDetail.GetProperty("lawFirm").GetString().Should().Be("Directory Law LLP");
        caseDetail.GetProperty("caseManagerId").GetGuid().Should().Be(caseManager.Id);
        caseDetail.GetProperty("caseManagerName").GetString().Should().Be("Cameron Manager");
    }

    [Fact]
    public async Task Case_information_rejects_a_medical_provider_with_the_wrong_company_type()
    {
        Company fundingCompany;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            fundingCompany = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.FundingCompanyId,
                "Not A Medical Provider",
                SeedHelper.UserId);
            db.Companies.Add(fundingCompany);
            await db.SaveChangesAsync();
        }

        var lienId = await CreateSellingLienAsync();
        var response = await _client.PutAsJsonAsync(
            $"/api/liens/selling/liens/{lienId}/case-information",
            new
            {
                fundingCompanyId = SeedHelper.FundingCompanyId,
                medicalProviderId = fundingCompany.Id,
                caseId = SeedHelper.CaseId,
                createCaseIfMissing = false,
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().Contain("medicalProviderId");
    }

    [Fact]
    public async Task Handling_law_firm_lookup_and_save_accept_only_standalone_law_firms()
    {
        var lawFirmContactId = Guid.CreateVersion7();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var lawFirmContact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.LawFirm,
                "Alex",
                "Attorney",
                SeedHelper.UserId,
                lawFirmId: SeedHelper.LawFirmId,
                contactSubtype: ContactSubtype.LawFirmAttorney,
                organization: "Smith & Associates LLP");
            SetId(lawFirmContact, lawFirmContactId);
            db.Contacts.Add(lawFirmContact);
            await db.SaveChangesAsync();
        }

        var lookupResponse = await _client.GetAsync("/api/liens/selling/lookups/law-firms");
        lookupResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await lookupResponse.Content.ReadAsStringAsync());
        using var lookupJson = JsonDocument.Parse(await lookupResponse.Content.ReadAsStringAsync());
        var lawFirmIds = lookupJson.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
        lawFirmIds.Should().Contain(SeedHelper.LawFirmId);
        lawFirmIds.Should().NotContain(lawFirmContactId);

        var lienId = await CreateSellingLienAsync();
        var saveResponse = await _client.PutAsJsonAsync(
            $"/api/liens/selling/liens/{lienId}/case-information",
            new
            {
                fundingCompanyId = SeedHelper.FundingCompanyId,
                handlingLawFirmId = lawFirmContactId,
                caseId = SeedHelper.CaseId,
                createCaseIfMissing = false,
            });

        saveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            await saveResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Confirm_sale_notification_uses_buyer_organization_and_never_persists_portal_capability_in_idempotency_replay()
    {
        var buyerOrgId = Guid.CreateVersion7();
        var buyerCompanyId = Guid.CreateVersion7();
        var buyerEmployeeId = Guid.CreateVersion7();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var buyerCompany = Contact.Create(
                SeedHelper.TenantId, buyerOrgId, ContactType.FundingCompany,
                "Buyer", "Capital", SeedHelper.UserId, organization: "Buyer Capital LLC");
            SetId(buyerCompany, buyerCompanyId);
            var buyerEmployee = Contact.Create(
                SeedHelper.TenantId, buyerOrgId, ContactType.Lead,
                "Erin", "Buyer", SeedHelper.UserId, organization: "Buyer Capital LLC", email: "erin@buyer-capital.test");
            SetId(buyerEmployee, buyerEmployeeId);
            var sellerContact = Contact.Create(
                SeedHelper.TenantId, SeedHelper.OrgId, ContactType.LawFirm,
                "Seller", "Representative", SeedHelper.UserId, organization: "Seller Law LLP", email: "seller@seller-law.test");
            db.Contacts.AddRange(buyerCompany, buyerEmployee, sellerContact);
            await db.SaveChangesAsync();
        }

        var lienId = await PrepareSellingLienAsync(buyerCompanyId, buyerEmployeeId, "Please review this time-sensitive lien.");
        var idempotencyKey = Guid.CreateVersion7().ToString();
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/confirm-sale")
        {
            Content = JsonContent.Create(new { confirmationAccepted = true, sendBuyerNotification = true }),
        };
        confirm.Headers.Add("Idempotency-Key", idempotencyKey);
        var response = await _client.SendAsync(confirm);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var portalUrl = responseJson.RootElement.GetProperty("notification").GetProperty("buyerPortalUrl").GetString();
        portalUrl.Should().NotBeNullOrWhiteSpace();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var link = verifyDb.SellingBuyerAccessLinks.Single(item =>
            item.LienId == lienId &&
            item.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse);
        link.BuyerOrgId.Should().Be(buyerOrgId);
        link.BuyerContactId.Should().Be(buyerEmployeeId);
        var replay = verifyDb.SellingIdempotencyRecords.Single(item =>
            item.Route == "/api/liens/selling/liens/{lienId}/confirm-sale" && item.IdempotencyKey == idempotencyKey);
        replay.ResponseBody.Should().NotContain(portalUrl!);
        replay.ResponseBody.Should().NotContain(portalUrl!.Split('/').Last());
        replay.ResponseBody.Should().Contain("\"buyerPortalUrl\":null");
        var notification = verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().Emails
            .Single(email => email.RecipientEmail == "erin@buyer-capital.test");
        notification.Options!.TemplateData!.Should().NotContainKey("buyerMessage");
        notification.Body.Should().Contain("Itemized Bill / HCFA-1500 Form: bill.pdf");
        notification.Options.HtmlBody.Should().Contain("Itemized Bill / HCFA-1500 Form");
        notification.Options.HtmlBody.Should().Contain("bill.pdf");
        notification.Body.Should().NotContain("Please review this time-sensitive lien.");
        notification.Options.HtmlBody.Should().NotContain("Please review this time-sensitive lien.");
        notification.Options.HtmlBody.Should().NotContain("Seller Message");

        var token = portalUrl!.Split('/').Last();
        using var anonClient = _factory.CreateClient();
        var publicResponse = await anonClient.GetAsync($"/api/liens/selling/public/{token}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK, await publicResponse.Content.ReadAsStringAsync());
        using var publicJson = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync());
        var documents = publicJson.RootElement.GetProperty("documents").EnumerateArray().ToList();
        documents.Should().ContainSingle();
        documents[0].GetProperty("fileName").GetString().Should().Be("bill.pdf");
        documents[0].GetProperty("category").GetString().Should().Be("Itemized Bill / HCFA-1500 Form");
        documents[0].GetProperty("id").GetGuid().Should().NotBeEmpty();
        documents[0].GetProperty("viewUrl").GetString()
            .Should().Be($"/api/lien/api/liens/selling/public/{token}/documents/{documents[0].GetProperty("id").GetGuid():D}/view");
        documents[0].GetProperty("downloadUrl").GetString()
            .Should().Be($"/api/lien/api/liens/selling/public/{token}/documents/{documents[0].GetProperty("id").GetGuid():D}/download");
    }

    [Fact]
    public async Task Prepare_and_confirm_sale_accept_company_directory_buyer_contact()
    {
        Company buyerCompany;
        CompanyContactPerson buyerContact;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var fundingRoleId = CompanyDirectoryReferenceData.ContactPersonTypes
                .First(role => role.CompanyTypeId == CompanyDirectoryReferenceData.FundingCompanyId)
                .Id;
            buyerCompany = Company.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                CompanyDirectoryReferenceData.FundingCompanyId,
                "Canonical Buyer Capital",
                SeedHelper.UserId);
            buyerContact = CompanyContactPerson.Create(
                SeedHelper.TenantId,
                buyerCompany.Id,
                fundingRoleId,
                "Carla",
                "Buyer",
                SeedHelper.UserId,
                email: "carla@canonical-buyer.test");
            var sellerContact = Contact.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                ContactType.LawFirm,
                "Seller",
                "Representative",
                SeedHelper.UserId,
                organization: "Seller Law LLP",
                email: "seller@canonical-buyer.test");
            db.AddRange(buyerCompany, buyerContact, sellerContact);
            await db.SaveChangesAsync();
        }

        var lienId = await PrepareSellingLienAsync(buyerCompany.Id, buyerContact.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var preparedLien = await db.Liens.SingleAsync(item => item.Id == lienId);
            preparedLien.FundingCompanyCompanyId.Should().Be(buyerCompany.Id);
            preparedLien.FundingCompanyContactPersonId.Should().Be(buyerContact.Id);
            preparedLien.FundingCompanyId.Should().BeNull();
            preparedLien.FundingCompanyContactId.Should().BeNull();
        }

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/confirm-sale")
        {
            Content = JsonContent.Create(new { confirmationAccepted = true, sendBuyerNotification = true }),
        };
        confirm.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var confirmResponse = await _client.SendAsync(confirm);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK, await confirmResponse.Content.ReadAsStringAsync());
        using var confirmJson = JsonDocument.Parse(await confirmResponse.Content.ReadAsStringAsync());
        var portalUrl = confirmJson.RootElement.GetProperty("notification").GetProperty("buyerPortalUrl").GetString();
        portalUrl.Should().NotBeNullOrWhiteSpace();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var link = await db.SellingBuyerAccessLinks.SingleAsync(item =>
                item.LienId == lienId &&
                item.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse);
            link.BuyerOrgId.Should().Be(buyerCompany.Id);
            link.BuyerContactId.Should().Be(buyerContact.Id);
            link.BuyerCompanyId.Should().Be(buyerCompany.Id);
            link.BuyerCompanyContactPersonId.Should().Be(buyerContact.Id);
        }

        var token = portalUrl!.Split('/').Last();
        using var anonymousClient = _factory.CreateClient();
        var publicResponse = await anonymousClient.GetAsync($"/api/liens/selling/public/{token}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK, await publicResponse.Content.ReadAsStringAsync());
        using var publicJson = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync());
        var buyer = publicJson.RootElement.GetProperty("buyer");
        buyer.GetProperty("contactName").GetString().Should().Be("Carla Buyer");
        buyer.GetProperty("company").GetString().Should().Be("Canonical Buyer Capital");
        buyer.GetProperty("email").GetString().Should().Be("carla@canonical-buyer.test");
    }

    [Fact]
    public async Task Public_offer_replays_identical_request_and_rejects_body_mismatch()
    {
        var (token, lienId) = await SeedPublicAccessLinkAsync();
        var key = Guid.CreateVersion7().ToString();

        var first = await PostPublicAsync(token, "offers", key, new { offerAmount = 450m, message = "First offer" });
        first.StatusCode.Should().Be(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        var firstBody = await first.Content.ReadAsStringAsync();

        var replay = await PostPublicAsync(token, "offers", key, new { offerAmount = 450m, message = "First offer" });
        replay.StatusCode.Should().Be(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadAsStringAsync()).Should().Be(firstBody);

        var mismatch = await PostPublicAsync(token, "offers", key, new { offerAmount = 451m, message = "Changed offer" });
        mismatch.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.LienOffers.Count(offer => offer.LienId == lienId).Should().Be(1);
    }

    [Fact]
    public async Task Public_decline_replays_identical_request_and_rejects_body_mismatch()
    {
        var (token, lienId) = await SeedPublicAccessLinkAsync();
        var key = Guid.CreateVersion7().ToString();

        var first = await PostPublicAsync(token, "decline", key, new { reason = "Not within mandate" });
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var firstBody = await first.Content.ReadAsStringAsync();

        var replay = await PostPublicAsync(token, "decline", key, new { reason = "Not within mandate" });
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadAsStringAsync()).Should().Be(firstBody);

        var mismatch = await PostPublicAsync(token, "decline", key, new { reason = "Changed reason" });
        mismatch.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        db.SellingBuyerAccessLinks.Single(link => link.LienId == lienId).ResponseStatus.Should().Be("Declined");
    }

    [Fact]
    public async Task Public_response_transition_gate_excludes_competing_accept_and_decline()
    {
        var (token, lienId) = await SeedPublicAccessLinkAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var link = db.SellingBuyerAccessLinks.Single(item => item.LienId == lienId);
            var nullRequestHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("null"))).ToLowerInvariant();
            db.SellingIdempotencyRecords.Add(SellingIdempotencyRecord.Create(
                SeedHelper.TenantId,
                "BuyerLinkResponseTransition",
                link.Id,
                "/api/liens/selling/public/{token}/response",
                "BuyerAccessLink",
                link.Id.ToString(),
                "buyer-response-transition-v1",
                nullRequestHash));
            await db.SaveChangesAsync();
        }

        var accept = await PostPublicAsync(token, "accept", Guid.CreateVersion7().ToString(), new { });
        var decline = await PostPublicAsync(token, "decline", Guid.CreateVersion7().ToString(), new { reason = "Competing response" });

        accept.StatusCode.Should().Be(HttpStatusCode.Conflict);
        decline.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verifyScope = _factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>()
            .SellingBuyerAccessLinks.Single(item => item.LienId == lienId).ResponseStatus.Should().BeNull();
    }

    [Fact]
    public async Task Authenticated_buyer_decline_shares_the_public_response_transition_gate()
    {
        var (token, lienId) = await SeedPublicAccessLinkAsync();
        Guid buyerOrgId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var link = db.SellingBuyerAccessLinks.Single(item => item.LienId == lienId);
            buyerOrgId = link.BuyerOrgId;
            var nullRequestHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("null"))).ToLowerInvariant();
            db.SellingIdempotencyRecords.Add(SellingIdempotencyRecord.Create(
                SeedHelper.TenantId,
                "BuyerLinkResponseTransition",
                link.Id,
                "/api/liens/selling/public/{token}/response",
                "BuyerAccessLink",
                link.Id.ToString(),
                "buyer-response-transition-v1",
                nullRequestHash));
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId, buyerOrgId));
        var accept = await PostPublicAsync(token, "accept", Guid.CreateVersion7().ToString(), new { });
        using var decline = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/buyer/liens/by-lien/{lienId}/decline")
        {
            Content = JsonContent.Create(new { reason = "Competing authenticated response" }),
        };
        decline.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var declineResponse = await _client.SendAsync(decline);

        accept.StatusCode.Should().Be(HttpStatusCode.Conflict);
        declineResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verifyScope = _factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>()
            .SellingBuyerAccessLinks.Single(item => item.LienId == lienId).ResponseStatus.Should().BeNull();
    }

    [Fact]
    public void Buyer_response_transition_subject_type_fits_the_persisted_column()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var maxLength = db.Model.FindEntityType(typeof(SellingIdempotencyRecord))!
            .FindProperty(nameof(SellingIdempotencyRecord.SubjectType))!
            .GetMaxLength();

        "BuyerLinkResponseTransition".Length.Should().BeLessThanOrEqualTo(maxLength!.Value);
    }

    [Fact]
    public async Task Lien_transition_gate_excludes_competing_public_accept_and_seller_withdraw()
    {
        var (token, lienId) = await SeedPublicAccessLinkAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var nullRequestHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("null"))).ToLowerInvariant();
            db.SellingIdempotencyRecords.Add(SellingIdempotencyRecord.Create(
                SeedHelper.TenantId,
                "LienStateTransition",
                lienId,
                "/api/liens/selling/liens/{lienId}/state-transition",
                "Lien",
                lienId.ToString(),
                "lien-state-transition-v1",
                nullRequestHash));
            await db.SaveChangesAsync();
        }

        var accept = await PostPublicAsync(token, "accept", Guid.CreateVersion7().ToString(), new { });
        using var withdraw = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/withdraw-sale")
        {
            Content = JsonContent.Create(new { reason = "Competing seller action" }),
        };
        withdraw.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var withdrawResponse = await _client.SendAsync(withdraw);

        accept.StatusCode.Should().Be(HttpStatusCode.Conflict);
        withdrawResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verifyScope = _factory.Services.CreateScope();
        var lien = verifyScope.ServiceProvider.GetRequiredService<LiensDbContext>().Liens.Single(item => item.Id == lienId);
        lien.Status.Should().Be(LienStatus.Offered);
        lien.SellerStatus.Should().Be(SellingLienStatus.SubmittedForSale);
    }

    [Fact]
    public async Task Contact_case_reassignment_denies_cross_organization_target()
    {
        var sourceId = Guid.CreateVersion7();
        var targetId = Guid.CreateVersion7();
        var caseId = Guid.CreateVersion7();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var source = Contact.Create(SeedHelper.TenantId, SeedHelper.OrgId, ContactType.CaseManager, "Source", "Manager", SeedHelper.UserId);
            SetId(source, sourceId);
            var target = Contact.Create(SeedHelper.TenantId, Guid.CreateVersion7(), ContactType.CaseManager, "Other", "Manager", SeedHelper.UserId);
            SetId(target, targetId);
            var caseEntity = Case.Create(SeedHelper.TenantId, SeedHelper.OrgId, $"CASE-{Guid.CreateVersion7():N}"[..15], "Client", "Name", SeedHelper.UserId, notes: $"caseManagerId={sourceId}");
            SetId(caseEntity, caseId);
            db.Contacts.AddRange(source, target);
            db.Cases.Add(caseEntity);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync($"/api/liens/contacts/{sourceId}/reassign-cases", new
        {
            targetContactId = targetId,
            relationshipType = "CaseManager",
            scope = "Selected",
            caseIds = new[] { caseId },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Documents_endpoint_rejects_unavailable_or_foreign_document_reference()
    {
        var lienId = await CreateSellingLienAsync();
        var documentId = Guid.CreateVersion7();
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CapturingSellingDocumentReferenceValidator>()
                .DeniedDocumentIds.Add(documentId);
        }

        var response = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/documents", new
        {
            documents = new[] { new { documentId, documentType = "MedicalBill" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Documents_endpoint_saves_required_and_supporting_documents_with_unique_task_numbers()
    {
        var lienId = await CreateSellingLienAsync();
        var documents = new[]
        {
            new { documentId = Guid.CreateVersion7(), documentType = "MedicalBill", displayName = "bill.pdf" },
            new { documentId = Guid.CreateVersion7(), documentType = "MedicalRecord", displayName = "record.pdf" },
            new { documentId = Guid.CreateVersion7(), documentType = "PoliceReport", displayName = "report.pdf" },
        };

        var response = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/documents", new { documents });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var savedReferences = db.ServicingItems
            .Where(item => item.LienId == lienId && item.TaskType == "SellingDocumentReference")
            .ToList();
        savedReferences.Should().HaveCount(documents.Length);
        savedReferences.Select(item => item.TaskNumber).Should().OnlyHaveUniqueItems();
        savedReferences.Should().OnlyContain(item => item.TaskNumber.StartsWith("SDR-") && item.TaskNumber.Length == 36);
    }

    [Fact]
    public async Task Intake_writes_remain_available_after_prepare_sale_until_confirmation()
    {
        var lienId = await PrepareSellingLienAsync(SeedHelper.FundingCompanyId, SeedHelper.FundingCompanyId);

        var response = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/lien-information", new
        {
            sellerStatus = "Pending", initialServiceDate = "2026-07-20", listingVisibility = "Private",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var persisted = await db.Liens.FindAsync(lienId);
        persisted!.SellerStatus.Should().Be(SellingLienStatus.Pending);
        persisted.InitialServiceDate.Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public async Task Invalid_confirmation_does_not_reserve_the_transition_or_idempotency_key()
    {
        var (buyerCompanyId, buyerContactId) = await SeedConfirmSaleContactsAsync(
            "buyer.invalid-confirm@capital.test",
            "seller.invalid-confirm@smithlaw.test");
        var lienId = await PrepareSellingLienAsync(buyerCompanyId, buyerContactId);
        var key = Guid.CreateVersion7().ToString();

        using (var invalid = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/confirm-sale")
        {
            Content = JsonContent.Create(new { confirmationAccepted = false, sendBuyerNotification = false }),
        })
        {
            invalid.Headers.Add("Idempotency-Key", key);
            (await _client.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using var valid = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/confirm-sale")
        {
            Content = JsonContent.Create(new { confirmationAccepted = true, sendBuyerNotification = false }),
        };
        valid.Headers.Add("Idempotency-Key", key);
        (await _client.SendAsync(valid)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Prepare_sale_preserves_internal_notes_and_exposes_buyer_message_only_to_seller_detail()
    {
        var lienId = await PrepareSellingLienAsync(SeedHelper.FundingCompanyId, SeedHelper.FundingCompanyId, "Buyer-only review message");

        var response = await _client.GetAsync($"/api/liens/selling/liens/{lienId}");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("lienInformation").GetProperty("buyerMessage").GetString()
            .Should().Be("Buyer-only review message");
    }

    private async Task<Guid> PrepareSellingLienAsync(Guid buyerCompanyId, Guid buyerContactId, string? messageToBuyer = null)
    {
        var lienId = await CreateSellingLienAsync();
        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/lien-information", new
        {
            sellerStatus = "Pending", initialServiceDate = "2026-07-19", listingVisibility = "Private",
        })).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/case-information", new
        {
            fundingCompanyId = SeedHelper.FundingCompanyId,
            fundingCompanyContactId = SeedHelper.FundingCompanyId,
            caseId = SeedHelper.CaseId,
        })).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/medical-pricing", new
        {
            askAmount = 1250m, billingAmount = 1800m,
            rows = new[] { new { medicalCode = "99213", billingAmount = 600m, medicareCost = 180m, targetSaleAmount = 350m } },
        })).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/documents", new
        {
            documents = new[] { new { documentId = Guid.CreateVersion7(), documentType = "MedicalBill", displayName = "bill.pdf" } },
        })).EnsureSuccessStatusCode();
        using var prepare = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/liens/{lienId}/prepare-sale")
        {
            Content = JsonContent.Create(new { buyerFundingCompanyId = buyerCompanyId, buyerContactId, askAmount = 1250m, listingVisibility = "Private", messageToBuyer }),
        };
        prepare.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        (await _client.SendAsync(prepare)).EnsureSuccessStatusCode();
        return lienId;
    }

    private async Task<(Guid BuyerCompanyId, Guid BuyerContactId)> SeedConfirmSaleContactsAsync(
        string buyerEmail,
        string sellerEmail)
    {
        var buyerOrgId = Guid.CreateVersion7();
        var buyerCompanyId = Guid.CreateVersion7();
        var buyerContactId = Guid.CreateVersion7();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var buyerCompany = Contact.Create(
            SeedHelper.TenantId,
            buyerOrgId,
            ContactType.FundingCompany,
            "Buyer",
            "Capital",
            SeedHelper.UserId,
            organization: "Buyer Capital LLC");
        SetId(buyerCompany, buyerCompanyId);

        var buyerContact = Contact.Create(
            SeedHelper.TenantId,
            buyerOrgId,
            ContactType.Lead,
            "Buyer",
            "Reviewer",
            SeedHelper.UserId,
            organization: "Buyer Capital LLC",
            email: buyerEmail);
        SetId(buyerContact, buyerContactId);

        var sellerContact = Contact.Create(
            SeedHelper.TenantId,
            SeedHelper.OrgId,
            ContactType.LawFirm,
            "Seller",
            "Representative",
            SeedHelper.UserId,
            organization: "Seller Law LLP",
            email: sellerEmail);

        db.Contacts.AddRange(buyerCompany, buyerContact, sellerContact);
        await db.SaveChangesAsync();

        return (buyerCompanyId, buyerContactId);
    }

    private async Task<(string Token, Guid LienId)> SeedPublicAccessLinkAsync()
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        var buyerOrgId = Guid.CreateVersion7();
        var buyerContactId = Guid.CreateVersion7();
        var lienId = Guid.CreateVersion7();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
        var buyer = Contact.Create(SeedHelper.TenantId, buyerOrgId, ContactType.FundingCompany, "Public", "Buyer", SeedHelper.UserId, email: "public.buyer@test.local");
        SetId(buyer, buyerContactId);
        var lien = Lien.Create(SeedHelper.TenantId, SeedHelper.OrgId, $"PUB-{Guid.CreateVersion7():N}"[..15], LienType.MedicalLien, 900m, SeedHelper.UserId);
        SetId(lien, lienId);
        lien.ListForSale(450m, SeedHelper.UserId);
        var accessLink = SellingBuyerAccessLink.Create(
            SeedHelper.TenantId, lienId, SeedHelper.OrgId, buyerOrgId, buyerContactId, token,
            SellingAccessLinkPurposes.ConfirmSaleBuyerResponse, "/api/liens/selling/public/{token}", Guid.CreateVersion7().ToString(), DateTime.UtcNow.AddDays(1), SeedHelper.UserId);
        db.Contacts.Add(buyer);
        db.Liens.Add(lien);
        db.SellingBuyerAccessLinks.Add(accessLink);
        await db.SaveChangesAsync();
        return (token, lienId);
    }

    private async Task<HttpResponseMessage> PostPublicAsync(string token, string action, string idempotencyKey, object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/public/{token}/{action}")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(message);
    }

    private static void SetId<T>(T entity, Guid id) where T : class
        => typeof(T).GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.SetValue(entity, id);

    private async Task<Guid> CreateSellingLienAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/liens/selling/liens")
        {
            Content = JsonContent.Create(new { sellerStatus = "Pending", source = "Single" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("lienId").GetGuid();
    }
}
