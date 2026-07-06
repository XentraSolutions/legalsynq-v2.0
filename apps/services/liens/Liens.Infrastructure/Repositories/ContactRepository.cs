using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly LiensDbContext _db;

    public ContactRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Contacts
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Contact> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? contactType, bool? isActive,
        int page, int pageSize, Guid? lawFirmId = null, Guid? facilityId = null, string? contactSubtype = null, CancellationToken ct = default)
    {
        var q = _db.Contacts.Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c =>
                c.FirstName.Contains(term) ||
                c.LastName.Contains(term) ||
                c.DisplayName.Contains(term) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Organization != null && c.Organization.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(contactType))
            q = q.Where(c => c.ContactType == contactType);

        if (lawFirmId.HasValue)
            q = q.Where(c => c.LawFirmId == lawFirmId.Value);

        if (facilityId.HasValue)
            q = q.Where(c => c.FacilityId == facilityId.Value);

        if (!string.IsNullOrWhiteSpace(contactSubtype))
            q = q.Where(c => c.ContactSubtype == contactSubtype);

        if (isActive.HasValue)
            q = q.Where(c => c.IsActive == isActive.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(List<Contact> Items, int TotalCount)> SearchFacilityContactsAsync(
        Guid tenantId, string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var q = BuildFacilityContactsQuery(tenantId, search, isActive);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderBy(c => c.Organization ?? c.DisplayName)
            .ThenBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<Contact>> GetAllByTypeAsync(
        Guid tenantId, string? contactType, bool? isActive, CancellationToken ct = default)
    {
        var q = _db.Contacts.Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(contactType))
            q = q.Where(c => c.ContactType == contactType);

        if (isActive.HasValue)
            q = q.Where(c => c.IsActive == isActive.Value);

        return await q.OrderBy(c => c.DisplayName).ToListAsync(ct);
    }

    public async Task<Contact?> GetFacilityContactByReferenceAsync(
        Guid tenantId, Guid facilityReferenceId, CancellationToken ct = default)
    {
        return await _db.Contacts
            .Where(c => c.TenantId == tenantId
                && (c.ContactType == "Facility" || c.ContactType == "MedicalFacility")
                && c.ContactSubtype == null
                && (c.Id == facilityReferenceId || c.FacilityId == facilityReferenceId))
            .OrderByDescending(c => c.Id == facilityReferenceId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Contact?> GetFacilityContactByNameAsync(
        Guid tenantId, string facilityName, CancellationToken ct = default)
    {
        var term = facilityName.Trim();
        return await _db.Contacts
            .Where(c => c.TenantId == tenantId
                && (c.ContactType == "Facility" || c.ContactType == "MedicalFacility")
                && c.ContactSubtype == null
                && ((c.Organization != null && c.Organization == term) || c.DisplayName == term))
            .OrderBy(c => c.DisplayName)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Contact>> GetByFacilityAsync(
        Guid tenantId, Guid facilityId, string? contactSubtype = null, bool? isActive = true, CancellationToken ct = default)
    {
        var q = _db.Contacts.Where(c => c.TenantId == tenantId && c.FacilityId == facilityId);

        if (!string.IsNullOrWhiteSpace(contactSubtype))
            q = q.Where(c => c.ContactSubtype == contactSubtype);

        if (isActive.HasValue)
            q = q.Where(c => c.IsActive == isActive.Value);

        return await q.OrderBy(c => c.DisplayName).ToListAsync(ct);
    }

    public async Task AddAsync(Contact entity, CancellationToken ct = default)
    {
        await _db.Contacts.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Contact entity, CancellationToken ct = default)
    {
        _db.Contacts.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<Contact> BuildFacilityContactsQuery(
        Guid tenantId,
        string? search,
        bool? isActive)
    {
        var q = _db.Contacts.Where(c =>
            c.TenantId == tenantId &&
            (c.ContactType == "Facility" || c.ContactType == "MedicalFacility") &&
            c.ContactSubtype == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c =>
                c.FirstName.Contains(term) ||
                c.LastName.Contains(term) ||
                c.DisplayName.Contains(term) ||
                (c.Organization != null && c.Organization.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)));
        }

        if (isActive.HasValue)
            q = q.Where(c => c.IsActive == isActive.Value);

        return q;
    }
}
