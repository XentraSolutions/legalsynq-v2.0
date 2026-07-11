using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;
using Xenia.Tests.Automation.Infrastructure;

namespace Xenia.Tests.Automation.Concurrency;

/// <summary>
/// Optimistic concurrency tests using row_version on automation tables (G9/G10).
///
/// Proves:
///   G9 — concurrent writes to the same row surface DbUpdateConcurrencyException
///   G10 — multi-instance scenario: both instances operate without data corruption
///         when they access different rows; same-row conflicts are detected
///
/// Requires: MySQL container at 127.0.0.1:13309 (xenia-test-mysql-v1)
/// </summary>
[Collection("XeniaRelational")]
public sealed class RowVersionConcurrencyTests : IAsyncLifetime
{
    private readonly XeniaRelationalFixture _fx;

    public RowVersionConcurrencyTests(XeniaRelationalFixture fx) => _fx = fx;

    public async Task InitializeAsync() => await _fx.TruncateAutomationTablesAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── G9: DbUpdateConcurrencyException on stale row_version ────────────

    [Fact]
    public async Task SaveChanges_ThrowsDbUpdateConcurrencyException_OnRowVersionMismatch()
    {
        // Insert an initial row via registry
        await using var sp = XeniaTestServiceBuilder.Build();
        var registry = sp.GetRequiredService<IAutomationRegistry>();
        var provider = new FakeAutomationProvider("test.concur.rowversion.v1");
        await registry.RegisterAsync(provider);

        // Open TWO independent contexts reading the SAME row
        await using var ctx1 = await _fx.CreateContextAsync();
        await using var ctx2 = await _fx.CreateContextAsync();

        var row1 = await ctx1.AutomationRegistry
            .FirstAsync(r => r.AutomationKey == "test.concur.rowversion.v1");
        var row2 = await ctx2.AutomationRegistry
            .FirstAsync(r => r.AutomationKey == "test.concur.rowversion.v1");

        // Both see same initial row_version
        Assert.Equal(row1.RowVersion, row2.RowVersion);

        // Context 1 writes (increments row_version in DB)
        row1.RowVersion++;
        row1.UpdatedAt = DateTime.UtcNow;
        await ctx1.SaveChangesAsync();

        // Context 2 tries to write with the OLD row_version — should conflict
        row2.RowVersion++;  // still the old value; DB now has newer version
        row2.UpdatedAt = DateTime.UtcNow.AddSeconds(1);

        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctx2.SaveChangesAsync());

        Assert.NotNull(ex);
    }

    // ── G10: Multi-instance, different rows — no conflict ─────────────────

    [Fact]
    public async Task TwoInstances_DifferentRows_BothSucceedWithoutConflict()
    {
        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "InstanceA");
        await using var spB = XeniaTestServiceBuilder.Build(instanceId: "InstanceB");

        var ra = spA.GetRequiredService<IAutomationRegistry>();
        var rb = spB.GetRequiredService<IAutomationRegistry>();

        var pa = new FakeAutomationProvider("test.concur.multiinst.alpha");
        var pb = new FakeAutomationProvider("test.concur.multiinst.beta");

        // Register concurrently on different rows
        await Task.WhenAll(ra.RegisterAsync(pa), rb.RegisterAsync(pb));

        // Both rows should be present
        await using var ctx = await _fx.CreateContextAsync();
        var alpha = await ctx.AutomationRegistry
            .CountAsync(r => r.AutomationKey == "test.concur.multiinst.alpha");
        var beta = await ctx.AutomationRegistry
            .CountAsync(r => r.AutomationKey == "test.concur.multiinst.beta");

        Assert.Equal(1, alpha);
        Assert.Equal(1, beta);
    }

    // ── G10: Two instances enable different tenants concurrently ──────────

    [Fact]
    public async Task TwoInstances_EnableDifferentTenants_Concurrently_BothPersist()
    {
        // Setup: both instances know about the same provider
        await using var spSetup = XeniaTestServiceBuilder.Build();
        var regSetup = spSetup.GetRequiredService<IAutomationRegistry>();
        var provider = new FakeAutomationProvider("test.concur.tenants.v1");
        await regSetup.RegisterAsync(provider);

        await using var spA = XeniaTestServiceBuilder.Build(instanceId: "A");
        await using var spB = XeniaTestServiceBuilder.Build(instanceId: "B");

        var regA = spA.GetRequiredService<IAutomationRegistry>();
        var regB = spB.GetRequiredService<IAutomationRegistry>();
        await regA.RegisterAsync(provider);
        await regB.RegisterAsync(provider);

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();

        // Both instances enable a DIFFERENT tenant — no shared row contention
        await Task.WhenAll(
            regA.EnableForTenantAsync("test.concur.tenants.v1", tenantA, actorId),
            regB.EnableForTenantAsync("test.concur.tenants.v1", tenantB, actorId));

        await using var ctx = await _fx.CreateContextAsync();
        var count = await ctx.TenantAutomations
            .CountAsync(t => t.AutomationKey == "test.concur.tenants.v1");

        Assert.Equal(2, count);
    }
}
