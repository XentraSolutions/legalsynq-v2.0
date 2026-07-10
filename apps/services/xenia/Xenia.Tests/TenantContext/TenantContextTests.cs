using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.TenantContext;
using Xenia.Infrastructure.TenantContext;

namespace Xenia.Tests.TenantContext;

/// <summary>
/// Unit tests for Xenia's tenant context resolution.
///
/// Validates: valid resolution, missing claim, invalid GUID format,
/// unauthenticated requests, and the scoped accessor.
/// </summary>
public sealed class TenantContextTests
{
    private readonly JwtTenantContextResolver _resolver =
        new(NullLogger<JwtTenantContextResolver>.Instance);

    [Fact]
    public async Task Resolver_ValidTenantId_ReturnsResolvedContext()
    {
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var context = BuildHttpContext(
            isAuthenticated: true,
            tenantId: tenantId.ToString(),
            sub: actorId.ToString(),
            tenantCode: "ACME");

        var result = await _resolver.ResolveAsync(context);

        Assert.NotNull(result);
        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("ACME", result.TenantCode);
        Assert.Equal(actorId, result.ActorId);
    }

    [Fact]
    public async Task Resolver_MissingTenantIdClaim_ReturnsNull()
    {
        var context = BuildHttpContext(
            isAuthenticated: true,
            tenantId: null,
            sub: Guid.CreateVersion7().ToString());

        var result = await _resolver.ResolveAsync(context);

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolver_InvalidGuidTenantId_ReturnsNull()
    {
        var context = BuildHttpContext(
            isAuthenticated: true,
            tenantId: "not-a-valid-guid",
            sub: Guid.CreateVersion7().ToString());

        var result = await _resolver.ResolveAsync(context);

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolver_EmptyGuidTenantId_ReturnsNull()
    {
        var context = BuildHttpContext(
            isAuthenticated: true,
            tenantId: Guid.Empty.ToString(),
            sub: Guid.CreateVersion7().ToString());

        var result = await _resolver.ResolveAsync(context);

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolver_UnauthenticatedRequest_ReturnsNull()
    {
        var context = BuildHttpContext(isAuthenticated: false, tenantId: Guid.NewGuid().ToString());

        var result = await _resolver.ResolveAsync(context);

        Assert.Null(result);
    }

    [Fact]
    public void Accessor_WithoutResolution_CurrentIsNull()
    {
        var accessor = new XeniaTenantContextAccessor();
        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Accessor_AfterSet_CurrentIsResolved()
    {
        var accessor = new XeniaTenantContextAccessor();
        var mockContext = new MockTenantContext(Guid.CreateVersion7());

        accessor.Set(mockContext);

        Assert.NotNull(accessor.Current);
        Assert.True(accessor.Current.IsResolved);
    }

    [Fact]
    public void Accessor_SetNull_ThrowsArgumentNullException()
    {
        var accessor = new XeniaTenantContextAccessor();
        Assert.Throws<ArgumentNullException>(() => accessor.Set(null!));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpContext BuildHttpContext(
        bool isAuthenticated,
        string? tenantId = null,
        string? sub = null,
        string? tenantCode = null)
    {
        var claims = new List<Claim>();
        if (tenantId is not null) claims.Add(new Claim("tenant_id", tenantId));
        if (sub is not null) claims.Add(new Claim("sub", sub));
        if (tenantCode is not null) claims.Add(new Claim("tenant_code", tenantCode));

        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "Bearer")
            : new ClaimsIdentity();

        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return httpContext;
    }

    private sealed record MockTenantContext(Guid TenantId) : IXeniaTenantContext
    {
        public bool IsResolved => true;
        public string? TenantCode => null;
        public Guid? ActorId => null;
        public string? CorrelationId => null;
    }
}
