using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Domain.Services;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Repositories;

public sealed class InvoiceTemplateRepository : IInvoiceTemplateRepository
{
    /// <summary>
    /// Name of the MySQL unique index on the <c>DefaultScopeKey</c>
    /// computed column. Used to translate a duplicate-key
    /// <see cref="DbUpdateException"/> into the domain-typed
    /// <see cref="InvoiceTemplateDefaultConflictException"/>.
    /// </summary>
    private const string DefaultScopeUniqueIndexName = "UX_invoice_templates_DefaultScopeKey";

    private readonly BillingDbContext _db;

    public InvoiceTemplateRepository(BillingDbContext db) => _db = db;

    public async Task<InvoiceTemplate> AddAsync(InvoiceTemplate template, CancellationToken ct = default)
    {
        await _db.InvoiceTemplates.AddAsync(template, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDefaultScopeUniqueViolation(ex))
        {
            throw new InvoiceTemplateDefaultConflictException(
                "Another template was concurrently set as the default for this scope. " +
                "Re-fetch the current default and retry.");
        }
        return template;
    }

    public async Task UpdateAsync(InvoiceTemplate template, CancellationToken ct = default)
    {
        // Caller fetched via GetByIdInScopeAsync (tracked) and mutated
        // in place; we just need a SaveChanges. The explicit UPDATE
        // method exists so the service contract reads symmetrically
        // with AddAsync and so future swaps to a non-EF store don't
        // need the callers to know about change-tracking.
        _db.InvoiceTemplates.Update(template);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDefaultScopeUniqueViolation(ex))
        {
            throw new InvoiceTemplateDefaultConflictException(
                "Another template was concurrently set as the default for this scope. " +
                "Re-fetch the current default and retry.");
        }
    }

    /// <summary>
    /// Recognises a duplicate-key error on the
    /// <c>UX_invoice_templates_DefaultScopeKey</c> unique index. We
    /// match on the index name (substring) to stay portable across
    /// MySQL/MariaDB/Pomelo error message variants without taking a
    /// hard dependency on the provider's exception type.
    /// </summary>
    private static bool IsDefaultScopeUniqueViolation(DbUpdateException ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur.Message.Contains(DefaultScopeUniqueIndexName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public Task<InvoiceTemplate?> GetByIdInScopeAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: false)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public Task<InvoiceTemplate?> GetByIdInScopeReadOnlyAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: true)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<InvoiceTemplate>> ListInScopeAsync(Guid? tenantId, CancellationToken ct = default)
    {
        return await ScopedQuery(tenantId, asNoTracking: true)
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.UpdatedAtUtc)
            .ToListAsync(ct);
    }

    public Task<InvoiceTemplate?> GetDefaultInScopeAsync(Guid? tenantId, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: true)
            .Where(t => t.IsDefault && t.Status == InvoiceTemplateStatus.Active)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> AnyDefaultInScopeAsync(Guid? tenantId, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: true)
            .AnyAsync(t => t.IsDefault, ct);
    }

    public async Task<int> UnsetDefaultsInScopeAsync(
        Guid? tenantId,
        Guid exceptTemplateId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        // Use the relational ExecuteUpdateAsync path so the unset is a
        // single round-trip on MySQL. EF InMemory does not implement
        // ExecuteUpdateAsync faithfully (it returns 0 rows even when
        // matches exist), so for non-relational providers we fall back
        // to a tracked load + SaveChanges so tests behave the same as
        // production. Either path participates in the surrounding
        // transaction opened by IUnitOfWork.
        if (_db.Database.IsRelational())
        {
            var query = _db.InvoiceTemplates.AsQueryable();
            query = ApplyScope(query, tenantId);
            return await query
                .Where(t => t.IsDefault && t.Id != exceptTemplateId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsDefault, false)
                    .SetProperty(t => t.UpdatedAtUtc, nowUtc), ct);
        }

        var existing = await ApplyScope(_db.InvoiceTemplates, tenantId)
            .Where(t => t.IsDefault && t.Id != exceptTemplateId)
            .ToListAsync(ct);

        foreach (var t in existing)
        {
            t.IsDefault = false;
            t.UpdatedAtUtc = nowUtc;
        }

        if (existing.Count > 0)
            await _db.SaveChangesAsync(ct);

        return existing.Count;
    }

    private IQueryable<InvoiceTemplate> ScopedQuery(Guid? tenantId, bool asNoTracking)
    {
        var query = asNoTracking
            ? _db.InvoiceTemplates.AsNoTracking()
            : _db.InvoiceTemplates.AsQueryable();
        return ApplyScope(query, tenantId);
    }

    private static IQueryable<InvoiceTemplate> ApplyScope(IQueryable<InvoiceTemplate> query, Guid? tenantId)
    {
        // Platform scope = OwnerType=Platform AND BillingAccountId IS NULL.
        // Tenant scope = OwnerType=Tenant AND BillingAccountId == tenantId.
        // We require BOTH columns to match because OwnerType alone or
        // BillingAccountId alone could be wrong without a database
        // constraint to hold them together.
        if (tenantId is null)
        {
            return query.Where(t =>
                t.OwnerType == InvoiceTemplateOwnerType.Platform &&
                t.BillingAccountId == null);
        }

        var tid = tenantId.Value;
        return query.Where(t =>
            t.OwnerType == InvoiceTemplateOwnerType.Tenant &&
            t.BillingAccountId == tid);
    }
}
