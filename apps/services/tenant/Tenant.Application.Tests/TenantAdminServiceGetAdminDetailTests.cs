using System.Reflection;
using System.Runtime.CompilerServices;
using BuildingBlocks.Commerce;
using Contracts.Commerce;
using Microsoft.Extensions.Logging.Abstractions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Application.Services;
using Tenant.Domain;
using Xunit;

namespace Tenant.Application.Tests;

/// <summary>
/// Unit tests for <see cref="TenantAdminService.GetAdminDetailAsync"/>.
///
/// Covers the full aggregation path — branding, entitlements, domains,
/// capabilities, settings, and Identity compat read-through — without
/// requiring a live database.  All repositories and adapters are replaced
/// by in-memory stubs.
///
/// Task #170: Make sure tenant detail loads correctly with automated tests.
/// </summary>
public class TenantAdminServiceGetAdminDetailTests
{
    // ── Factory helper ────────────────────────────────────────────────────────

    private static TenantAdminService BuildService(
        ITenantRepository?          tenantRepo           = null,
        IBrandingRepository?        brandingRepo         = null,
        IEntitlementRepository?     entitlementRepo      = null,
        IDomainRepository?          domainRepo           = null,
        ICapabilityRepository?      capabilityRepo       = null,
        ISettingRepository?         settingRepo          = null,
        IIdentityCompatAdapter?     identityCompat       = null,
        IIdentityProvisioningAdapter? identityProvisioning = null)
    {
        return new TenantAdminService(
            tenantRepo           ?? new StubTenantRepository(),
            brandingRepo         ?? new StubBrandingRepository(),
            entitlementRepo      ?? new StubEntitlementRepository(),
            domainRepo           ?? new StubDomainRepository(),
            capabilityRepo       ?? new StubCapabilityRepository(),
            settingRepo          ?? new StubSettingRepository(),
            identityCompat       ?? new StubIdentityCompatAdapter(),
            identityProvisioning ?? new StubIdentityProvisioningAdapter(),
            new StubCommerceLifecycleNotifier(),
            NullLogger<TenantAdminService>.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsNull_WhenTenantNotFound()
    {
        var svc = BuildService(tenantRepo: new StubTenantRepository(tenant: null));

        var result = await svc.GetAdminDetailAsync(Guid.CreateVersion7());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsCoreFields_WhenTenantFound()
    {
        var tenant = Domain.Tenant.Create(
            code:         "acme",
            displayName:  "Acme Corp",
            subdomain:    "acme",
            supportEmail: "support@acme.com",
            timeZone:     "America/New_York",
            locale:       "en-US");

        var svc    = BuildService(
            tenantRepo: new StubTenantRepository(tenant),
            identityCompat: new StubIdentityCompatAdapter(
                snapshot: new TenantIdentityCompatSnapshot(
                    Type: "LIEN_OWNER",
                    SessionTimeoutMinutes: null,
                    Hostname: "acme.legalsynq.net")));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(tenant.Id,          result.Id);
        Assert.Equal("acme",             result.Code);
        Assert.Equal("Acme Corp",        result.DisplayName);
        Assert.Equal("Active",           result.Status);
        Assert.True(result.IsActive);
        Assert.Equal("LIEN_OWNER",       result.Type);
        Assert.Equal("acme",             result.Subdomain);
        Assert.Equal("https://acme.legalsynq.net", result.Url);
        Assert.Equal("support@acme.com", result.Email);
    }

    [Fact]
    public async Task ListAdminAsync_UsesIdentityTypeAndHostname_WhenAvailable()
    {
        var tenant = Domain.Tenant.Create(
            code: "acme",
            displayName: "Acme Corp",
            subdomain: "acme");

        var svc = BuildService(
            tenantRepo: new StubTenantRepository(
                tenant,
                items: [tenant],
                total: 1),
            identityCompat: new StubIdentityCompatAdapter(
                snapshot: new TenantIdentityCompatSnapshot(
                    Type: "PROVIDER",
                    SessionTimeoutMinutes: null,
                    Hostname: "portal.acme.example")));

        var (items, total) = await svc.ListAdminAsync(1, 20);

        var item = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal("PROVIDER", item.Type);
        Assert.Equal("https://portal.acme.example", item.Url);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsBrandingSummary_WhenBrandingExists()
    {
        var tenant   = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var branding = TenantBranding.Create(tenant.Id);
        branding.Update(
            brandName:      "Acme Brand",
            primaryColor:   "#001122",
            logoDocumentId: Guid.CreateVersion7());

        var svc    = BuildService(
            tenantRepo:   new StubTenantRepository(tenant),
            brandingRepo: new StubBrandingRepository(branding));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.BrandingSummary);
        Assert.Equal("Acme Brand", result.BrandingSummary!.BrandName);
        Assert.Equal("#001122",    result.BrandingSummary.PrimaryColor);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsBrandingSummaryNull_WhenNoBranding()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var svc    = BuildService(
            tenantRepo:   new StubTenantRepository(tenant),
            brandingRepo: new StubBrandingRepository(branding: null));

        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Null(result.BrandingSummary);
    }

    [Fact]
    public async Task GetAdminDetailAsync_LogoFallsBackToTenant_WhenBrandingHasNoLogoOverride()
    {
        var logoId  = Guid.CreateVersion7();
        var wLogoId = Guid.CreateVersion7();

        // Tenant has logos but branding record does not override them
        var tenant = Domain.Tenant.Rehydrate(
            id:                 Guid.CreateVersion7(),
            code:               "acme",
            displayName:        "Acme Corp",
            status:             TenantStatus.Active,
            logoDocumentId:     logoId,
            logoWhiteDocumentId: wLogoId);

        var branding = TenantBranding.Create(tenant.Id);

        var svc    = BuildService(
            tenantRepo:   new StubTenantRepository(tenant),
            brandingRepo: new StubBrandingRepository(branding));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(logoId,  result.LogoDocumentId);
        Assert.Equal(wLogoId, result.LogoWhiteDocumentId);
    }

    [Fact]
    public async Task GetAdminDetailAsync_LogoUsedFromBranding_WhenBrandingHasLogoOverride()
    {
        var brandingLogoId = Guid.CreateVersion7();
        var tenant         = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var branding       = TenantBranding.Create(tenant.Id);
        branding.Update(logoDocumentId: brandingLogoId);

        var svc    = BuildService(
            tenantRepo:   new StubTenantRepository(tenant),
            brandingRepo: new StubBrandingRepository(branding));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(brandingLogoId, result.LogoDocumentId);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsEntitlements_WithCorrectMapping()
    {
        var tenant      = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var enabledFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var entitlements = new List<TenantProductEntitlement>
        {
            TenantProductEntitlement.Create(
                tenantId:           tenant.Id,
                productKey:         "liens",
                productDisplayName: "Liens Management",
                isEnabled:          true,
                isDefault:          true,
                effectiveFromUtc:   enabledFrom),
            TenantProductEntitlement.Create(
                tenantId:           tenant.Id,
                productKey:         "task",
                productDisplayName: "Task Management",
                isEnabled:          false,
                isDefault:          false),
        };

        var svc    = BuildService(
            tenantRepo:      new StubTenantRepository(tenant),
            entitlementRepo: new StubEntitlementRepository(entitlements));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result.ProductEntitlements.Count);

        var liens = result.ProductEntitlements.Single(e => e.ProductCode == "liens");
        Assert.Equal("Liens Management", liens.ProductName);
        Assert.True(liens.Enabled);
        Assert.Equal("Active",      liens.Status);
        Assert.Equal(enabledFrom,   liens.EnabledAtUtc);

        var task = result.ProductEntitlements.Single(e => e.ProductCode == "task");
        Assert.Equal("Task Management", task.ProductName);
        Assert.False(task.Enabled);
        Assert.Equal("Disabled", task.Status);
    }

    [Fact]
    public async Task GetAdminDetailAsync_EntitlementProductName_FallsBackToProductCode_WhenDisplayNameNull()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");

        var entitlements = new List<TenantProductEntitlement>
        {
            TenantProductEntitlement.Create(
                tenantId:           tenant.Id,
                productKey:         "liens",
                productDisplayName: null,
                isEnabled:          true,
                isDefault:          false),
        };

        var svc    = BuildService(
            tenantRepo:      new StubTenantRepository(tenant),
            entitlementRepo: new StubEntitlementRepository(entitlements));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        var item = Assert.Single(result.ProductEntitlements);
        Assert.Equal("liens", item.ProductName);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsDomainAndCapabilityCount()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");

        var svc    = BuildService(
            tenantRepo:    new StubTenantRepository(tenant),
            domainRepo:    new StubDomainRepository(domainCount: 3),
            capabilityRepo: new StubCapabilityRepository(capabilityCount: 5));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(3, result.DomainCount);
        Assert.Equal(5, result.CapabilityCount);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ReturnsSessionTimeout_WhenIdentityReturnsValue()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var svc    = BuildService(
            tenantRepo:     new StubTenantRepository(tenant),
            identityCompat: new StubIdentityCompatAdapter(sessionTimeoutMinutes: 30));

        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(30,              result.SessionTimeoutMinutes);
        Assert.Equal("IdentityCompat", result.IdentityCompatSource);
    }

    [Fact]
    public async Task GetAdminDetailAsync_SetsCompatSourceUnavailable_WhenIdentityReturnsNull()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var svc    = BuildService(
            tenantRepo:     new StubTenantRepository(tenant),
            identityCompat: new StubIdentityCompatAdapter(sessionTimeoutMinutes: null));

        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Null(result.SessionTimeoutMinutes);
        Assert.Equal("Unavailable", result.IdentityCompatSource);
    }

    [Fact]
    public async Task ToggleEntitlementAsync_RollsBackExistingEntitlement_WhenIdentitySyncFails()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var existing = TenantProductEntitlement.Create(
            tenantId: tenant.Id,
            productKey: "xenia",
            productDisplayName: "Xenia",
            isEnabled: true,
            isDefault: true,
            effectiveFromUtc: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var entitlementRepo = new StubEntitlementRepository([existing]);
        var identityCompat = new StubIdentityCompatAdapter(setTenantProductEntitlementResult: false);
        var svc = BuildService(
            tenantRepo: new StubTenantRepository(tenant),
            entitlementRepo: entitlementRepo,
            identityCompat: identityCompat);

        var result = await svc.ToggleEntitlementAsync(tenant.Id, "Xenia", enabled: false);
        var reloaded = await entitlementRepo.GetByTenantAndProductKeyAsync(tenant.Id, "xenia");

        Assert.False(result.IdentitySynced);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsEnabled);
        Assert.True(reloaded.IsDefault);
    }

