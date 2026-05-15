using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

public sealed class TenantBillingProfileService : ITenantBillingProfileService
{
    private readonly ITenantBillingProfileRepository _repo;
    private readonly TimeProvider _clock;

    public TenantBillingProfileService(ITenantBillingProfileRepository repo, TimeProvider? clock = null)
    {
        _repo = repo;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<TenantBillingProfile> CreateAsync(
        Guid tenantId,
        Guid billingAccountId,
        string? hostPlatformKey,
        string? externalTenantId,
        string mode,
        string? notes,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        if (billingAccountId == Guid.Empty)
            throw new ArgumentException("BillingAccountId must be non-empty.", nameof(billingAccountId));
        if (!TenantBillingMode.IsValid(mode))
            throw new ArgumentException($"Unknown billing mode '{mode}'.", nameof(mode));

        if (await _repo.HasOpenProfileForTenantAsync(tenantId, ct))
        {
            throw new TenantBillingProfileConflictException(
                $"Tenant {tenantId} already has an open billing profile. " +
                "Close it before creating a new one.");
        }

        if (await _repo.IsBillingAccountClaimedAsync(billingAccountId, ct))
        {
            throw new TenantBillingProfileConflictException(
                $"BillingAccount {billingAccountId} is already claimed by another open profile.");
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var profile = TenantBillingProfile.CreateDraft(
            tenantId,
            billingAccountId,
            hostPlatformKey,
            externalTenantId,
            mode,
            notes,
            nowUtc);

        return await _repo.AddAsync(profile, ct);
    }

    public Task<TenantBillingProfile?> GetAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        if (profileId == Guid.Empty) throw new ArgumentException("ProfileId must be non-empty.", nameof(profileId));
        return _repo.GetByIdAsync(tenantId, profileId, ct);
    }

    public Task<TenantBillingProfile?> GetByBillingAccountAsync(
        Guid tenantId,
        Guid billingAccountId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        if (billingAccountId == Guid.Empty)
            throw new ArgumentException("BillingAccountId must be non-empty.", nameof(billingAccountId));
        return _repo.GetByBillingAccountAsync(tenantId, billingAccountId, ct);
    }

    public async Task<TenantBillingProfilePage> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));

        var effectivePage     = page < 1 ? 1 : page;
        var effectivePageSize = pageSize < 1
            ? ITenantBillingProfileService.DefaultPageSize
            : Math.Min(pageSize, ITenantBillingProfileService.MaxPageSize);

        var items = await _repo.ListAsync(tenantId, effectivePage, effectivePageSize, ct);
        var total = await _repo.CountAsync(tenantId, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)effectivePageSize);

        return new TenantBillingProfilePage(items, effectivePage, effectivePageSize, total, totalPages);
    }

    public Task<TenantBillingProfile> ActivateAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => TransitionAsync(tenantId, profileId, (p, now) =>
        {
            // Activating a Draft / Suspended profile must not collide with
            // another already-Active profile for the same tenant or the same
            // billing account.
            try { p.Activate(now); }
            catch (InvalidOperationException ex)
            { throw new InvalidTenantBillingProfileTransitionException(ex.Message); }
        }, requireActiveUniquenessCheck: true, ct);

    public Task<TenantBillingProfile> SuspendAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => TransitionAsync(tenantId, profileId, (p, now) =>
        {
            try { p.Suspend(now); }
            catch (InvalidOperationException ex)
            { throw new InvalidTenantBillingProfileTransitionException(ex.Message); }
        }, requireActiveUniquenessCheck: false, ct);

    public Task<TenantBillingProfile> CloseAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => TransitionAsync(tenantId, profileId, (p, now) =>
        {
            try { p.Close(now); }
            catch (InvalidOperationException ex)
            { throw new InvalidTenantBillingProfileTransitionException(ex.Message); }
        }, requireActiveUniquenessCheck: false, ct);

    private async Task<TenantBillingProfile> TransitionAsync(
        Guid tenantId,
        Guid profileId,
        Action<TenantBillingProfile, DateTime> transition,
        bool requireActiveUniquenessCheck,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must be non-empty.", nameof(tenantId));
        if (profileId == Guid.Empty) throw new ArgumentException("ProfileId must be non-empty.", nameof(profileId));

        var profile = await _repo.GetByIdAsync(tenantId, profileId, ct)
            ?? throw new TenantBillingProfileNotFoundException(
                $"Profile {profileId} not found for tenant {tenantId}.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        // Pre-flight uniqueness re-check on Activate: a tenant might have a
        // separate Active profile (e.g. created out-of-band) by the time the
        // operator clicks Activate on a Draft/Suspended row. The relational
        // unique index enforces this server-side too, but checking here lets
        // us return a clean 409 instead of a duplicate-key surprise.
        if (requireActiveUniquenessCheck && profile.Status != TenantBillingProfileStatus.Active)
        {
            var openTenant = await _repo.GetActiveByTenantAsync(tenantId, ct);
            if (openTenant is not null && openTenant.Id != profile.Id)
            {
                throw new TenantBillingProfileConflictException(
                    $"Tenant {tenantId} already has an Active profile ({openTenant.Id}); " +
                    "close it before activating another.");
            }

            if (await _repo.IsBillingAccountClaimedAsync(profile.BillingAccountId, ct))
            {
                // The repo flag is "any non-Closed profile claims it" — exclude
                // ourselves: if WE are the one currently claiming it (Draft /
                // Suspended), that's fine, we're about to flip our own row to
                // Active. Re-check by fetching the exact claimant.
                var claimant = await _repo.GetByBillingAccountAsync(tenantId, profile.BillingAccountId, ct);
                if (claimant is not null && claimant.Id != profile.Id
                    && claimant.Status == TenantBillingProfileStatus.Active)
                {
                    throw new TenantBillingProfileConflictException(
                        $"BillingAccount {profile.BillingAccountId} is already claimed by " +
                        $"another Active profile ({claimant.Id}).");
                }
            }
        }

        transition(profile, nowUtc);
        return await _repo.UpdateAsync(profile, ct);
    }
}
