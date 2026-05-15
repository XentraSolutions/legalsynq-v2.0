using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.StatementTemplates;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Repositories;

/// <summary>
/// STAT-B02 — EF Core implementation of
/// <see cref="IStatementTemplateRepository"/>. Mirrors
/// <see cref="InvoiceTemplateRepository"/> but tenant-scoped only;
/// the <c>ApplyScope</c> helper requires a non-empty tenant id.
/// </summary>
public sealed class StatementTemplateRepository : IStatementTemplateRepository
{
    /// <summary>
    /// Name of the MySQL unique index on the <c>DefaultScopeKey</c>
    /// computed column. Matched as a substring of the
    /// <see cref="DbUpdateException"/> message so the catch is
    /// portable across MySQL/MariaDB/Pomelo error variants.
    /// </summary>
    private const string DefaultScopeUniqueIndexName = "UX_statement_templates_DefaultScopeKey";

    private readonly BillingDbContext _db;

    public StatementTemplateRepository(BillingDbContext db) => _db = db;

    public async Task<StatementTemplate> AddAsync(StatementTemplate template, CancellationToken ct = default)
    {
        await _db.StatementTemplates.AddAsync(template, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDefaultScopeUniqueViolation(ex))
        {
            throw new StatementTemplateDefaultConflictException(
                "Another statement template was concurrently set as the default for this tenant. " +
                "Re-fetch the current default and retry.");
        }
        return template;
    }

    public async Task UpdateAsync(StatementTemplate template, CancellationToken ct = default)
    {
        _db.StatementTemplates.Update(template);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDefaultScopeUniqueViolation(ex))
        {
            throw new StatementTemplateDefaultConflictException(
                "Another statement template was concurrently set as the default for this tenant. " +
                "Re-fetch the current default and retry.");
        }
    }

    private static bool IsDefaultScopeUniqueViolation(DbUpdateException ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur.Message.Contains(DefaultScopeUniqueIndexName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public Task<StatementTemplate?> GetByIdInScopeAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: false)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public Task<StatementTemplate?> GetByIdInScopeReadOnlyAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: true)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<StatementTemplate>> ListInScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await ScopedQuery(tenantId, asNoTracking: true)
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.UpdatedAtUtc)
            .ToListAsync(ct);
    }

    public Task<StatementTemplate?> GetDefaultInScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: true)
            .Where(t => t.IsDefault && t.Status == StatementTemplateStatus.Active)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> AnyDefaultInScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        return ScopedQuery(tenantId, asNoTracking: true)
            .AnyAsync(t => t.IsDefault, ct);
    }

    public async Task<int> UnsetDefaultsInScopeAsync(
        Guid tenantId,
        Guid exceptTemplateId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        if (_db.Database.IsRelational())
        {
            var query = _db.StatementTemplates.AsQueryable();
            query = ApplyScope(query, tenantId);
            return await query
                .Where(t => t.IsDefault && t.Id != exceptTemplateId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsDefault, false)
                    .SetProperty(t => t.UpdatedAtUtc, nowUtc), ct);
        }

        var existing = await ApplyScope(_db.StatementTemplates, tenantId)
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

    private IQueryable<StatementTemplate> ScopedQuery(Guid tenantId, bool asNoTracking)
    {
        var query = asNoTracking
            ? _db.StatementTemplates.AsNoTracking()
            : _db.StatementTemplates.AsQueryable();
        return ApplyScope(query, tenantId);
    }

    private static IQueryable<StatementTemplate> ApplyScope(IQueryable<StatementTemplate> query, Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return query.Where(t => t.TenantId == tenantId);
    }
}
