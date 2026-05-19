using Billing.Api.LegalSynq;
using Billing.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Billing.Tests.Integration;

/// <summary>
/// LS-INT-01 — unit tests for <see cref="LegalSynqJwtTenantContextResolver"/>.
/// Validates the dual-mode hierarchy (JWT claim → internal-service header → fallback)
/// and safe-default behavior without spinning up a full pipeline.
/// </summary>
public class LegalSynqTenantContextResolverTests
{
    private static LegalSynqJwtTenantContextResolver BuildResolver(
        bool enabled = true,
        bool preferJwt = true,
        bool allowHeader = true,
        bool allowInternal = true)
    {
        var opts = Options.Create(new LegalSynqTenantContextOptions
        {
            Enabled = enabled,
            PreferJwtTenant = preferJwt,
            AllowHeaderFallback = allowHeader,
            AllowInternalTokenFallback = allowInternal,
        });
        return new LegalSynqJwtTenantContextResolver(opts, NullLogger<LegalSynqJwtTenantContextResolver>.Instance);
    }

    private static HttpContext BuildContext(
        Guid? jwtTenantId = null,
        string? role = null,
        string? headerTenantId = null,
        bool isAuthenticated = false)
    {
        var ctx = new DefaultHttpContext();

        if (isAuthenticated || jwtTenantId.HasValue || role is not null)
        {
            var claims = new List<System.Security.Claims.Claim>();
            if (jwtTenantId.HasValue)
                claims.Add(new("tenant_id", jwtTenantId.Value.ToString()));
            if (role is not null)
                claims.Add(new("role", role));

            var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestScheme");
            ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }

        if (headerTenantId is not null)
            ctx.Request.Headers[TenantResolutionMiddleware.HeaderName] = headerTenantId;

        return ctx;
    }

    // ── Priority 1: JWT tenant_id claim ────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_JwtClaim_ReturnsJwtSource()
    {
        var tenantId = Guid.CreateVersion7();
        var ctx = BuildContext(jwtTenantId: tenantId, isAuthenticated: true);
        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(ctx);

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(TenantResolutionSource.JwtClaim, result.Source);
    }

    [Fact]
    public async Task ResolveAsync_JwtClaim_PreferJwtFalse_SkipsClaimFallsToHeader()
    {
        var jwtTenant = Guid.CreateVersion7();
        var headerTenant = Guid.CreateVersion7();
        var ctx = BuildContext(
            jwtTenantId: jwtTenant,
            headerTenantId: headerTenant.ToString(),
            isAuthenticated: true);
        var resolver = BuildResolver(preferJwt: false);

        var result = await resolver.ResolveAsync(ctx);

        Assert.True(result.IsResolved);
        Assert.Equal(headerTenant, result.TenantId);
        Assert.Equal(TenantResolutionSource.Header, result.Source);
    }

    [Fact]
    public async Task ResolveAsync_JwtClaimEmptyGuid_FailsOnClaim()
    {
        var ctx = BuildContext(jwtTenantId: Guid.Empty, isAuthenticated: true);
        var resolver = BuildResolver(allowHeader: false);

        var result = await resolver.ResolveAsync(ctx);

        Assert.False(result.IsResolved);
    }

    // ── Priority 2: Internal-service JWT + X-Tenant-Id header ──────────────

    [Fact]
    public async Task ResolveAsync_InternalServiceRole_UsesHeader()
    {
        var tenantId = Guid.CreateVersion7();
        var ctx = BuildContext(
            role: LegalSynqBillingRoles.InternalService,
            headerTenantId: tenantId.ToString(),
            isAuthenticated: true);

        // Disable JWT tenant claim (no tenant_id claim present)
        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(ctx);

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(TenantResolutionSource.Header, result.Source);
    }

    [Fact]
    public async Task ResolveAsync_InternalServiceRole_AllowInternalFalse_FallsThrough()
    {
        var tenantId = Guid.CreateVersion7();
        var ctx = BuildContext(
            role: LegalSynqBillingRoles.InternalService,
            headerTenantId: tenantId.ToString(),
            isAuthenticated: true);

        var resolver = BuildResolver(allowInternal: false, allowHeader: true);

        var result = await resolver.ResolveAsync(ctx);

        // Should still resolve via priority-3 header fallback when allowHeader=true
        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
    }

