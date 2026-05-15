using Commerce.Domain.Invoicing;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Invoicing.Services;

/// <summary>
/// Allocates the next <see cref="InvoiceNumber"/> using max+1 over
/// existing rows. Mirrors the semantics of the other number
/// generators in the project: the unique index
/// <c>ux_invoices_invoice_number</c> is the source of truth for
/// uniqueness; the application service retries on conflict.
/// </summary>
public interface IInvoiceNumberGenerator
{
    Task<string> AllocateAsync(CancellationToken ct);
}

public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly CommerceDbContext _db;

    public InvoiceNumberGenerator(CommerceDbContext db) => _db = db;

    public async Task<string> AllocateAsync(CancellationToken ct)
    {
        var existing = await _db.Invoices
            .AsNoTracking()
            .Select(x => x.InvoiceNumber)
            .ToListAsync(ct);

        long next = 1;
        foreach (var num in existing)
        {
            if (InvoiceNumber.TryParseSequence(num, out var seq) && seq >= next)
                next = seq + 1;
        }
        return InvoiceNumber.Format(next);
    }
}
