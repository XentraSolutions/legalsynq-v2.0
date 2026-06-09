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

public sealed class BillingAccountService : IBillingAccountService
{
    private const int AccountNumberMaxAttempts = 5;

    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateBillingAccountRequest> _createValidator;
    private readonly IValidator<UpdateBillingAccountRequest> _updateValidator;
    private readonly IAccountNumberGenerator _numbers;
    private readonly BillingAuditWriter _audit;

    public BillingAccountService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateBillingAccountRequest> createValidator,
        IValidator<UpdateBillingAccountRequest> updateValidator,
        IAccountNumberGenerator numbers,
        BillingAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _numbers = numbers;
        _audit = audit;
    }

    public async Task<BillingAccountResponse> CreateAsync(CreateBillingAccountRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        for (var attempt = 1; attempt <= AccountNumberMaxAttempts; attempt++)
        {
            var number = await _numbers.AllocateAsync(ct);
            var account = BillingAccount.Create(
                number, request.DisplayName, request.LegalName, request.DefaultCurrency, _clock.UtcNow);
            _db.BillingAccounts.Add(account);
            // BillingProfile is created eagerly so the 1:1 invariant always holds.
            _db.BillingProfiles.Add(BillingProfile.CreateEmpty(account.Id, _clock.UtcNow));
            _audit.Append(account.Id, BillingAccountAuditEventTypes.AccountCreated,
                $"Account '{account.AccountNumber}' created.");

            try
            {
                await _db.SaveChangesAsync(ct);
                return account.ToResponse();
            }
            catch (DbUpdateException)
            {
                // Likely AccountNumber unique-collision under concurrent insert. Reset.
                foreach (var entry in _db.ChangeTracker.Entries().ToList())
                {
                    if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
                }
                if (attempt >= AccountNumberMaxAttempts)
                    throw new DuplicateKeyException("BillingAccount", "AccountNumber");
                // else loop and retry with a fresh number.
            }
        }

        // Unreachable: loop either returns on success or throws on the final attempt.
        throw new DuplicateKeyException("BillingAccount", "AccountNumber");
    }

    public async Task<IReadOnlyList<BillingAccountResponse>> ListAsync(CancellationToken ct)
    {
        var items = await _db.BillingAccounts.AsNoTracking()
            .OrderBy(x => x.AccountNumber)
            .ToListAsync(ct);
        return items.Select(BillingMappers.ToResponse).ToList();
    }

    public async Task<BillingAccountResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var account = await _db.BillingAccounts.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("BillingAccount", id.ToString());
        return account.ToResponse();
    }

    public async Task<BillingAccountResponse> UpdateAsync(Guid id, UpdateBillingAccountRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var account = await _db.BillingAccounts.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("BillingAccount", id.ToString());

        try { account.Update(request.DisplayName, request.LegalName, request.DefaultCurrency, _clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }

        _audit.Append(account.Id, BillingAccountAuditEventTypes.AccountUpdated,
            $"Account '{account.AccountNumber}' header updated.");
        await _db.SaveChangesAsync(ct);
        return account.ToResponse();
    }

    public Task<BillingAccountResponse> ActivateAsync(Guid id, CancellationToken ct)
        => TransitionAsync(id, a => a.Activate(_clock.UtcNow),
            BillingAccountAuditEventTypes.AccountActivated, "activated", ct);

    public Task<BillingAccountResponse> SuspendAsync(Guid id, CancellationToken ct)
        => TransitionAsync(id, a => a.Suspend(_clock.UtcNow),
            BillingAccountAuditEventTypes.AccountSuspended, "suspended", ct);

    public Task<BillingAccountResponse> CloseAsync(Guid id, CancellationToken ct)
        => TransitionAsync(id, a => a.Close(_clock.UtcNow),
            BillingAccountAuditEventTypes.AccountClosed, "closed", ct);

    private async Task<BillingAccountResponse> TransitionAsync(
        Guid id, Action<BillingAccount> apply, string eventType, string verb, CancellationToken ct)
    {
        var account = await _db.BillingAccounts.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("BillingAccount", id.ToString());

        try { apply(account); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }

        _audit.Append(account.Id, eventType, $"Account '{account.AccountNumber}' {verb}.");
        await _db.SaveChangesAsync(ct);
        return account.ToResponse();
    }
}
