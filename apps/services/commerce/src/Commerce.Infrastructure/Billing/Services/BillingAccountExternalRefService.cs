using Commerce.Application.Billing.Abstractions;
using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Contracts.Billing;
using Commerce.Domain.Billing;
using Commerce.Infrastructure.Billing.Mapping;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Billing.Services;

public sealed class BillingAccountExternalRefService : IBillingAccountExternalRefService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateExternalRefRequest> _createValidator;
    private readonly IValidator<UpdateExternalRefRequest> _updateValidator;
    private readonly BillingAuditWriter _audit;

    public BillingAccountExternalRefService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateExternalRefRequest> createValidator,
        IValidator<UpdateExternalRefRequest> updateValidator,
        BillingAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _audit = audit;
    }

    public async Task<ExternalRefResponse> AddAsync(Guid accountId, CreateExternalRefRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var account = await EnsureAccountAsync(accountId, ct);

        var hostKey = HostPlatformKey.Normalize(request.HostPlatformKey);
        var tenantId = request.ExternalTenantId.Trim();

        if (await _db.BillingAccountExternalRefs
                .AsNoTracking()
                .AnyAsync(r => r.HostPlatformKey == hostKey && r.ExternalTenantId == tenantId, ct))
        {
            throw new DuplicateKeyException("BillingAccountExternalRef", $"{hostKey}|{tenantId}");
        }

        var existingRefs = await _db.BillingAccountExternalRefs
            .Where(r => r.BillingAccountId == accountId)
            .ToListAsync(ct);

        var makePrimary = request.IsPrimary || existingRefs.Count == 0;
        if (makePrimary)
        {
            foreach (var r in existingRefs)
                r.SetPrimary(false, _clock.UtcNow);
        }

        var entity = BillingAccountExternalRef.Create(
            accountId, request.HostPlatformKey, request.ExternalTenantId,
            request.ExternalCustomerRef, makePrimary, _clock.UtcNow);
        _db.BillingAccountExternalRefs.Add(entity);

        _audit.Append(accountId, BillingAccountAuditEventTypes.ExternalRefAdded,
            $"External ref added: {hostKey}|{tenantId} (primary={makePrimary}).");
        await _db.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyList<ExternalRefResponse>> ListAsync(Guid accountId, CancellationToken ct)
    {
        await EnsureAccountAsync(accountId, ct);
        var items = await _db.BillingAccountExternalRefs.AsNoTracking()
            .Where(r => r.BillingAccountId == accountId)
            .OrderByDescending(r => r.IsPrimary)
            .ThenBy(r => r.HostPlatformKey).ThenBy(r => r.ExternalTenantId)
            .ToListAsync(ct);
        return items.Select(BillingMappers.ToResponse).ToList();
    }

    public async Task<ExternalRefResponse> UpdateAsync(Guid accountId, Guid refId, UpdateExternalRefRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        await EnsureAccountAsync(accountId, ct);

        var entity = await _db.BillingAccountExternalRefs.FindAsync(new object[] { refId }, ct)
            ?? throw new NotFoundException("BillingAccountExternalRef", refId.ToString());
        if (entity.BillingAccountId != accountId)
            throw new NotFoundException("BillingAccountExternalRef", refId.ToString());

        var newHost = HostPlatformKey.Normalize(request.HostPlatformKey);
        var newTenant = request.ExternalTenantId.Trim();
        if ((newHost != entity.HostPlatformKey || newTenant != entity.ExternalTenantId) &&
            await _db.BillingAccountExternalRefs.AsNoTracking()
                .AnyAsync(r => r.Id != refId && r.HostPlatformKey == newHost && r.ExternalTenantId == newTenant, ct))
        {
            throw new DuplicateKeyException("BillingAccountExternalRef", $"{newHost}|{newTenant}");
        }

        entity.Update(request.HostPlatformKey, request.ExternalTenantId, request.ExternalCustomerRef, _clock.UtcNow);
        _audit.Append(accountId, BillingAccountAuditEventTypes.ExternalRefUpdated,
            $"External ref {refId} updated to {newHost}|{newTenant}.");
        await _db.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    public async Task<ExternalRefResponse> MakePrimaryAsync(Guid accountId, Guid refId, CancellationToken ct)
    {
        await EnsureAccountAsync(accountId, ct);
        var entity = await _db.BillingAccountExternalRefs.FindAsync(new object[] { refId }, ct)
            ?? throw new NotFoundException("BillingAccountExternalRef", refId.ToString());
        if (entity.BillingAccountId != accountId)
            throw new NotFoundException("BillingAccountExternalRef", refId.ToString());

        if (entity.IsPrimary)
            return entity.ToResponse();

        var others = await _db.BillingAccountExternalRefs
            .Where(r => r.BillingAccountId == accountId && r.Id != refId && r.IsPrimary)
            .ToListAsync(ct);
        foreach (var r in others)
            r.SetPrimary(false, _clock.UtcNow);
        entity.SetPrimary(true, _clock.UtcNow);

        _audit.Append(accountId, BillingAccountAuditEventTypes.ExternalRefMadePrimary,
            $"External ref {refId} promoted to primary.");
        await _db.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    private async Task<BillingAccount> EnsureAccountAsync(Guid accountId, CancellationToken ct)
    {
        return await _db.BillingAccounts.FindAsync(new object[] { accountId }, ct)
            ?? throw new NotFoundException("BillingAccount", accountId.ToString());
    }
}