    // ── Priority 3: X-Tenant-Id header fallback ────────────────────────────

    [Fact]
    public async Task ResolveAsync_NoJwt_HeaderFallback_Resolves()
    {
        var tenantId = Guid.CreateVersion7();
        var ctx = BuildContext(headerTenantId: tenantId.ToString());
        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(ctx);

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(TenantResolutionSource.Header, result.Source);
    }

    [Fact]
    public async Task ResolveAsync_NoJwt_HeaderFallbackDisabled_Fails()
    {
        var tenantId = Guid.CreateVersion7();
        var ctx = BuildContext(headerTenantId: tenantId.ToString());
        var resolver = BuildResolver(allowHeader: false);

        var result = await resolver.ResolveAsync(ctx);

        Assert.False(result.IsResolved);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task ResolveAsync_InvalidGuidHeader_Fails()
    {
        var ctx = BuildContext(headerTenantId: "not-a-guid");
        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(ctx);

        Assert.False(result.IsResolved);
    }

    [Fact]
    public async Task ResolveAsync_EmptyGuidHeader_Fails()
    {
        var ctx = BuildContext(headerTenantId: Guid.Empty.ToString());
        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(ctx);

        Assert.False(result.IsResolved);
    }

    [Fact]
    public async Task ResolveAsync_NeitherJwtNorHeader_Fails()
    {
        var ctx = BuildContext();
        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(ctx);

        Assert.False(result.IsResolved);
        Assert.NotNull(result.FailureReason);
    }

    // ── TenantResolutionResult factory methods ──────────────────────────────

    [Fact]
    public void TenantResolutionResult_Resolved_SetsFields()
    {
        var id = Guid.CreateVersion7();
        var result = TenantResolutionResult.Resolved(id, TenantResolutionSource.JwtClaim);

        Assert.True(result.IsResolved);
        Assert.Equal(id, result.TenantId);
        Assert.Equal(TenantResolutionSource.JwtClaim, result.Source);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void TenantResolutionResult_Failed_SetsReason()
    {
        var result = TenantResolutionResult.Failed("some reason");

        Assert.False(result.IsResolved);
        Assert.Equal("some reason", result.FailureReason);
    }

    // ── Safe-default guarantees ─────────────────────────────────────────────

    [Fact]
    public void TenantContextOptions_Defaults_AreConservative()
    {
        var opts = new LegalSynqTenantContextOptions();

        Assert.False(opts.Enabled,
            "LegalSynq:TenantContext:Enabled must default to false to preserve standalone behavior.");
        Assert.True(opts.PreferJwtTenant);
        Assert.True(opts.AllowHeaderFallback);
        Assert.True(opts.AllowInternalTokenFallback);
    }

    [Fact]
    public void IdentityOptions_Defaults_AreConservative()
    {
        var opts = new LegalSynqIdentityOptions();

        Assert.False(opts.Enabled,
            "LegalSynq:Identity:Enabled must default to false to preserve standalone behavior.");
        Assert.Equal("legalsynq-identity", opts.Issuer);
        Assert.Equal("legalsynq-platform", opts.Audience);
    }

    // ── LegalSynqBillingRoles ───────────────────────────────────────────────

    [Theory]
    [InlineData(LegalSynqBillingRoles.PlatformAdmin, true)]
    [InlineData(LegalSynqBillingRoles.TenantAdmin, true)]
    [InlineData(LegalSynqBillingRoles.BillingManager, true)]
    [InlineData(LegalSynqBillingRoles.InternalService, true)]
    [InlineData(LegalSynqBillingRoles.BillingReadOnly, false)]
    [InlineData(LegalSynqBillingRoles.SupportAgent, false)]
    [InlineData("Unrelated", false)]
    public void BillingRoles_HasBillingWrite_ReturnsExpected(string role, bool expected)
    {
        Assert.Equal(expected, LegalSynqBillingRoles.HasBillingWrite([role]));
    }
}
