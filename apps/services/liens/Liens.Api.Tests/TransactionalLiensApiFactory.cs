using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Services;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
            services.RemoveAll<ILienCaseNoteService>();
            services.AddScoped<LienCaseNoteService>();
            services.AddScoped<ILienCaseNoteService>(serviceProvider =>
                new FailInternalCaseUpdateNoteService(
                    serviceProvider.GetRequiredService<LienCaseNoteService>()));

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, RollbackVerifyingUnitOfWork>();
        });
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
        var existingNoteIds = _db.LienCaseNotes
            .AsNoTracking()
            .Select(note => note.Id)
            .ToHashSet();

        return Task.FromResult<ITransactionScope>(new SnapshotTransaction(
            _db,
            caseSnapshots,
            existingNoteIds));
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    private sealed class SnapshotTransaction : ITransactionScope
    {
        private readonly LiensDbContext _db;
        private readonly IReadOnlyDictionary<Guid, PropertyValues> _caseSnapshots;
        private readonly IReadOnlySet<Guid> _existingNoteIds;

        public SnapshotTransaction(
            LiensDbContext db,
            IReadOnlyDictionary<Guid, PropertyValues> caseSnapshots,
            IReadOnlySet<Guid> existingNoteIds)
        {
            _db = db;
            _caseSnapshots = caseSnapshots;
            _existingNoteIds = existingNoteIds;
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

            var addedNotes = await _db.LienCaseNotes
                .Where(note => !_existingNoteIds.Contains(note.Id))
                .ToListAsync(ct);
            _db.LienCaseNotes.RemoveRange(addedNotes);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
