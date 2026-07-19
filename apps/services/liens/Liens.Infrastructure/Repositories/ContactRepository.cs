using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private const string LegacyMetadataMarker = "[legacy-meta]";
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

    public async Task<List<Contact>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await _db.Contacts
            .Where(c => c.TenantId == tenantId && ids.Contains(c.Id))
            .ToListAsync(ct);
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

        if (string.Equals(contactType, ContactType.LawFirm, StringComparison.Ordinal) &&
            !lawFirmId.HasValue &&
            contactSubtype is null)
        {
            q = q.Where(c => c.ContactSubtype == null || c.ContactSubtype == string.Empty);
        }

        if (lawFirmId.HasValue)
            q = q.Where(c => c.LawFirmId == lawFirmId.Value);

        if (facilityId.HasValue)
            q = q.Where(c => c.FacilityId == facilityId.Value);

        if (contactSubtype is not null)
        {
            if (string.IsNullOrWhiteSpace(contactSubtype))
            {
                q = q.Where(c => c.ContactSubtype == null || c.ContactSubtype == string.Empty);
            }
            else
            {
                q = q.Where(c => c.ContactSubtype == contactSubtype);
            }
        }

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

    public async Task<Dictionary<Guid, int>> GetActiveCaseCountsAsync(
        Guid tenantId,
        IReadOnlyCollection<Contact> contacts,
        CancellationToken ct = default)
    {
        var result = contacts
            .Select(c => c.Id)
            .Distinct()
            .ToDictionary(id => id, _ => 0, EqualityComparer<Guid>.Default);

        if (contacts.Count == 0)
            return result;

        var contactCaseIds = result.Keys.ToDictionary(id => id, _ => new HashSet<Guid>(), EqualityComparer<Guid>.Default);
        var activeCases = await _db.Cases
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status != CaseStatus.CaseSettled)
            .Select(c => new ActiveCaseLink(c.Id, c.OrgId, c.Notes))
            .ToListAsync(ct);

        if (activeCases.Count == 0)
            return result;

        await ApplyLawFirmCountsAsync(tenantId, contacts, activeCases, contactCaseIds, ct);
        ApplyMetadataCaseCounts(contacts, activeCases, contactCaseIds, "leadId", IsLeadContact);
        ApplyMetadataCaseCounts(contacts, activeCases, contactCaseIds, "caseManagerId", IsCaseManagerContact);
        await ApplyLienLinkedCountsAsync(tenantId, contacts, activeCases, contactCaseIds, ct);

        foreach (var (contactId, caseIds) in contactCaseIds)
            result[contactId] = caseIds.Count;

        return result;
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
    private async Task ApplyLawFirmCountsAsync(
        Guid tenantId,
        IReadOnlyCollection<Contact> contacts,
        IReadOnlyCollection<ActiveCaseLink> activeCases,
        Dictionary<Guid, HashSet<Guid>> contactCaseIds,
        CancellationToken ct)
    {
        var lawFirmContacts = contacts
            .Where(IsLawFirmContact)
            .ToList();

        if (lawFirmContacts.Count == 0)
            return;

        var targetIds = lawFirmContacts
            .Select(c => c.Id)
            .ToHashSet(EqualityComparer<Guid>.Default);

        var fallbackLawFirmByOrgId = await _db.Contacts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId &&
                        c.ContactType == ContactType.LawFirm &&
                        c.ContactSubtype == null)
            .OrderBy(c => c.DisplayName)
            .Select(c => new { c.Id, c.OrgId })
            .ToListAsync(ct);

        var defaultContactByOrgId = fallbackLawFirmByOrgId
            .GroupBy(c => c.OrgId)
            .ToDictionary(g => g.Key, g => g.First().Id, EqualityComparer<Guid>.Default);

        foreach (var activeCase in activeCases)
        {
            var fields = ParseLegacyNoteFields(activeCase.Notes);
            var lawFirmId = fields.GetValueOrDefault("lawFirmId", string.Empty);
            if (!string.IsNullOrWhiteSpace(lawFirmId))
            {
                if (Guid.TryParse(lawFirmId, out var parsedLawFirmId) && targetIds.Contains(parsedLawFirmId))
                    contactCaseIds[parsedLawFirmId].Add(activeCase.CaseId);

                continue;
            }

            if (defaultContactByOrgId.TryGetValue(activeCase.OrgId, out var contactId) && targetIds.Contains(contactId))
                contactCaseIds[contactId].Add(activeCase.CaseId);
        }
    }

    private async Task ApplyLienLinkedCountsAsync(
        Guid tenantId,
        IReadOnlyCollection<Contact> contacts,
        IReadOnlyCollection<ActiveCaseLink> activeCases,
        Dictionary<Guid, HashSet<Guid>> contactCaseIds,
        CancellationToken ct)
    {
        var providerIds = contacts
            .Where(c => string.Equals(c.ContactType, ContactType.Provider, StringComparison.Ordinal))
            .Select(c => c.Id)
            .ToHashSet(EqualityComparer<Guid>.Default);
        var fundingCompanyIds = contacts
            .Where(IsFundingCompanyContact)
            .Select(c => c.Id)
            .ToHashSet(EqualityComparer<Guid>.Default);
        var facilityContacts = contacts
            .Where(IsStandaloneFacilityContact)
            .ToList();

        if (providerIds.Count == 0 && fundingCompanyIds.Count == 0 && facilityContacts.Count == 0)
            return;

        var activeCaseIds = activeCases
            .Select(c => c.CaseId)
            .ToHashSet(EqualityComparer<Guid>.Default);
        var liens = await _db.Liens
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.CaseId.HasValue && activeCaseIds.Contains(l.CaseId.Value))
            .Select(l => new ActiveLienLink(l.Id, l.CaseId!.Value, l.FacilityId, l.ExternalReference))
            .ToListAsync(ct);

        if (liens.Count == 0)
            return;

        var lienIds = liens
            .Select(l => l.LienId)
            .ToHashSet(EqualityComparer<Guid>.Default);
        var latestFacilityInfoByLienId = await _db.ServicingItems
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId &&
                        s.LienId.HasValue &&
                        lienIds.Contains(s.LienId.Value) &&
                        s.TaskType == "LegacyMedicalFacilityInfo")
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new FacilityInfoLink(s.LienId!.Value, s.Notes))
            .ToListAsync(ct);

        var facilityInfoByLienId = latestFacilityInfoByLienId
            .GroupBy(x => x.LienId)
            .ToDictionary(g => g.Key, g => g.First().Notes, EqualityComparer<Guid>.Default);

        var facilityById = facilityContacts
            .ToDictionary(c => c.Id, c => c.Id, EqualityComparer<Guid>.Default);
        var facilityByLinkedId = facilityContacts
            .Where(c => c.FacilityId.HasValue)
            .GroupBy(c => c.FacilityId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id, EqualityComparer<Guid>.Default);
        var facilityByName = facilityContacts
            .SelectMany(c => GetFacilityLookupNames(c).Select(name => new { Name = name, ContactId = c.Id }))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ContactId, StringComparer.OrdinalIgnoreCase);

        foreach (var lien in liens)
        {
            facilityInfoByLienId.TryGetValue(lien.LienId, out var facilityInfoNotes);
            var fields = ParseLegacyNoteFields(facilityInfoNotes);

            if (fundingCompanyIds.Count > 0 &&
                Guid.TryParse(lien.FundingCompanyId, out var fundingCompanyId) &&
                fundingCompanyIds.Contains(fundingCompanyId))
            {
                contactCaseIds[fundingCompanyId].Add(lien.CaseId);
            }

            if (providerIds.Count > 0 &&
                Guid.TryParse(fields.GetValueOrDefault("medicalProviderId", string.Empty), out var providerId) &&
                providerIds.Contains(providerId))
            {
                contactCaseIds[providerId].Add(lien.CaseId);
            }

            var normalizedFacilityContactId = ResolveFacilityContactId(
                lien.FacilityId,
                fields.GetValueOrDefault("facilityId", string.Empty),
                fields.GetValueOrDefault("facilityName", string.Empty),
                facilityById,
                facilityByLinkedId,
                facilityByName);

            if (normalizedFacilityContactId.HasValue)
                contactCaseIds[normalizedFacilityContactId.Value].Add(lien.CaseId);
        }
    }

    private static void ApplyMetadataCaseCounts(
        IReadOnlyCollection<Contact> contacts,
        IReadOnlyCollection<ActiveCaseLink> activeCases,
        Dictionary<Guid, HashSet<Guid>> contactCaseIds,
        string fieldName,
        Func<Contact, bool> predicate)
    {
        var targetIds = contacts
            .Where(predicate)
            .Select(c => c.Id)
            .ToHashSet(EqualityComparer<Guid>.Default);

        if (targetIds.Count == 0)
            return;

        foreach (var activeCase in activeCases)
        {
            var fields = ParseLegacyNoteFields(activeCase.Notes);
            if (!Guid.TryParse(fields.GetValueOrDefault(fieldName, string.Empty), out var contactId))
                continue;

            if (targetIds.Contains(contactId))
                contactCaseIds[contactId].Add(activeCase.CaseId);
        }
    }

    private static Guid? ResolveFacilityContactId(
        Guid? lienFacilityId,
        string? facilityIdValue,
        string? facilityName,
        IReadOnlyDictionary<Guid, Guid> facilityById,
        IReadOnlyDictionary<Guid, Guid> facilityByLinkedId,
        IReadOnlyDictionary<string, Guid> facilityByName)
    {
        var resolvedFacilityId = string.IsNullOrWhiteSpace(facilityIdValue)
            ? lienFacilityId?.ToString()
            : facilityIdValue.Trim();

        if (Guid.TryParse(resolvedFacilityId, out var parsedFacilityId))
        {
            if (facilityById.TryGetValue(parsedFacilityId, out var directFacilityContactId))
                return directFacilityContactId;

            if (facilityByLinkedId.TryGetValue(parsedFacilityId, out var linkedFacilityContactId))
                return linkedFacilityContactId;
        }

        if (!string.IsNullOrWhiteSpace(facilityName) &&
            facilityByName.TryGetValue(facilityName.Trim(), out var facilityContactId))
        {
            return facilityContactId;
        }

        return null;
    }

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return fields;

        var rawMetadata = notes;
        var markerIndex = notes.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            rawMetadata = notes[(markerIndex + LegacyMetadataMarker.Length)..].Trim();
        }
        else if (!LooksLikeLegacyMetadata(notes))
        {
            return fields;
        }

        foreach (var segment in rawMetadata.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == segment.Length - 1)
                continue;

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (key.Length > 0)
                fields[key] = value;
        }

        return fields;
    }

    private static bool LooksLikeLegacyMetadata(string notes)
    {
        var segments = notes.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 && segments.All(segment => segment.Contains('='));
    }

    private static IEnumerable<string> GetFacilityLookupNames(Contact contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.Organization))
            yield return contact.Organization.Trim();

        if (!string.IsNullOrWhiteSpace(contact.DisplayName))
            yield return contact.DisplayName.Trim();
    }

    private static bool IsLawFirmContact(Contact contact) =>
        string.Equals(contact.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
        string.IsNullOrWhiteSpace(contact.ContactSubtype);

    private static bool IsLeadContact(Contact contact) =>
        string.Equals(contact.ContactType, ContactType.Lead, StringComparison.Ordinal);

    private static bool IsCaseManagerContact(Contact contact) =>
        string.Equals(contact.ContactType, ContactType.CaseManager, StringComparison.Ordinal) ||
        string.Equals(contact.ContactSubtype, ContactSubtype.LawFirmCaseManager, StringComparison.Ordinal);

    private static bool IsFundingCompanyContact(Contact contact) =>
        string.Equals(contact.ContactType, ContactType.FundingCompany, StringComparison.Ordinal) ||
        string.Equals(contact.ContactType, ContactType.LienHolder, StringComparison.Ordinal);

    private static bool IsStandaloneFacilityContact(Contact contact) =>
        (string.Equals(contact.ContactType, ContactType.Facility, StringComparison.Ordinal) ||
         string.Equals(contact.ContactType, ContactType.MedicalFacility, StringComparison.Ordinal)) &&
        string.IsNullOrWhiteSpace(contact.ContactSubtype);

    private sealed record ActiveCaseLink(Guid CaseId, Guid OrgId, string? Notes);
    private sealed record ActiveLienLink(Guid LienId, Guid CaseId, Guid? FacilityId, string? FundingCompanyId);
    private sealed record FacilityInfoLink(Guid LienId, string? Notes);
}
