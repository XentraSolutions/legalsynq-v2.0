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

public sealed class BillingProfileService : IBillingProfileService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<UpdateBillingProfileRequest> _updateValidator;
    private readonly BillingAuditWriter _audit;

    public BillingProfileService(
        CommerceDbContext db,
        IClock clock,
        IValidator<UpdateBillingProfileRequest> updateValidator,
        BillingAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _updateValidator = updateValidator;
        _audit = audit;
    }

    public async Task<BillingProfileResponse> GetAsync(Guid accountId, CancellationToken ct)
    {
        await EnsureAccountExistsAsync(accountId, ct);
        var profile = await _db.BillingProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.BillingAccountId == accountId, ct)
            ?? throw new NotFoundException("BillingProfile", accountId.ToString());
        return profile.ToResponse();
    }

    public async Task<BillingProfileResponse> UpdateAsync(Guid accountId, UpdateBillingProfileRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        await EnsureAccountExistsAsync(accountId, ct);

        var profile = await _db.BillingProfiles
            .FirstOrDefaultAsync(p => p.BillingAccountId == accountId, ct);

        // Self-heal for accounts that predate profile auto-provisioning.
        // The new row + the field updates + the audit event are persisted in a
        // single SaveChanges so the audit-on-mutation invariant holds.
        if (profile is null)
        {
            profile = BillingProfile.CreateEmpty(accountId, _clock.UtcNow);
            _db.BillingProfiles.Add(profile);
        }

        profile.Update(
            request.AddressLine1, request.AddressLine2, request.City, request.StateRegion,
            request.PostalCode, request.Country, request.TaxId, request.TaxExempt, _clock.UtcNow);

        _audit.Append(accountId, BillingAccountAuditEventTypes.BillingProfileUpdated,
            "Billing profile updated.");
        await _db.SaveChangesAsync(ct);
        return profile.ToResponse();
    }

    private async Task EnsureAccountExistsAsync(Guid accountId, CancellationToken ct)
    {
        var accountExists = await _db.BillingAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId, ct);
        if (!accountExists)
            throw new NotFoundException("BillingAccount", accountId.ToString());
    }
}
