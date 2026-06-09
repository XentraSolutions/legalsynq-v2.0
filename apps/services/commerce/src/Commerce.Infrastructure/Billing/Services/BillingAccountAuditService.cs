using Commerce.Application.Billing.Abstractions;
using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Billing;
using Commerce.Infrastructure.Billing.Mapping;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Billing.Services;

public sealed class BillingAccountAuditService : IBillingAccountAuditService
{
    private readonly CommerceDbContext _db;

    public BillingAccountAuditService(CommerceDbContext db) => _db = db;

    public async Task<IReadOnlyList<BillingAccountAuditEventResponse>> ListAsync(Guid accountId, CancellationToken ct)
    {
        var exists = await _db.BillingAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId, ct);
        if (!exists) throw new NotFoundException("BillingAccount", accountId.ToString());

        var items = await _db.BillingAccountAuditEvents.AsNoTracking()
            .Where(e => e.BillingAccountId == accountId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
        return items.Select(BillingMappers.ToResponse).ToList();
    }
}
