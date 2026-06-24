using Commerce.Application.Common.Time;
using Commerce.Application.Invoicing.Abstractions;
using Commerce.Contracts.Invoicing;
using Commerce.Domain.Invoicing;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Invoicing.Services;

/// <summary>
/// Concrete implementation of <see cref="IInvoiceBrandingService"/>.
/// Persists exactly one row keyed by <see cref="InvoiceBranding.SingletonId"/>.
/// On first read, self-heals by inserting a default row so the admin UI
/// can render a sensible empty state without a separate "create" call.
/// </summary>
public sealed class InvoiceBrandingService : IInvoiceBrandingService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<UpdateInvoiceBrandingRequest> _updateValidator;

    public InvoiceBrandingService(
        CommerceDbContext db,
        IClock clock,
        IValidator<UpdateInvoiceBrandingRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _updateValidator = updateValidator;
    }

    public async Task<InvoiceBrandingResponse> GetAsync(CancellationToken ct)
    {
        var row = await _db.InvoiceBrandings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == InvoiceBranding.SingletonId, ct);

        if (row is null)
        {
            // Self-heal: insert the default row so the admin UI sees a
            // baseline branding object on first ever GET. Concurrent first
            // GETs may both try to insert; we tolerate the race by retrying
            // the read on a unique-key violation.
            row = InvoiceBranding.CreateDefault(_clock.UtcNow);
            _db.InvoiceBrandings.Add(row);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.Entry(row).State = EntityState.Detached;
                row = await _db.InvoiceBrandings.AsNoTracking()
                    .FirstAsync(b => b.Id == InvoiceBranding.SingletonId, ct);
            }
        }

        return ToResponse(row);
    }

    public async Task<InvoiceBrandingResponse> UpdateAsync(
        UpdateInvoiceBrandingRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);

        var row = await LoadOrCreateForUpdateAsync(ct);
        var now = _clock.UtcNow;

        row.Update(
            request.CompanyName,
            request.LogoUrl,
            request.AccentColorHex,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateRegion,
            request.PostalCode,
            request.Country,
            request.ContactEmail,
            request.ContactPhone,
            request.Website,
            request.FooterText,
            now);

        await _db.SaveChangesAsync(ct);
        return ToResponse(row);
    }

    /// <summary>
    /// Returns a tracked singleton row, creating it if absent. Tolerates the
    /// "two concurrent first writes" race: if our INSERT collides with a
    /// concurrent insert (the primary-key uniqueness on <see cref="InvoiceBranding.SingletonId"/>
    /// is what enforces the singleton invariant), we detach the rejected
    /// entity, re-read the now-existing row tracked, and let the caller
    /// apply the update on top of it.
    /// </summary>
    private async Task<InvoiceBranding> LoadOrCreateForUpdateAsync(CancellationToken ct)
    {
        var row = await _db.InvoiceBrandings
            .FirstOrDefaultAsync(b => b.Id == InvoiceBranding.SingletonId, ct);
        if (row is not null) return row;

        var seed = InvoiceBranding.CreateDefault(_clock.UtcNow);
        _db.InvoiceBrandings.Add(seed);
        try
        {
            await _db.SaveChangesAsync(ct);
            return seed;
        }
        catch (DbUpdateException)
        {
            _db.Entry(seed).State = EntityState.Detached;
            return await _db.InvoiceBrandings
                .FirstAsync(b => b.Id == InvoiceBranding.SingletonId, ct);
        }
    }

    private static InvoiceBrandingResponse ToResponse(InvoiceBranding b) =>
        new(
            b.CompanyName,
            b.LogoUrl,
            b.AccentColorHex,
            b.AddressLine1,
            b.AddressLine2,
            b.City,
            b.StateRegion,
            b.PostalCode,
            b.Country,
            b.ContactEmail,
            b.ContactPhone,
            b.Website,
            b.FooterText,
            b.CreatedAtUtc,
            b.UpdatedAtUtc);
}
