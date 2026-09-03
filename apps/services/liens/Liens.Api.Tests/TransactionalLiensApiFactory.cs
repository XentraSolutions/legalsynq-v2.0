using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Application.Services;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Liens.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Liens.Api.Tests;

internal sealed class TransactionalLiensApiFactory : LiensApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var contextServices = services
                .Where(descriptor => descriptor.ServiceType.FullName?.Contains("LiensDbContext") == true ||
                                     (descriptor.ServiceType.IsGenericType && descriptor.ServiceType
                                         .GetGenericArguments()
                                         .Any(argument => argument.FullName?.Contains("LiensDbContext") == true)))
                .ToList();
            foreach (var descriptor in contextServices)
                services.Remove(descriptor);

            services.AddSingleton<FailRootHistorySaveInterceptor>();
            services.AddDbContext<LiensDbContext>((serviceProvider, options) => options
                .UseInMemoryDatabase(DbName)
                .AddInterceptors(serviceProvider.GetRequiredService<FailRootHistorySaveInterceptor>())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, RollbackVerifyingUnitOfWork>();
        });
    }
}

internal sealed class FailRootHistorySaveInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfRootHistoryWrite(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfRootHistoryWrite(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void ThrowIfRootHistoryWrite(DbContext? context)
    {
        if (context is null)
            return;

        var hasRootMutation = context.ChangeTracker.Entries()
            .Any(entry => entry.Entity is Case or Lien &&
                          entry.State is EntityState.Modified or EntityState.Deleted);
        var hasCapturedHistory = context.ChangeTracker.Entries()
            .Any(entry => entry.State == EntityState.Added &&
                          entry.Entity is CaseUpdateHistory or LienStatusHistory);
        if (hasRootMutation && hasCapturedHistory)
            throw new InvalidOperationException("Simulated root history persistence failure.");
    }
}

internal sealed class FailAfterLienStatusHistoryRepository : ILienStatusHistoryRepository
{
    private readonly LienStatusHistoryRepository _inner;

    public FailAfterLienStatusHistoryRepository(LienStatusHistoryRepository inner)
    {
        _inner = inner;
    }

    public Task<List<LienStatusHistory>> GetByCaseIdAsync(
        Guid tenantId,
        Guid caseId,
        CancellationToken ct = default) =>
        _inner.GetByCaseIdAsync(tenantId, caseId, ct);

    public async Task AddAsync(LienStatusHistory entity, CancellationToken ct = default)
    {
        await _inner.AddAsync(entity, ct);
        throw new InvalidOperationException("Simulated lien-update history write failure.");
    }
}

internal sealed class FailInternalCaseUpdateNoteService : ILienCaseNoteService
{
    private readonly LienCaseNoteService _inner;

    public FailInternalCaseUpdateNoteService(LienCaseNoteService inner)
    {
        _inner = inner;
    }

    public Task<List<CaseNoteResponse>> GetNotesAsync(
        Guid tenantId,
        Guid caseId,
        CancellationToken ct = default) =>
        _inner.GetNotesAsync(tenantId, caseId, ct);

    public Task<CaseNoteResponse> CreateNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid actorUserId,
        CreateCaseNoteRequest request,
        CancellationToken ct = default)
    {
        if (string.Equals(request.Category, CaseNoteCategory.Internal, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Simulated case-update history write failure.");

        return _inner.CreateNoteAsync(tenantId, caseId, actorUserId, request, ct);
    }

    public Task<CaseNoteResponse> UpdateNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid noteId,
        Guid actorUserId,
        UpdateCaseNoteRequest request,
        CancellationToken ct = default) =>
        _inner.UpdateNoteAsync(tenantId, caseId, noteId, actorUserId, request, ct);

    public Task DeleteNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid noteId,
        Guid actorUserId,
        CancellationToken ct = default) =>
        _inner.DeleteNoteAsync(tenantId, caseId, noteId, actorUserId, ct);

    public Task<CaseNoteResponse> PinNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid noteId,
        Guid actorUserId,
        CancellationToken ct = default) =>
        _inner.PinNoteAsync(tenantId, caseId, noteId, actorUserId, ct);

    public Task<CaseNoteResponse> UnpinNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid noteId,
        Guid actorUserId,
        CancellationToken ct = default) =>
        _inner.UnpinNoteAsync(tenantId, caseId, noteId, actorUserId, ct);
}

internal sealed class RollbackVerifyingUnitOfWork : IUnitOfWork
{
    private readonly LiensDbContext _db;

    public RollbackVerifyingUnitOfWork(LiensDbContext db)
    {
        _db = db;
    }

    public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default)
    {
        var caseSnapshots = _db.ChangeTracker.Entries<Case>()
            .ToDictionary(
                entry => entry.Entity.Id,
                entry => entry.OriginalValues.Clone());
        var lienSnapshots = _db.ChangeTracker.Entries<Lien>()
            .ToDictionary(
                entry => entry.Entity.Id,
                entry => entry.OriginalValues.Clone());
        var existingNoteIds = _db.LienCaseNotes
            .AsNoTracking()
            .Select(note => note.Id)
            .ToHashSet();
        var existingHistoryIds = _db.LienStatusHistories
            .AsNoTracking()
            .Select(history => history.Id)
            .ToHashSet();

        return Task.FromResult<ITransactionScope>(new SnapshotTransaction(
            _db,
            caseSnapshots,
            lienSnapshots,
            existingNoteIds,
            existingHistoryIds));
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    private sealed class SnapshotTransaction : ITransactionScope
    {
        private readonly LiensDbContext _db;
        private readonly IReadOnlyDictionary<Guid, PropertyValues> _caseSnapshots;
        private readonly IReadOnlyDictionary<Guid, PropertyValues> _lienSnapshots;
        private readonly IReadOnlySet<Guid> _existingNoteIds;
        private readonly IReadOnlySet<Guid> _existingHistoryIds;

        public SnapshotTransaction(
            LiensDbContext db,
            IReadOnlyDictionary<Guid, PropertyValues> caseSnapshots,
            IReadOnlyDictionary<Guid, PropertyValues> lienSnapshots,
            IReadOnlySet<Guid> existingNoteIds,
            IReadOnlySet<Guid> existingHistoryIds)
        {
            _db = db;
            _caseSnapshots = caseSnapshots;
            _lienSnapshots = lienSnapshots;
            _existingNoteIds = existingNoteIds;
            _existingHistoryIds = existingHistoryIds;
        }

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            foreach (var (caseId, snapshot) in _caseSnapshots)
            {
                var caseEntity = await _db.Cases.SingleAsync(item => item.Id == caseId, ct);
                var entry = _db.Entry(caseEntity);
                entry.CurrentValues.SetValues(snapshot);
                entry.State = EntityState.Modified;
            }

            foreach (var (lienId, snapshot) in _lienSnapshots)
            {
                var lien = await _db.Liens.SingleAsync(item => item.Id == lienId, ct);
                var entry = _db.Entry(lien);
                entry.CurrentValues.SetValues(snapshot);
                entry.State = EntityState.Modified;
            }

            var addedNotes = await _db.LienCaseNotes
                .Where(note => !_existingNoteIds.Contains(note.Id))
                .ToListAsync(ct);
            _db.LienCaseNotes.RemoveRange(addedNotes);
            var addedHistories = await _db.LienStatusHistories
                .Where(history => !_existingHistoryIds.Contains(history.Id))
                .ToListAsync(ct);
            _db.LienStatusHistories.RemoveRange(addedHistories);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
