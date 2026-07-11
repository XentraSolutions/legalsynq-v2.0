using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for EfSyncStateService.
/// Uses InMemory EF provider — no database required.
/// </summary>
public sealed class SyncStateServiceTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfSyncStateService _sut;

    private static readonly Guid TenantId = Guid.Parse("11111111-0000-0000-0000-000000000011");
    private static readonly Guid SourceId = Guid.Parse("22222222-0000-0000-0000-000000000022");

    public SyncStateServiceTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new XeniaDbContext(options);
        _sut = new EfSyncStateService(_db, NullLogger<EfSyncStateService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── GetOrCreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_NewSource_CreatesState()
    {
        var state = await _sut.GetOrCreateAsync(TenantId, SourceId, EmailProviderType.Imap);

        Assert.NotEqual(Guid.Empty, state.Id);
        Assert.Equal(TenantId, state.TenantId);
        Assert.Equal(SourceId, state.EmailSourceId);
        Assert.Equal(EmailProviderType.Imap, state.ProviderType);
        Assert.False(state.InitialSyncCompleted);
        Assert.Equal(0, state.ConsecutiveFailureCount);

        var count = await _db.EmailSyncStates.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingSource_ReturnsExistingState()
    {
        var first  = await _sut.GetOrCreateAsync(TenantId, SourceId, EmailProviderType.Imap);
        var second = await _sut.GetOrCreateAsync(TenantId, SourceId, EmailProviderType.Imap);

        Assert.Equal(first.Id, second.Id);

        var count = await _db.EmailSyncStates.CountAsync();
        Assert.Equal(1, count);
    }

    // ── StartRunAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StartRunAsync_CreatesIngestionRun()
    {
        var run = await _sut.StartRunAsync(
            TenantId, SourceId, IngestionRunTriggerType.Manual,
            correlationId: "corr-001", actorId: null, workerInstanceId: null,
            cursorBeforeSafeSummary: "page 1");

        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(TenantId, run.TenantId);
        Assert.Equal(SourceId, run.EmailSourceId);
        Assert.Equal(IngestionRunTriggerType.Manual, run.TriggerType);
        Assert.Equal(IngestionRunStatus.Queued, run.Status);
        Assert.Equal("corr-001", run.CorrelationId);

        var dbRun = await _db.EmailIngestionRuns.FindAsync(run.Id);
        Assert.NotNull(dbRun);
    }

    // ── MarkRunStartedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task MarkRunStartedAsync_TransitionsToRunning()
    {
        var run = await _sut.StartRunAsync(
            TenantId, SourceId, IngestionRunTriggerType.Scheduled,
            null, null, null, null);

        await _sut.MarkRunStartedAsync(run.Id);

        var updated = await _db.EmailIngestionRuns.FindAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal(IngestionRunStatus.Running, updated.Status);
    }

    // ── CompleteRunAsync / FailRunAsync ───────────────────────────────────────

    [Fact]
    public async Task CompleteRunAsync_TransitionsToCompleted()
    {
        var run = await _sut.StartRunAsync(
            TenantId, SourceId, IngestionRunTriggerType.Manual,
            null, null, null, null);
        await _sut.MarkRunStartedAsync(run.Id);

        await _sut.CompleteRunAsync(run.Id, cursorAfterSafeSummary: "page 5");

        var updated = await _db.EmailIngestionRuns.FindAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal(IngestionRunStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal("page 5", updated.CursorAfterSafeSummary);
    }

    [Fact]
    public async Task FailRunAsync_TransitionsToFailed()
    {
        var run = await _sut.StartRunAsync(
            TenantId, SourceId, IngestionRunTriggerType.Manual,
            null, null, null, null);
        await _sut.MarkRunStartedAsync(run.Id);

        await _sut.FailRunAsync(run.Id, "CONNECTOR_TIMEOUT", "Connection timed out.");

        var updated = await _db.EmailIngestionRuns.FindAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal(IngestionRunStatus.Failed, updated.Status);
        Assert.Equal("CONNECTOR_TIMEOUT", updated.ErrorCode);
        Assert.NotNull(updated.CompletedAt);
    }

    // ── RecordFailureAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RecordFailureAsync_IncrementsConsecutiveFailureCount()
    {
        await _sut.GetOrCreateAsync(TenantId, SourceId, EmailProviderType.Imap);

        await _sut.RecordFailureAsync(TenantId, SourceId, "ERR_001", "Test failure");

        var state = await _db.EmailSyncStates
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.EmailSourceId == SourceId);
        Assert.NotNull(state);
        Assert.Equal(1, state.ConsecutiveFailureCount);
        Assert.Equal("ERR_001", state.LastErrorCode);
        Assert.NotNull(state.NextEligibleSyncAt);
    }

    // ── GetSyncStateAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSyncStateAsync_NoState_ReturnsNull()
    {
        var state = await _sut.GetSyncStateAsync(TenantId, SourceId);
        Assert.Null(state);
    }

    [Fact]
    public async Task GetSyncStateAsync_ExistingState_ReturnsState()
    {
        await _sut.GetOrCreateAsync(TenantId, SourceId, EmailProviderType.Google);

        var state = await _sut.GetSyncStateAsync(TenantId, SourceId);
        Assert.NotNull(state);
        Assert.Equal(SourceId, state.EmailSourceId);
    }

    // ── GetIngestionHistoryAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetIngestionHistoryAsync_ReturnsRunsForSource()
    {
        var otherSource = Guid.CreateVersion7();
        await _sut.StartRunAsync(TenantId, SourceId,     IngestionRunTriggerType.Manual,    null, null, null, null);
        await _sut.StartRunAsync(TenantId, SourceId,     IngestionRunTriggerType.Scheduled, null, null, null, null);
        await _sut.StartRunAsync(TenantId, otherSource,  IngestionRunTriggerType.Manual,    null, null, null, null);

        var history = await _sut.GetIngestionHistoryAsync(TenantId, SourceId, pageSize: 10, pageOffset: 0);

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal(SourceId, r.EmailSourceId));
    }

    [Fact]
    public async Task GetIngestionHistoryAsync_TenantIsolation()
    {
        var otherTenant = Guid.CreateVersion7();
        await _sut.StartRunAsync(TenantId,   SourceId, IngestionRunTriggerType.Manual, null, null, null, null);
        await _sut.StartRunAsync(otherTenant, SourceId, IngestionRunTriggerType.Manual, null, null, null, null);

        var history = await _sut.GetIngestionHistoryAsync(TenantId, SourceId, pageSize: 10, pageOffset: 0);
        Assert.Single(history);
    }
}
