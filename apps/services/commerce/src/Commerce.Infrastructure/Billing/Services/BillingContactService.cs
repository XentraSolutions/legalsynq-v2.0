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

public sealed class BillingContactService : IBillingContactService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateBillingContactRequest> _createValidator;
    private readonly IValidator<UpdateBillingContactRequest> _updateValidator;
    private readonly BillingAuditWriter _audit;

    public BillingContactService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateBillingContactRequest> createValidator,
        IValidator<UpdateBillingContactRequest> updateValidator,
        BillingAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _audit = audit;
    }

    public async Task<BillingContactResponse> AddAsync(Guid accountId, CreateBillingContactRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        await EnsureAccountAsync(accountId, ct);

        var existingForType = await _db.BillingContacts
            .Where(c => c.BillingAccountId == accountId && c.ContactType == request.ContactType)
            .ToListAsync(ct);

        var makePrimary = request.IsPrimary || existingForType.All(c => !c.IsPrimary);
        if (makePrimary)
        {
            foreach (var c in existingForType.Where(c => c.IsPrimary))
                c.SetPrimary(false, _clock.UtcNow);
        }

        var entity = BillingContact.Create(
            accountId, request.ContactType, request.Name, request.Email,
            request.Phone, makePrimary, _clock.UtcNow);
        _db.BillingContacts.Add(entity);

        _audit.Append(accountId, BillingAccountAuditEventTypes.BillingContactAdded,
            $"Contact added ({request.ContactType}/{request.Email}, primary={makePrimary}).");
        await _db.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyList<BillingContactResponse>> ListAsync(Guid accountId, CancellationToken ct)
    {
        await EnsureAccountAsync(accountId, ct);
        var items = await _db.BillingContacts.AsNoTracking()
            .Where(c => c.BillingAccountId == accountId)
            .OrderBy(c => c.ContactType).ThenByDescending(c => c.IsPrimary).ThenBy(c => c.Name)
            .ToListAsync(ct);
        return items.Select(BillingMappers.ToResponse).ToList();
    }

    public async Task<BillingContactResponse> UpdateAsync(Guid accountId, Guid contactId, UpdateBillingContactRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        await EnsureAccountAsync(accountId, ct);

        var entity = await _db.BillingContacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new NotFoundException("BillingContact", contactId.ToString());
        if (entity.BillingAccountId != accountId)
            throw new NotFoundException("BillingContact", contactId.ToString());

        var typeChanged = entity.ContactType != request.ContactType;
        entity.Update(request.ContactType, request.Name, request.Email, request.Phone, _clock.UtcNow);

        if (typeChanged && entity.IsPrimary)
        {
            // Demote any other primary in the new type bucket so the
            // "one primary per type" invariant continues to hold.
            var conflicting = await _db.BillingContacts
                .Where(c => c.BillingAccountId == accountId
                            && c.ContactType == request.ContactType
                            && c.Id != contactId
                            && c.IsPrimary)
                .ToListAsync(ct);
            foreach (var c in conflicting)
                c.SetPrimary(false, _clock.UtcNow);
        }

        _audit.Append(accountId, BillingAccountAuditEventTypes.BillingContactUpdated,
            $"Contact {contactId} updated ({request.ContactType}/{request.Email}).");
        await _db.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    public async Task<BillingContactResponse> MakePrimaryAsync(Guid accountId, Guid contactId, CancellationToken ct)
    {
        await EnsureAccountAsync(accountId, ct);
        var entity = await _db.BillingContacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new NotFoundException("BillingContact", contactId.ToString());
        if (entity.BillingAccountId != accountId)
            throw new NotFoundException("BillingContact", contactId.ToString());

        if (entity.IsPrimary)
            return entity.ToResponse();

        var others = await _db.BillingContacts
            .Where(c => c.BillingAccountId == accountId
                        && c.ContactType == entity.ContactType
                        && c.Id != contactId
                        && c.IsPrimary)
            .ToListAsync(ct);
        foreach (var c in others)
            c.SetPrimary(false, _clock.UtcNow);
        entity.SetPrimary(true, _clock.UtcNow);

        _audit.Append(accountId, BillingAccountAuditEventTypes.BillingContactMadePrimary,
            $"Contact {contactId} promoted to primary for {entity.ContactType}.");
        await _db.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    private async Task EnsureAccountAsync(Guid accountId, CancellationToken ct)
    {
        var exists = await _db.BillingAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId, ct);
        if (!exists) throw new NotFoundException("BillingAccount", accountId.ToString());
    }
}
