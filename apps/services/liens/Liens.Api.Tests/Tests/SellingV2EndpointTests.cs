using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
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
    public async Task Prepared_lien_confirm_sale_sets_offered_and_submitted_not_sold()
    {
        var lienId = await CreateSellingLienAsync();

        var lienInfo = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/lien-information", new
        {
            sellerStatus = "Pending",
            initialServiceDate = "2026-07-19",
            listingVisibility = "Private",
            notes = "V2 test",
        });
        lienInfo.EnsureSuccessStatusCode();

        var caseInfo = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/case-information", new
        {
            fundingCompanyId = SeedHelper.FundingCompanyId,
            fundingCompanyContactId = SeedHelper.FundingCompanyId,
            caseId = SeedHelper.CaseId,
            createCaseIfMissing = false,
        });
        caseInfo.EnsureSuccessStatusCode();

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
                buyerFundingCompanyId = SeedHelper.FundingCompanyId,
                buyerContactId = SeedHelper.FundingCompanyId,
                askAmount = 1250m,
                listingVisibility = "Private",
            }),
        };
        prepare.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        (await _client.SendAsync(prepare)).EnsureSuccessStatusCode();

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
        persisted!.Status.Should().Be(LienStatus.Offered);
        persisted.SellerStatus.Should().Be(SellingLienStatus.SubmittedForSale);
        persisted.OfferPrice.Should().Be(1250m);
        persisted.SoldAtUtc.Should().BeNull();
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
        var link = verifyDb.SellingBuyerAccessLinks.Single(item => item.LienId == lienId);
        link.BuyerOrgId.Should().Be(buyerOrgId);
        link.BuyerContactId.Should().Be(buyerEmployeeId);
        var replay = verifyDb.SellingIdempotencyRecords.Single(item =>
            item.Route == "/api/liens/selling/liens/{lienId}/confirm-sale" && item.IdempotencyKey == idempotencyKey);
        replay.ResponseBody.Should().NotContain(portalUrl!);
        replay.ResponseBody.Should().NotContain(portalUrl!.Split('/').Last());
        replay.ResponseBody.Should().Contain("\"buyerPortalUrl\":null");
        var notification = verifyScope.ServiceProvider.GetRequiredService<CapturingNotificationPublisher>().Emails.Last();
        notification.Options!.TemplateData!["buyerMessage"].Should().Be("Please review this time-sensitive lien.");
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
        using var decline = new HttpRequestMessage(HttpMethod.Post, $"/api/liens/selling/buyer/liens/{lienId}/decline")
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
    public async Task Intake_writes_are_locked_after_prepare_sale()
    {
        var lienId = await PrepareSellingLienAsync(SeedHelper.FundingCompanyId, SeedHelper.FundingCompanyId);

        var response = await _client.PutAsJsonAsync($"/api/liens/selling/liens/{lienId}/lien-information", new
        {
            sellerStatus = "Pending", initialServiceDate = "2026-07-20", listingVisibility = "Private",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_confirmation_does_not_reserve_the_transition_or_idempotency_key()
    {
        var lienId = await PrepareSellingLienAsync(SeedHelper.FundingCompanyId, SeedHelper.FundingCompanyId);
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
            "Test", "/api/liens/selling/public/{token}", Guid.CreateVersion7().ToString(), DateTime.UtcNow.AddDays(1), SeedHelper.UserId);
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
