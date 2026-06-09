using Commerce.Domain.Billing;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Billing.Services;

/// <summary>
/// Generates the next <see cref="AccountNumber"/> by scanning existing
/// rows for the maximum sequence and adding one. The generator is
/// scoped to the request DbContext.
/// <para>
/// Concurrency limitation: under concurrent multi-instance writes two
/// callers can read the same MAX value and produce the same number. The
/// unique index <c>ux_billing_accounts_account_number</c> guarantees
/// only one of the duplicate inserts succeeds; on conflict the
/// application service retries the allocation. A dedicated sequence
/// table is deferred to a later block (documented in the report).
/// </para>
/// </summary>
public interface IAccountNumberGenerator
{
    Task<string> AllocateAsync(CancellationToken ct);
}

public sealed class AccountNumberGenerator : IAccountNumberGenerator
{
    private readonly CommerceDbContext _db;

    public AccountNumberGenerator(CommerceDbContext db) => _db = db;

    public async Task<string> AllocateAsync(CancellationToken ct)
    {
        var existing = await _db.BillingAccounts
            .AsNoTracking()
            .Select(x => x.AccountNumber)
            .ToListAsync(ct);

        long next = 1;
        foreach (var num in existing)
        {
            if (AccountNumber.TryParseSequence(num, out var seq) && seq >= next)
                next = seq + 1;
        }
        return AccountNumber.Format(next);
    }
}
