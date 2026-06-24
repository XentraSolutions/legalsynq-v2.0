using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Invoicing.Abstractions;
using Commerce.Contracts.Invoicing;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Invoicing.Mapping;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Invoicing.Services;

public sealed class InvoiceService : IInvoiceService
{
    private const int MaxAllocateRetries = 5;

    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IInvoiceNumberGenerator _numbers;
    private readonly IValidator<CreateInvoiceRequest> _validator;

    public InvoiceService(
        CommerceDbContext db,
        IClock clock,
        IInvoiceNumberGenerator numbers,
        IValidator<CreateInvoiceRequest> validator)
    {
        _db = db;
        _clock = clock;
        _numbers = numbers;
        _validator = validator;
    }

    public async Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var account = await _db.BillingAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.BillingAccountId, ct)
            ?? throw new NotFoundException("BillingAccount", request.BillingAccountId.ToString());

        Subscription? subscription = null;
        if (request.SubscriptionId.HasValue)
        {
            subscription = await _db.Subscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId.Value, ct)
                ?? throw new NotFoundException("Subscription", request.SubscriptionId.Value.ToString());
            if (subscription.BillingAccountId != account.Id)
                throw new InvalidRelationshipException(
                    $"Subscription '{subscription.Id}' does not belong to BillingAccount '{account.Id}'.");
        }

        if (request.SubscriptionId.HasValue)
        {
            // Validate that any provided subscription-item line links resolve
            // to items that belong to the supplied subscription.
            var requestedItemIds = request.Lines
                .Where(l => l.SubscriptionItemId.HasValue)
                .Select(l => l.SubscriptionItemId!.Value)
                .Distinct()
                .ToArray();
            if (requestedItemIds.Length > 0)
            {
                var validItemIds = await _db.SubscriptionItems.AsNoTracking()
                    .Where(i => i.SubscriptionId == request.SubscriptionId.Value
                                && requestedItemIds.Contains(i.Id))
                    .Select(i => i.Id)
                    .ToListAsync(ct);
                var missing = requestedItemIds.Except(validItemIds).ToArray();
                if (missing.Length > 0)
                    throw new InvalidRelationshipException(
                        $"Invoice line subscription item '{missing[0]}' does not belong to subscription '{request.SubscriptionId.Value}'.");
            }
        }
        else
        {
            // No subscription supplied — line items must not reference one either.
            if (request.Lines.Any(l => l.SubscriptionItemId.HasValue))
                throw new InvalidRelationshipException(
                    "Invoice lines cannot reference SubscriptionItemId without a parent SubscriptionId.");
        }

        var now = _clock.UtcNow;
        var currency = request.Currency.ToUpperInvariant();

        for (var attempt = 0; attempt < MaxAllocateRetries; attempt++)
        {
            var number = await _numbers.AllocateAsync(ct);

            var invoice = Invoice.Create(
                account.Id,
                request.SubscriptionId,
                number,
                currency,
                now,
                request.DueDateUtc,
                InvoiceStatus.Open,
                now);

            var lines = request.Lines
                .Select(l => InvoiceLine.Create(
                    invoice.Id,
                    l.SubscriptionItemId,
                    l.Description,
                    l.Quantity,
                    l.UnitAmountMinor,
                    currency,
                    l.ServicePeriodStartUtc,
                    l.ServicePeriodEndUtc,
                    now))
                .ToList();

            invoice.Recalculate(lines, now);

            _db.Invoices.Add(invoice);
            _db.InvoiceLines.AddRange(lines);
            try
            {
                await _db.SaveChangesAsync(ct);
                return invoice.ToResponse(lines);
            }
            catch (DbUpdateException) when (attempt + 1 < MaxAllocateRetries)
            {
                _db.Entry(invoice).State = EntityState.Detached;
                foreach (var l in lines) _db.Entry(l).State = EntityState.Detached;
            }
        }

        throw new FinancialRecordConflictException("Invoice", "Could not allocate a unique invoice number.");
    }

    public async Task<InvoiceResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Invoice", id.ToString());
        var lines = await _db.InvoiceLines.AsNoTracking()
            .Where(l => l.InvoiceId == id)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(ct);
        return invoice.ToResponse(lines);
    }

    public async Task<IReadOnlyList<InvoiceResponse>> ListAsync(int take, CancellationToken ct)
        => await ProjectAsync(_db.Invoices.AsNoTracking().OrderByDescending(i => i.CreatedAtUtc), take, ct);

    public async Task<IReadOnlyList<InvoiceResponse>> ListForBillingAccountAsync(
        Guid billingAccountId, CancellationToken ct)
        => await ProjectAsync(
            _db.Invoices.AsNoTracking()
                .Where(i => i.BillingAccountId == billingAccountId)
                .OrderByDescending(i => i.CreatedAtUtc),
            500, ct);

    public async Task<IReadOnlyList<InvoiceResponse>> ListForSubscriptionAsync(
        Guid subscriptionId, CancellationToken ct)
        => await ProjectAsync(
            _db.Invoices.AsNoTracking()
                .Where(i => i.SubscriptionId == subscriptionId)
                .OrderByDescending(i => i.CreatedAtUtc),
            500, ct);

    private async Task<IReadOnlyList<InvoiceResponse>> ProjectAsync(
        IQueryable<Invoice> query, int take, CancellationToken ct)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        var invoices = await query.Take(take).ToListAsync(ct);
        if (invoices.Count == 0) return Array.Empty<InvoiceResponse>();
        var ids = invoices.Select(i => i.Id).ToArray();
        var lines = await _db.InvoiceLines.AsNoTracking()
            .Where(l => ids.Contains(l.InvoiceId))
            .ToListAsync(ct);
        var grouped = lines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => (IReadOnlyList<InvoiceLine>)g.OrderBy(x => x.CreatedAtUtc).ToList());
        return invoices.Select(i => i.ToResponse(grouped.TryGetValue(i.Id, out var ls) ? ls : Array.Empty<InvoiceLine>())).ToList();
    }
}
