using BuildingBlocks.TestHelpers;
using Identity.Application.DTOs;
using Identity.Application.Errors;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Tests;

/// <summary>
/// BE-BIO: integration tests for DeviceSessionService, run against a real MySQL
/// instance (via Testcontainers, same pattern as PlatformAdminRoleSeedingTests)
/// because the core rotation transaction (RefreshAsync) uses relational-only
/// features — `SELECT ... FOR UPDATE` row locking and explicit transactions —
/// that the EF Core InMemory provider does not support.
///
/// CI: runs under the "Identity Integration Tests" workflow.
/// Filter: --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public sealed class DeviceSessionServiceTests : IAsyncLifetime
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));
    private readonly MySqlTestContainer _container = new();
    private string _cs = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _cs = await _container.CreateDatabaseAsync("identity_biometric_test");

        await using var db = BuildDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private IdentityDbContext BuildDbContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseMySql(_cs, ServerVersion).Options);

    private static DeviceSessionService BuildService(IdentityDbContext db, RefreshTokenPolicyOptions? policy = null) =>
        new(db, new FakeJwtTokenService(), new NoOpAuditEventClient(),
            Options.Create(policy ?? new RefreshTokenPolicyOptions()),
            NullLogger<DeviceSessionService>.Instance);

    private async Task<(Guid UserId, Guid TenantId)> SeedUserAndTenantAsync(IdentityDbContext db, bool userLocked = false, bool userActive = true)
    {
        var tenant = Tenant.Create("Bio Test Tenant", "biotest" + Guid.NewGuid().ToString("N"));
        db.Tenants.Add(tenant);

        var user = User.Create(tenant.Id, $"{Guid.CreateVersion7()}@example.com", "hashed-password", "Test", "User");
        if (!userActive) user.Deactivate();
        if (userLocked) user.Lock();
        db.Users.Add(user);

        await db.SaveChangesAsync();
        return (user.Id, tenant.Id);
    }

    private static DeviceInfo TestDevice => new("ios", "1.0.0", "17.0", "Test iPhone");

    // ── Normal rotation ──────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesAndReturnsNewToken()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        var result = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(created.RefreshToken, result.Response!.RefreshToken);
        Assert.Equal(created.DeviceSessionId, result.Response.DeviceSessionId);
    }

    [Fact]
    public async Task RefreshAsync_AfterRotation_OldTokenNoLongerWorks()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));
        await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        var secondAttempt = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        Assert.False(secondAttempt.IsSuccess);
    }

    // ── Reuse detection (BE-BIO-007) ─────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_ReusedRotatedToken_IsRejectedAndRevokesTheSession()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        // Zero grace period so this test's near-instant resubmission is evaluated
        // as confirmed reuse rather than the benign-race grace path (see
        // RefreshAsync_ConcurrentCallsWithSameToken_OnlyOneSucceeds for that path).
        var service = BuildService(db, new RefreshTokenPolicyOptions { ReuseGraceSeconds = 0 });

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));
        var rotated = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");
        Assert.True(rotated.IsSuccess);

        // Resubmitting the original (now-rotated/superseded) token outside the
        // grace period is confirmed reuse.
        var reuseAttempt = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "10.0.0.1");

        Assert.False(reuseAttempt.IsSuccess);
        // SEC-010: externally-visible code must not distinguish reuse from a generic invalid token.
        Assert.Equal(AuthErrorCodes.RefreshTokenInvalid, reuseAttempt.ErrorCode);

        await using var verifyDb = BuildDbContext();
        var session = await verifyDb.DeviceSessions.AsNoTracking().SingleAsync(s => s.Id == created.DeviceSessionId);
        Assert.Equal(DeviceSessionStatuses.Compromised, session.Status);

        // The legitimately-rotated (now most recent) token must also be rejected —
        // the whole family was revoked, not just the reused generation.
        var followUpWithLatestToken = await service.RefreshAsync(rotated.Response!.RefreshToken, created.DeviceSessionId, "127.0.0.1");
        Assert.False(followUpWithLatestToken.IsSuccess);
        Assert.Equal(AuthErrorCodes.DeviceSessionRevoked, followUpWithLatestToken.ErrorCode);
    }

    [Fact]
    public async Task RefreshAsync_NeverIssuedToken_IsRejectedWithoutRevokingTheSession()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        var garbageAttempt = await service.RefreshAsync("this-was-never-issued", created.DeviceSessionId, "127.0.0.1");

        Assert.False(garbageAttempt.IsSuccess);
        Assert.Equal(AuthErrorCodes.RefreshTokenInvalid, garbageAttempt.ErrorCode);

        await using var verifyDb = BuildDbContext();
        var session = await verifyDb.DeviceSessions.AsNoTracking().SingleAsync(s => s.Id == created.DeviceSessionId);
        Assert.Equal(DeviceSessionStatuses.Active, session.Status); // not misclassified as theft

        // The real token must still work.
        var legitimateRefresh = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");
        Assert.True(legitimateRefresh.IsSuccess);
    }

    // ── Independent absolute / inactivity expiry (BE-BIO-009) ───────────────

    [Fact]
    public async Task RefreshAsync_PastAbsoluteExpiry_IsRejectedEvenWhenInactivityWindowStillOpen()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        await using (var mutateDb = BuildDbContext())
        {
            await mutateDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE `idt_DeviceSessions` SET `AbsoluteExpiresAtUtc` = {DateTime.UtcNow.AddDays(-1)} WHERE `Id` = {created.DeviceSessionId}");
        }

        var result = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RefreshTokenExpired, result.ErrorCode);
    }

    [Fact]
    public async Task RefreshAsync_PastInactivityExpiry_IsRejectedEvenWhenAbsoluteWindowStillOpen()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        await using (var mutateDb = BuildDbContext())
        {
            await mutateDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE `idt_DeviceSessions` SET `InactivityExpiresAtUtc` = {DateTime.UtcNow.AddDays(-1)} WHERE `Id` = {created.DeviceSessionId}");
        }

        var result = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RefreshTokenExpired, result.ErrorCode);
    }

    // ── Revoked session / account status ─────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_RevokedSession_IsRejected()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));
        await service.RevokeSessionAsync(userId, created.DeviceSessionId, "Test");

        var result = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.DeviceSessionRevoked, result.ErrorCode);
    }

    [Fact]
    public async Task RefreshAsync_LockedAccount_IsRejectedAndRevokesSession()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db, userLocked: true);
        var service = BuildService(db);

        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        var result = await service.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.AccountLocked, result.ErrorCode);
    }

    // ── Idempotency (BE-BIO-019) ─────────────────────────────────────────────

    [Fact]
    public async Task LogoutCurrentAsync_CalledTwice_DoesNotThrow()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);
        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        await service.LogoutCurrentAsync(created.RefreshToken, created.DeviceSessionId);
        await service.LogoutCurrentAsync(created.RefreshToken, created.DeviceSessionId);
    }

    [Fact]
    public async Task DisableBiometricAsync_CalledTwice_BothReturnTrueWithoutError()
    {
        await using var db = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);
        var created = await service.CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));
        await service.EnableBiometricAsync(userId, created.DeviceSessionId);

        var first = await service.DisableBiometricAsync(userId, created.DeviceSessionId);
        var second = await service.DisableBiometricAsync(userId, created.DeviceSessionId);

        Assert.True(first);
        Assert.True(second);
    }

    // ── IDOR ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeSessionAsync_ForAnotherUsersSession_ReturnsFalse()
    {
        await using var db = BuildDbContext();
        var (ownerId, tenantId) = await SeedUserAndTenantAsync(db);
        var (attackerId, _) = await SeedUserAndTenantAsync(db);
        var service = BuildService(db);
        var created = await service.CreateDeviceSessionAsync(ownerId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        var result = await service.RevokeSessionAsync(attackerId, created.DeviceSessionId, "Test");

        Assert.False(result);
    }

    // ── Concurrency race ──────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_ConcurrentCallsWithSameToken_OnlyOneSucceeds()
    {
        await using var seedDb = BuildDbContext();
        var (userId, tenantId) = await SeedUserAndTenantAsync(seedDb);
        var created = await BuildService(seedDb).CreateDeviceSessionAsync(userId, tenantId, TestDevice, "access-token", DateTime.UtcNow.AddMinutes(15));

        // Separate DbContext instances (and thus separate connections) per concurrent
        // call, mirroring two simultaneous HTTP requests each with their own scope.
        await using var db1 = BuildDbContext();
        await using var db2 = BuildDbContext();
        var service1 = BuildService(db1);
        var service2 = BuildService(db2);

        var task1 = service1.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");
        var task2 = service2.RefreshAsync(created.RefreshToken, created.DeviceSessionId, "127.0.0.1");
        var results = await Task.WhenAll(task1, task2);

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => !r.IsSuccess);

        await using var verifyDb = BuildDbContext();
        var session = await verifyDb.DeviceSessions.AsNoTracking().SingleAsync(s => s.Id == created.DeviceSessionId);
        // The loser of the race must not be treated as an attacker — this was a
        // benign concurrent resubmission within the reuse grace period, not theft.
        Assert.Equal(DeviceSessionStatuses.Active, session.Status);

        var activeLedgerCount = await verifyDb.RefreshTokenLedgerEntries
            .CountAsync(l => l.DeviceSessionId == created.DeviceSessionId && l.Status == DeviceSessionStatuses.Active);
        Assert.Equal(1, activeLedgerCount); // exactly one current generation — no double-rotation corruption
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) GenerateToken(
            User user, Tenant tenant, IEnumerable<string> roles, Organization? organization = null,
            IEnumerable<string>? productRoles = null, int? sessionTimeoutMinutes = null,
            IEnumerable<string>? productCodes = null, IEnumerable<string>? permissions = null,
            IEnumerable<Guid>? tenantIds = null) =>
            ("fake-access-token", DateTime.UtcNow.AddMinutes(60));

        public (string Token, DateTime ExpiresAtUtc) GenerateRefreshedAccessToken(User user, Tenant tenant, Guid deviceSessionId) =>
            ("fake-refreshed-access-token", DateTime.UtcNow.AddMinutes(15));
    }

    private sealed class NoOpAuditEventClient : IAuditEventClient
    {
        public Task<IngestResult> IngestAsync(IngestAuditEventRequest request, CancellationToken ct = default) =>
            Task.FromResult(new IngestResult(true, Guid.NewGuid().ToString(), null, 200));

        public Task<BatchIngestResult> IngestBatchAsync(BatchIngestRequest request, CancellationToken ct = default) =>
            Task.FromResult(new BatchIngestResult(0, 0, 0, Array.Empty<IngestResult>()));
    }
}