    [Fact]
    public async Task ToggleEntitlementAsync_RemovesCreatedEntitlement_WhenIdentitySyncFails()
    {
        var tenant = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var entitlementRepo = new StubEntitlementRepository();
        var identityCompat = new StubIdentityCompatAdapter(setTenantProductEntitlementResult: false);
        var svc = BuildService(
            tenantRepo: new StubTenantRepository(tenant),
            entitlementRepo: entitlementRepo,
            identityCompat: identityCompat);

        var result = await svc.ToggleEntitlementAsync(tenant.Id, "Xenia", enabled: true);
        var reloaded = await entitlementRepo.GetByTenantAndProductKeyAsync(tenant.Id, "xenia");

        Assert.False(result.IdentitySynced);
        Assert.Null(reloaded);
        Assert.Empty(await entitlementRepo.ListByTenantAsync(tenant.Id));
    }

    [Fact]
    public async Task GetAdminDetailAsync_SettingsSummary_FallsBackToTenantFields_WhenNoSettings()
    {
        var tenant = Domain.Tenant.Create(
            code:        "acme",
            displayName: "Acme Corp",
            timeZone:    "America/Chicago",
            locale:      "en-US");

        var svc    = BuildService(
            tenantRepo:  new StubTenantRepository(tenant),
            settingRepo: new StubSettingRepository(settings: new List<TenantSetting>()));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.SettingsSummary);
        // DefaultProduct falls back to tenant.Locale when no setting exists
        Assert.Equal("en-US",           result.SettingsSummary!.DefaultProduct);
        Assert.Equal("en-US",           result.SettingsSummary.Locale);
        Assert.Equal("America/Chicago", result.SettingsSummary.TimeZone);
    }

    [Fact]
    public async Task GetAdminDetailAsync_SettingsSummary_UsesSettingValues_WhenPresent()
    {
        var tenant = Domain.Tenant.Create(
            code:        "acme",
            displayName: "Acme Corp",
            timeZone:    "America/Chicago",
            locale:      "en-US");

        var settings = new List<TenantSetting>
        {
            MakeSetting(tenant.Id, "default_product", "liens"),
            MakeSetting(tenant.Id, "locale",          "fr-FR"),
            MakeSetting(tenant.Id, "timezone",        "Europe/Paris"),
        };

        var svc    = BuildService(
            tenantRepo:  new StubTenantRepository(tenant),
            settingRepo: new StubSettingRepository(settings));
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.SettingsSummary);
        Assert.Equal("liens",        result.SettingsSummary!.DefaultProduct);
        Assert.Equal("fr-FR",        result.SettingsSummary.Locale);
        Assert.Equal("Europe/Paris", result.SettingsSummary.TimeZone);
    }

    [Fact]
    public async Task GetAdminDetailAsync_AllSubDataIsAssembled_InSingleCall()
    {
        var logoId      = Guid.CreateVersion7();
        var enabledFrom = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenant = Domain.Tenant.Rehydrate(
            id:                  Guid.CreateVersion7(),
            code:                "full",
            displayName:         "Full Tenant",
            status:              TenantStatus.Active,
            subdomain:           "full",
            logoDocumentId:      logoId,
            logoWhiteDocumentId: null);

        var branding = TenantBranding.Create(tenant.Id);
        branding.Update(brandName: "Full Brand", primaryColor: "#FFFFFF");

        var entitlements = new List<TenantProductEntitlement>
        {
            TenantProductEntitlement.Create(
                tenant.Id, "liens", "Liens", true, true, effectiveFromUtc: enabledFrom),
        };

        var settings = new List<TenantSetting>
        {
            MakeSetting(tenant.Id, "default_product", "liens"),
            MakeSetting(tenant.Id, "locale",          "en-GB"),
            MakeSetting(tenant.Id, "timezone",        "Europe/London"),
        };

        var svc = BuildService(
            tenantRepo:      new StubTenantRepository(tenant),
            brandingRepo:    new StubBrandingRepository(branding),
            entitlementRepo: new StubEntitlementRepository(entitlements),
            domainRepo:      new StubDomainRepository(domainCount: 2),
            capabilityRepo:  new StubCapabilityRepository(capabilityCount: 4),
            settingRepo:     new StubSettingRepository(settings),
            identityCompat:  new StubIdentityCompatAdapter(sessionTimeoutMinutes: 60));

        var result = await svc.GetAdminDetailAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.Equal(tenant.Id,    result.Id);
        Assert.Equal("Full Tenant", result.DisplayName);
        Assert.NotNull(result.BrandingSummary);
        Assert.Equal("Full Brand", result.BrandingSummary!.BrandName);
        var ent = Assert.Single(result.ProductEntitlements);
        Assert.Equal("liens",      ent.ProductCode);
        Assert.Equal(2,            result.DomainCount);
        Assert.Equal(4,            result.CapabilityCount);
        Assert.Equal(60,           result.SessionTimeoutMinutes);
        Assert.Equal("IdentityCompat", result.IdentityCompatSource);
        Assert.Equal("liens",      result.SettingsSummary!.DefaultProduct);
        Assert.Equal("en-GB",      result.SettingsSummary.Locale);
        Assert.Equal("Europe/London", result.SettingsSummary.TimeZone);
        Assert.Equal(logoId,       result.LogoDocumentId);
    }

    [Fact]
    public async Task GetAdminDetailAsync_ExecutesRepositoryCalls_Sequentially_NotConcurrently()
    {
        // Arrange: a concurrency guard that tracks how many async data-source calls
        // are executing at the same moment.  Each stub signals entry, introduces a
        // short delay to widen the concurrency window, then signals exit.
        // If any two calls overlap (counter > 1), maxConcurrent will exceed 1.
        //
        // With sequential awaiting this stays at 1; Task.WhenAll would push it to ≥ 2.
        var guard       = new ConcurrencyGuard();
        var tenant      = Domain.Tenant.Create(code: "acme", displayName: "Acme Corp");
        var tenantRepo  = new StubTenantRepository(tenant);

        var svc = BuildService(
            tenantRepo:      tenantRepo,
            brandingRepo:    new GuardedBrandingRepository(guard),
            entitlementRepo: new GuardedEntitlementRepository(guard),
            domainRepo:      new GuardedDomainRepository(guard),
            capabilityRepo:  new GuardedCapabilityRepository(guard),
            settingRepo:     new GuardedSettingRepository(guard),
            identityCompat:  new GuardedIdentityCompatAdapter(guard));

        // Act
        var result = await svc.GetAdminDetailAsync(tenant.Id);

        // Assert: never more than one data-source call in flight at a time
        Assert.NotNull(result);
        Assert.True(
            guard.MaxConcurrentCalls <= 1,
            $"Expected sequential execution (max 1 concurrent call) but observed {guard.MaxConcurrentCalls} concurrent calls. " +
            "This indicates the service regressed to parallel execution (e.g. Task.WhenAll), " +
            "which causes concurrent DbContext access failures in production.");
    }

    private sealed class ConcurrencyGuard
    {
        private int _active;
        public int MaxConcurrentCalls { get; private set; }

        public async Task TrackAsync(Func<Task> body)
        {
            var active = Interlocked.Increment(ref _active);
            if (active > MaxConcurrentCalls)
                MaxConcurrentCalls = active;

            await Task.Delay(10);
            try   { await body(); }
            finally { Interlocked.Decrement(ref _active); }
        }
    }

    private sealed class GuardedBrandingRepository : IBrandingRepository
    {
        private readonly ConcurrencyGuard _guard;
        public GuardedBrandingRepository(ConcurrencyGuard guard) => _guard = guard;

        public async Task<TenantBranding?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        {
            TenantBranding? result = null;
            await _guard.TrackAsync(() => { result = null; return Task.CompletedTask; });
            return result;
        }

        public Task AddAsync(TenantBranding branding, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(TenantBranding branding, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class GuardedEntitlementRepository : IEntitlementRepository
    {
        private readonly ConcurrencyGuard _guard;
        public GuardedEntitlementRepository(ConcurrencyGuard guard) => _guard = guard;

        public async Task<List<TenantProductEntitlement>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            List<TenantProductEntitlement> result = new();
            await _guard.TrackAsync(() => Task.CompletedTask);
            return result;
        }

        public Task<TenantProductEntitlement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TenantProductEntitlement?>(null);
        public Task<TenantProductEntitlement?> GetByTenantAndProductKeyAsync(Guid tenantId, string productKey, CancellationToken ct = default) => Task.FromResult<TenantProductEntitlement?>(null);
        public Task<TenantProductEntitlement?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantProductEntitlement?>(null);
        public Task<List<TenantProductEntitlement>> GetDefaultsForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(new List<TenantProductEntitlement>());
        public Task AddAsync(TenantProductEntitlement entitlement, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(TenantProductEntitlement entitlement, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRangeAsync(IEnumerable<TenantProductEntitlement> entitlements, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantProductEntitlement entitlement, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class GuardedDomainRepository : IDomainRepository
    {
        private readonly ConcurrencyGuard _guard;
        public GuardedDomainRepository(ConcurrencyGuard guard) => _guard = guard;

        public async Task<List<TenantDomain>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            await _guard.TrackAsync(() => Task.CompletedTask);
            return new List<TenantDomain>();
        }

        public Task<TenantDomain?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TenantDomain?>(null);
        public Task<TenantDomain?> GetActiveByHostAsync(string normalizedHost, CancellationToken ct = default) => Task.FromResult<TenantDomain?>(null);
        public Task<TenantDomain?> GetActivePrimarySubdomainByTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantDomain?>(null);
        public Task<bool> ActiveHostExistsAsync(string normalizedHost, Guid? excludeId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<List<TenantDomain>> GetActiveSubdomainsForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(new List<TenantDomain>());
        public Task<TenantDomain?> GetActiveSubdomainByLabelAsync(string label, CancellationToken ct = default) => Task.FromResult<TenantDomain?>(null);
        public Task AddAsync(TenantDomain domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(TenantDomain domain, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRangeAsync(IEnumerable<TenantDomain> domains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class GuardedCapabilityRepository : ICapabilityRepository
    {
        private readonly ConcurrencyGuard _guard;
        public GuardedCapabilityRepository(ConcurrencyGuard guard) => _guard = guard;

        public async Task<List<TenantCapability>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            await _guard.TrackAsync(() => Task.CompletedTask);
            return new List<TenantCapability>();
        }

        public Task<TenantCapability?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TenantCapability?>(null);
        public Task<TenantCapability?> GetByKeyAsync(Guid tenantId, string capabilityKey, Guid? productEntitlementId, CancellationToken ct = default) => Task.FromResult<TenantCapability?>(null);
        public Task AddAsync(TenantCapability capability, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(TenantCapability capability, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantCapability capability, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class GuardedSettingRepository : ISettingRepository
    {
        private readonly ConcurrencyGuard _guard;
        public GuardedSettingRepository(ConcurrencyGuard guard) => _guard = guard;

        public async Task<List<TenantSetting>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            await _guard.TrackAsync(() => Task.CompletedTask);
            return new List<TenantSetting>();
        }

        public Task<TenantSetting?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TenantSetting?>(null);
        public Task<TenantSetting?> GetByKeyAsync(Guid tenantId, string settingKey, string? productKey, CancellationToken ct = default) => Task.FromResult<TenantSetting?>(null);
        public Task AddAsync(TenantSetting setting, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(TenantSetting setting, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantSetting setting, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class GuardedIdentityCompatAdapter : IIdentityCompatAdapter
    {
        private readonly ConcurrencyGuard _guard;
        public GuardedIdentityCompatAdapter(ConcurrencyGuard guard) => _guard = guard;

        public async Task<TenantIdentityCompatSnapshot?> GetTenantAdminSnapshotAsync(Guid tenantId, CancellationToken ct = default)
        {
            await _guard.TrackAsync(() => Task.CompletedTask);
            return null;
        }

        public async Task<int?> GetSessionTimeoutMinutesAsync(Guid tenantId, CancellationToken ct = default)
        {
            await _guard.TrackAsync(() => Task.CompletedTask);
            return null;
        }

        public Task<bool> SetSessionTimeoutAsync(Guid tenantId, int? sessionTimeoutMinutes, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> SetTenantProductEntitlementAsync(Guid tenantId, string productCode, bool enabled, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    // ── Setting helper — bypasses key-dot validation for test keys ────────────

    /// <summary>
    /// Creates a <see cref="TenantSetting"/> instance with an arbitrary key, including
    /// the un-namespaced keys ("default_product", "locale", "timezone") that the
    /// service queries.  The domain factory enforces a dot in the key to prevent
    /// misconfiguration in production code; we use reflection here only in tests.
    /// </summary>
    private static TenantSetting MakeSetting(Guid tenantId, string key, string value)
    {
        var setting = (TenantSetting)RuntimeHelpers.GetUninitializedObject(typeof(TenantSetting));

        void Set(string name, object? v) =>
            typeof(TenantSetting)
                .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(setting, v);

        Set(nameof(TenantSetting.Id),           Guid.CreateVersion7());
        Set(nameof(TenantSetting.TenantId),     tenantId);
        Set(nameof(TenantSetting.SettingKey),   key);
        Set(nameof(TenantSetting.SettingValue), value);
        Set(nameof(TenantSetting.ValueType),    SettingValueType.String);
        Set(nameof(TenantSetting.CreatedAtUtc), DateTime.UtcNow);
        Set(nameof(TenantSetting.UpdatedAtUtc), DateTime.UtcNow);

        return setting;
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubTenantRepository : ITenantRepository
    {
        private readonly Domain.Tenant? _tenant;
        private readonly List<Domain.Tenant> _items;
        private readonly int _total;

        public StubTenantRepository(
            Domain.Tenant? tenant = null,
            List<Domain.Tenant>? items = null,
            int? total = null)
        {
            _tenant = tenant;
            _items = items ?? (tenant is null ? new List<Domain.Tenant>() : [tenant]);
            _total = total ?? _items.Count;
        }

        public Task<Domain.Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_tenant);

        public Task<Domain.Tenant?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(_tenant);

        public Task<Domain.Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default)
            => Task.FromResult<Domain.Tenant?>(null);

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> ExistsBySubdomainAsync(string subdomain, Guid? excludeId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<(List<Domain.Tenant> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult((_items, _total));

        public Task AddAsync(Domain.Tenant tenant, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(Domain.Tenant tenant, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubBrandingRepository : IBrandingRepository
    {
        private readonly TenantBranding? _branding;
        public StubBrandingRepository(TenantBranding? branding = null) => _branding = branding;

        public Task<TenantBranding?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_branding);

        public Task AddAsync(TenantBranding branding, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(TenantBranding branding, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubEntitlementRepository : IEntitlementRepository
    {
        private readonly List<TenantProductEntitlement> _entitlements;
        public StubEntitlementRepository(List<TenantProductEntitlement>? entitlements = null)
            => _entitlements = entitlements ?? new List<TenantProductEntitlement>();

        public Task<List<TenantProductEntitlement>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_entitlements.Where(e => e.TenantId == tenantId).ToList());

        public Task<TenantProductEntitlement?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_entitlements.FirstOrDefault(e => e.Id == id));

        public Task<TenantProductEntitlement?> GetByTenantAndProductKeyAsync(Guid tenantId, string productKey, CancellationToken ct = default)
            => Task.FromResult(_entitlements.FirstOrDefault(e => e.TenantId == tenantId && e.ProductKey == productKey));

        public Task<TenantProductEntitlement?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_entitlements.FirstOrDefault(e => e.TenantId == tenantId && e.IsDefault));

        public Task<List<TenantProductEntitlement>> GetDefaultsForTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_entitlements.Where(e => e.TenantId == tenantId && e.IsDefault).ToList());

        public Task AddAsync(TenantProductEntitlement entitlement, CancellationToken ct = default)
        {
            _entitlements.Add(entitlement);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TenantProductEntitlement entitlement, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateRangeAsync(IEnumerable<TenantProductEntitlement> entitlements, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteAsync(TenantProductEntitlement entitlement, CancellationToken ct = default)
        {
            _entitlements.Remove(entitlement);
            return Task.CompletedTask;
        }
    }

    private sealed class StubDomainRepository : IDomainRepository
    {
        private readonly int _count;
        public StubDomainRepository(int domainCount = 0) => _count = domainCount;

        public Task<List<TenantDomain>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Repeat<TenantDomain>(null!, _count).ToList());

        public Task<TenantDomain?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<TenantDomain?>(null);

        public Task<TenantDomain?> GetActiveByHostAsync(string normalizedHost, CancellationToken ct = default)
            => Task.FromResult<TenantDomain?>(null);

        public Task<TenantDomain?> GetActivePrimarySubdomainByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantDomain?>(null);

        public Task<bool> ActiveHostExistsAsync(string normalizedHost, Guid? excludeId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<List<TenantDomain>> GetActiveSubdomainsForTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new List<TenantDomain>());

        public Task<TenantDomain?> GetActiveSubdomainByLabelAsync(string label, CancellationToken ct = default)
            => Task.FromResult<TenantDomain?>(null);

        public Task AddAsync(TenantDomain domain, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(TenantDomain domain, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateRangeAsync(IEnumerable<TenantDomain> domains, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubCapabilityRepository : ICapabilityRepository
    {
        private readonly int _count;
        public StubCapabilityRepository(int capabilityCount = 0) => _count = capabilityCount;

        public Task<List<TenantCapability>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Repeat<TenantCapability>(null!, _count).ToList());

        public Task<TenantCapability?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<TenantCapability?>(null);

        public Task<TenantCapability?> GetByKeyAsync(Guid tenantId, string capabilityKey, Guid? productEntitlementId, CancellationToken ct = default)
            => Task.FromResult<TenantCapability?>(null);

        public Task AddAsync(TenantCapability capability, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(TenantCapability capability, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteAsync(TenantCapability capability, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubSettingRepository : ISettingRepository
    {
        private readonly List<TenantSetting> _settings;
        public StubSettingRepository(List<TenantSetting>? settings = null)
            => _settings = settings ?? new List<TenantSetting>();

        public Task<List<TenantSetting>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_settings);

        public Task<TenantSetting?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<TenantSetting?>(null);

        public Task<TenantSetting?> GetByKeyAsync(Guid tenantId, string settingKey, string? productKey, CancellationToken ct = default)
            => Task.FromResult<TenantSetting?>(null);

        public Task AddAsync(TenantSetting setting, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(TenantSetting setting, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteAsync(TenantSetting setting, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubIdentityCompatAdapter : IIdentityCompatAdapter
    {
        private readonly int? _sessionTimeoutMinutes;
        private readonly TenantIdentityCompatSnapshot? _snapshot;
        private readonly bool _setTenantProductEntitlementResult;

        public StubIdentityCompatAdapter(
            int? sessionTimeoutMinutes = null,
            TenantIdentityCompatSnapshot? snapshot = null,
            bool setTenantProductEntitlementResult = true)
        {
            _sessionTimeoutMinutes = sessionTimeoutMinutes;
            _snapshot = snapshot;
            _setTenantProductEntitlementResult = setTenantProductEntitlementResult;
        }

        public Task<TenantIdentityCompatSnapshot?> GetTenantAdminSnapshotAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantIdentityCompatSnapshot?>(_snapshot ?? new TenantIdentityCompatSnapshot(null, _sessionTimeoutMinutes, null));

        public Task<int?> GetSessionTimeoutMinutesAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_sessionTimeoutMinutes);

        public Task<bool> SetSessionTimeoutAsync(Guid tenantId, int? sessionTimeoutMinutes, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> SetTenantProductEntitlementAsync(Guid tenantId, string productCode, bool enabled, CancellationToken ct = default)
            => Task.FromResult(_setTenantProductEntitlementResult);
    }

    private sealed class StubIdentityProvisioningAdapter : IIdentityProvisioningAdapter
    {
        public Task<IdentityProvisioningResult> ProvisionAsync(IdentityProvisioningRequest request, CancellationToken ct = default)
            => Task.FromResult(new IdentityProvisioningResult(
                Success: false, AdminUserId: null, AdminEmail: null,
                TemporaryPassword: null, ProvisioningStatus: null, Hostname: null,
                Subdomain: null, Warnings: new List<string>(), Errors: new List<string>()));

        public Task<ProvisioningRetryResult> RetryProvisioningAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new ProvisioningRetryResult(false, "Unknown", null, null, null));

        public Task<ProvisioningRetryResult> RetryVerificationAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new ProvisioningRetryResult(false, "Unknown", null, null, null));
    }

    private sealed class StubCommerceLifecycleNotifier : ICommerceLifecycleNotifier
    {
        public Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
