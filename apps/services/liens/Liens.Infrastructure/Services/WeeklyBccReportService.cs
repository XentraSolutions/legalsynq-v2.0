using System.Globalization;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class WeeklyBccReportService : IWeeklyBccReportService
{
    private const string LegacyMetadataMarker = "[legacy-meta]";
    private readonly LiensDbContext _db;

    public WeeklyBccReportService(LiensDbContext db) => _db = db;

    public Task<WeeklyBccReportResult> GetAsync(
        Guid tenantId,
        DateOnly asOfDate,
        CancellationToken ct = default) =>
        GetAsync(tenantId, asOfDate, page: null, pageSize: null, includeTotalCount: true, ct);

    public Task<WeeklyBccReportResult> GetPageAsync(
        Guid tenantId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        bool includeTotalCount = true,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        return GetAsync(tenantId, asOfDate, page, pageSize, includeTotalCount, ct);
    }

    private async Task<WeeklyBccReportResult> GetAsync(
        Guid tenantId,
        DateOnly asOfDate,
        int? page,
        int? pageSize,
        bool includeTotalCount,
        CancellationToken ct)
    {
        var query = _db.Liens
            .AsNoTracking()
            .Where(lien => lien.TenantId == tenantId &&
                           lien.PurchaseDate.HasValue &&
                           lien.PurchaseDate.Value <= asOfDate);
        var orderedQuery = query
            .OrderBy(lien => lien.PurchaseDate)
            .ThenBy(lien => lien.LienNumber)
            .ThenBy(lien => lien.Id);

        int totalCount;
        List<Lien> liens;
        if (page.HasValue && pageSize.HasValue)
        {
            totalCount = includeTotalCount ? await query.CountAsync(ct) : 0;
            var offset = (long)(page.Value - 1) * pageSize.Value;
            liens = includeTotalCount && offset >= totalCount
                ? []
                : await orderedQuery
                    .Skip((int)offset)
                    .Take(pageSize.Value)
                    .ToListAsync(ct);
        }
        else
        {
            liens = await orderedQuery.ToListAsync(ct);
            totalCount = liens.Count;
        }

        if (liens.Count == 0)
        {
            return new WeeklyBccReportResult
            {
                AsOfDate = asOfDate,
                Page = page ?? 1,
                PageSize = pageSize ?? 0,
                TotalCount = totalCount,
            };
        }

        var lienIds = liens.Select(lien => lien.Id).ToHashSet();
        var caseIds = liens
            .Where(lien => lien.CaseId.HasValue)
            .Select(lien => lien.CaseId!.Value)
            .ToHashSet();

        var casesById = (await _db.Cases
                .AsNoTracking()
                .Where(caseEntity => caseEntity.TenantId == tenantId && caseIds.Contains(caseEntity.Id))
                .ToListAsync(ct))
            .ToDictionary(caseEntity => caseEntity.Id);
        var servicingItems = await _db.ServicingItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           ((item.LienId.HasValue && lienIds.Contains(item.LienId.Value)) ||
                            (item.CaseId.HasValue && caseIds.Contains(item.CaseId.Value))))
            .ToListAsync(ct);
        var reductions = await _db.LienReductions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                           lienIds.Contains(item.LienId))
            .ToListAsync(ct);
        var settlements = await _db.LienSettlements
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                           lienIds.Contains(item.LienId))
            .ToListAsync(ct);
        var payments = await _db.SettlementPaymentDetails
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                           lienIds.Contains(item.LienId))
            .ToListAsync(ct);
        var caseNotes = await _db.LienCaseNotes
            .AsNoTracking()
            .Where(note => note.TenantId == tenantId && caseIds.Contains(note.CaseId) &&
                           !note.IsDeleted)
            .ToListAsync(ct);

        var caseMetadataById = casesById.ToDictionary(
            pair => pair.Key,
            pair => ParseMetadata(pair.Value.Notes));
        var facilityInfoByLienId = servicingItems
            .Where(item => item.LienId.HasValue &&
                           string.Equals(item.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal))
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAtUtc).First());

        var referencedContactIds = new HashSet<Guid>();
        var referencedCompanyIds = new HashSet<Guid>();
        var referencedCompanyContactIds = new HashSet<Guid>();
        var referencedFacilityIds = new HashSet<Guid>();

        foreach (var lien in liens)
        {
            Add(referencedContactIds, lien.FundingCompanyId);
            Add(referencedCompanyIds, lien.FundingCompanyCompanyId);
            Add(referencedCompanyIds, lien.MedicalProviderCompanyId);
            Add(referencedCompanyIds, lien.MedicalFacilityCompanyId);
            Add(referencedCompanyContactIds, lien.FundingCompanyContactPersonId);
            Add(referencedFacilityIds, lien.FacilityId);

            if (facilityInfoByLienId.TryGetValue(lien.Id, out var facilityInfo))
            {
                var fields = ParseMetadata(facilityInfo.Notes);
                AddParsed(referencedFacilityIds, fields.GetValueOrDefault("facilityId"));
                AddParsed(referencedContactIds, fields.GetValueOrDefault("medicalProviderId"));
                AddParsed(referencedContactIds, fields.GetValueOrDefault("facilityContactPersonId"));
                AddParsed(referencedContactIds, fields.GetValueOrDefault("medicalFacilityContactId"));
            }
        }

        foreach (var caseEntity in casesById.Values)
        {
            Add(referencedCompanyIds, caseEntity.HandlingLawFirmCompanyId);
            Add(referencedCompanyContactIds, caseEntity.CaseManagerContactPersonId);
            var fields = caseMetadataById[caseEntity.Id];
            AddParsed(referencedContactIds, fields.GetValueOrDefault("lawFirmId"));
            AddParsed(referencedContactIds, fields.GetValueOrDefault("caseManagerId"));
            AddParsed(referencedContactIds, fields.GetValueOrDefault("leadId"));
        }

        var facilitiesById = (await _db.Facilities.AsNoTracking()
                .Where(item => item.TenantId == tenantId && referencedFacilityIds.Contains(item.Id))
                .ToListAsync(ct))
            .ToDictionary(item => item.Id);
        var contacts = await _db.Contacts.AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           (referencedContactIds.Contains(item.Id) ||
                            (item.FacilityId.HasValue && referencedFacilityIds.Contains(item.FacilityId.Value))))
            .ToListAsync(ct);
        var contactsById = contacts.ToDictionary(item => item.Id);
        var facilityContactsByFacilityId = contacts
            .Where(item => item.FacilityId.HasValue)
            .GroupBy(item => item.FacilityId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.IsActive).ThenBy(item => item.DisplayName).First());
        var facilityPeopleByFacilityId = (await _db.FacilityContactPersons.AsNoTracking()
                .Where(item => item.TenantId == tenantId && referencedFacilityIds.Contains(item.FacilityId))
                .ToListAsync(ct))
            .GroupBy(item => item.FacilityId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.IsActive)
                    .ThenBy(item => item.FirstName)
                    .ThenBy(item => item.LastName)
                    .First());
        var companiesById = (await _db.Companies.AsNoTracking()
                .Where(item => item.TenantId == tenantId && referencedCompanyIds.Contains(item.Id))
                .ToListAsync(ct))
            .ToDictionary(item => item.Id);
        var companyContactsById = (await _db.CompanyContactPersons.AsNoTracking()
                .Where(item => item.TenantId == tenantId && referencedCompanyContactIds.Contains(item.Id))
                .ToListAsync(ct))
            .ToDictionary(item => item.Id);

        var servicingByLienId = servicingItems
            .Where(item => item.LienId.HasValue)
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var servicingByCaseId = servicingItems
            .Where(item => item.CaseId.HasValue)
            .GroupBy(item => item.CaseId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var reductionsByLienId = reductions.GroupBy(item => item.LienId).ToDictionary(group => group.Key, group => group.ToList());
        var settlementsByLienId = settlements.GroupBy(item => item.LienId).ToDictionary(group => group.Key, group => group.ToList());
        var paymentsByLienId = payments.GroupBy(item => item.LienId).ToDictionary(group => group.Key, group => group.ToList());
        var notesByCaseId = caseNotes.GroupBy(item => item.CaseId).ToDictionary(group => group.Key, group => group.ToList());

        var rows = liens.Select(lien => BuildRow(
                lien,
                asOfDate,
                casesById,
                caseMetadataById,
                servicingByLienId.GetValueOrDefault(lien.Id, []),
                lien.CaseId.HasValue ? servicingByCaseId.GetValueOrDefault(lien.CaseId.Value, []) : [],
                reductionsByLienId.GetValueOrDefault(lien.Id, []),
                settlementsByLienId.GetValueOrDefault(lien.Id, []),
                paymentsByLienId.GetValueOrDefault(lien.Id, []),
                lien.CaseId.HasValue ? notesByCaseId.GetValueOrDefault(lien.CaseId.Value, []) : [],
                facilitiesById,
                contactsById,
                facilityContactsByFacilityId,
                facilityPeopleByFacilityId,
                companiesById,
                companyContactsById))
            .ToList();

        return new WeeklyBccReportResult
        {
            AsOfDate = asOfDate,
            Items = rows,
            Page = page ?? 1,
            PageSize = pageSize ?? rows.Count,
            TotalCount = totalCount,
        };
    }

    private static WeeklyBccReportRow BuildRow(
        Lien lien,
        DateOnly asOfDate,
        IReadOnlyDictionary<Guid, Case> casesById,
        IReadOnlyDictionary<Guid, Dictionary<string, string>> caseMetadataById,
        IReadOnlyCollection<ServicingItem> lienServicing,
        IReadOnlyCollection<ServicingItem> caseServicing,
        IReadOnlyCollection<LienReduction> reductions,
        IReadOnlyCollection<LienSettlement> settlements,
        IReadOnlyCollection<SettlementPaymentDetail> payments,
        IReadOnlyCollection<LienCaseNote> caseNotes,
        IReadOnlyDictionary<Guid, Facility> facilitiesById,
        IReadOnlyDictionary<Guid, Contact> contactsById,
        IReadOnlyDictionary<Guid, Contact> facilityContactsByFacilityId,
        IReadOnlyDictionary<Guid, FacilityContactPerson> facilityPeopleByFacilityId,
        IReadOnlyDictionary<Guid, Company> companiesById,
        IReadOnlyDictionary<Guid, CompanyContactPerson> companyContactsById)
    {
        casesById.TryGetValue(lien.CaseId ?? Guid.Empty, out var caseEntity);
        var caseFields = caseEntity is not null
            ? caseMetadataById.GetValueOrDefault(caseEntity.Id, EmptyMetadata)
            : EmptyMetadata;
        var medicalCodeItems = lienServicing
            .Where(item => string.Equals(item.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
            .ToList();
        var codeFields = medicalCodeItems.Select(item => ParseMetadata(item.Notes)).ToList();
        var hasMedicalPurchase = codeFields.Any(fields => fields.ContainsKey("purchaseAmount"));
        var hasMedicalBilling = codeFields.Any(fields => fields.ContainsKey("billingAmount"));
        var purchaseAmount = hasMedicalPurchase
            ? codeFields.Sum(fields => ParseDecimal(fields.GetValueOrDefault("purchaseAmount")))
            : lien.PurchasePrice ?? 0m;
        var billingAmount = hasMedicalBilling
            ? codeFields.Sum(fields => ParseDecimal(fields.GetValueOrDefault("billingAmount")))
            : lien.OriginalAmount;

        var settlementFields = settlements.Select(item => ParseMetadata(item.Note)).ToList();
        var hasLegacyReturned = settlementFields.Any(fields => fields.ContainsKey("totalSettledAmount"));
        var returnedAmount = hasLegacyReturned
            ? settlementFields.Sum(fields => ParseDecimal(fields.GetValueOrDefault("totalSettledAmount")))
            : lien.PayoffAmount ?? payments.Sum(item => item.Amount);
        var hasLegacyReduction = settlementFields.Any(fields => fields.ContainsKey("reductionAmount"));
        var reductionAmount = hasLegacyReduction
            ? settlementFields.Sum(fields => ParseDecimal(fields.GetValueOrDefault("reductionAmount")))
            : reductions.Sum(item => item.Amount);
        var expectedSettlement = reductionAmount > 0m
            ? billingAmount - purchaseAmount
            : (decimal?)null;
        var amountToSettlement = settlements.Sum(item => item.Amount);
        var daysSincePurchase = lien.PurchaseDate.HasValue
            ? Math.Max(asOfDate.DayNumber - lien.PurchaseDate.Value.DayNumber, 0)
            : (int?)null;
        var grossProfit = returnedAmount - purchaseAmount;
        var roi = purchaseAmount > 0m ? grossProfit / purchaseAmount * 100m : 0m;
        var annualizedRoi = purchaseAmount > 0m && daysSincePurchase is > 0
            ? roi * 365m / daysSincePurchase.Value
            : 0m;

        var facilityInfo = lienServicing
            .Where(item => string.Equals(item.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        var facilityFields = ParseMetadata(facilityInfo?.Notes);
        var facilityId = FirstGuid(facilityFields.GetValueOrDefault("facilityId"), lien.FacilityId?.ToString());
        facilitiesById.TryGetValue(facilityId ?? Guid.Empty, out var facility);
        companiesById.TryGetValue(lien.MedicalFacilityCompanyId ?? Guid.Empty, out var facilityCompany);
        var facilityContactId = FirstGuid(
            facilityFields.GetValueOrDefault("facilityContactPersonId"),
            facilityFields.GetValueOrDefault("medicalFacilityContactId"));
        contactsById.TryGetValue(facilityContactId ?? Guid.Empty, out var facilityContact);
        if (facilityContact is null && facilityId.HasValue)
            facilityContactsByFacilityId.TryGetValue(facilityId.Value, out facilityContact);
        facilityPeopleByFacilityId.TryGetValue(facilityId ?? Guid.Empty, out var facilityPerson);

        var providerId = FirstGuid(facilityFields.GetValueOrDefault("medicalProviderId"));
        contactsById.TryGetValue(providerId ?? Guid.Empty, out var providerContact);
        companiesById.TryGetValue(lien.MedicalProviderCompanyId ?? Guid.Empty, out var providerCompany);

        var lawFirmId = FirstGuid(caseFields.GetValueOrDefault("lawFirmId"));
        contactsById.TryGetValue(lawFirmId ?? Guid.Empty, out var legacyLawFirm);
        companiesById.TryGetValue(caseEntity?.HandlingLawFirmCompanyId ?? Guid.Empty, out var lawFirmCompany);
        var lawFirm = ResolveParty(lawFirmCompany, legacyLawFirm, caseFields.GetValueOrDefault("lawFirm"));

        var caseManagerId = FirstGuid(caseFields.GetValueOrDefault("caseManagerId"));
        contactsById.TryGetValue(caseManagerId ?? Guid.Empty, out var legacyCaseManager);
        companyContactsById.TryGetValue(caseEntity?.CaseManagerContactPersonId ?? Guid.Empty, out var canonicalCaseManager);
        var caseManagerName = canonicalCaseManager is not null
            ? DisplayPerson(canonicalCaseManager.FirstName, canonicalCaseManager.LastName)
            : FirstNonEmpty(legacyCaseManager?.DisplayName, caseFields.GetValueOrDefault("caseManager"));
        var caseManagerEmail = FirstNonEmpty(canonicalCaseManager?.Email, legacyCaseManager?.Email);

        var leadId = FirstGuid(caseFields.GetValueOrDefault("leadId"));
        contactsById.TryGetValue(leadId ?? Guid.Empty, out var leadContact);
        var address = SplitAddress(caseEntity?.ClientAddress);
        var latestFeedNote = caseNotes
            .Where(note => string.Equals(note.Category, CaseNoteCategory.Feed, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(note => note.CreatedAtUtc)
            .FirstOrDefault();
        var latestTrackingNote = caseNotes
            .Where(note => string.Equals(note.Category, CaseNoteCategory.General, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(note.Category, CaseNoteCategory.FollowUp, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(note => note.CreatedAtUtc)
            .FirstOrDefault();
        var lastActivity = lienServicing.Concat(caseServicing)
            .Where(item => !string.Equals(item.TaskType, "LegacyMedicalCode", StringComparison.Ordinal) &&
                           !string.Equals(item.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        var medicalCodes = codeFields
            .Select(fields => FirstNonEmpty(fields.GetValueOrDefault("code"), fields.GetValueOrDefault("description")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var medicalProviders = new[]
            {
                providerCompany?.Name,
                providerContact is null ? null : DisplayContact(providerContact),
                facilityFields.GetValueOrDefault("medicalProvider"),
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var dateClosed = ResolveDateClosed(lien, settlements, payments, asOfDate);

        return new WeeklyBccReportRow
        {
            PlaintiffFirstName = NullIfEmpty(caseEntity?.ClientFirstName ?? lien.SubjectFirstName),
            PlaintiffLastName = NullIfEmpty(caseEntity?.ClientLastName ?? lien.SubjectLastName),
            PlaintiffDob = FormatDate(caseEntity?.ClientDob),
            PlaintiffPhone = NullIfEmpty(caseEntity?.ClientPhone),
            PlaintiffAddress = NullIfEmpty(address.Address),
            PlaintiffCity = NullIfEmpty(address.City),
            PlaintiffState = NullIfEmpty(address.State),
            PlaintiffZip = NullIfEmpty(address.Zip),
            LienId = lien.LienNumber,
            CaseId = NullIfEmpty(caseEntity?.CaseNumber),
            PurchaseDate = FormatDate(lien.PurchaseDate),
            DaysSincePurchase = daysSincePurchase,
            PurchaseAmt = FormatMoney(purchaseAmount),
            BillingAmt = FormatMoney(billingAmount),
            ExpectedSettlementAmt = FormatMoney(expectedSettlement),
            ReductionPercentage = FormatMoney(billingAmount > 0m ? reductionAmount / billingAmount * 100m : 0m),
            CapitalProviders = NullIfEmpty(ResolveParty(
                companiesById.GetValueOrDefault(lien.FundingCompanyCompanyId ?? Guid.Empty),
                contactsById.GetValueOrDefault(lien.FundingCompanyId ?? Guid.Empty),
                null).Name),
            DateClosed = FormatDate(dateClosed),
            ReturnedAmt = string.Equals(lien.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase)
                ? FormatMoney(returnedAmount)
                : null,
            GrossProfit = FormatMoney(grossProfit),
            Roi = FormatMoney(roi),
            AnnualizedRoi = FormatMoney(annualizedRoi),
            MedicalCodeCount = medicalCodeItems.Count,
            MedicalCodes = NullIfEmpty(string.Join(", ", medicalCodes)),
            InitialServiceDate = FormatDate(lien.InitialServiceDate),
            EndServiceDate = FormatDate(lien.EndServiceDate),
            MedicalProviders = NullIfEmpty(string.Join(", ", medicalProviders)),
            MedicalFacilityContact = facilityPerson is not null
                ? NullIfEmpty(DisplayPerson(facilityPerson.FirstName, facilityPerson.LastName))
                : facilityContact is null ? null : NullIfEmpty(DisplayContact(facilityContact)),
            MedicalFacility = NullIfEmpty(FirstNonEmpty(facilityCompany?.Name, facility?.Name, facilityFields.GetValueOrDefault("facilityName"))),
            MedicalFacilityAddress = NullIfEmpty(FirstNonEmpty(facilityCompany?.AddressLine1, facility?.AddressLine1)),
            MedicalFacilityCity = NullIfEmpty(FirstNonEmpty(facilityCompany?.City, facility?.City)),
            MedicalFacilityState = NullIfEmpty(FirstNonEmpty(facilityCompany?.State, facility?.State)),
            MedicalFacilityZip = NullIfEmpty(FirstNonEmpty(facilityCompany?.PostalCode, facility?.PostalCode)),
            Noted = latestFeedNote?.Content,
            Lawfirm = NullIfEmpty(lawFirm.Name),
            LawfirmAddress = NullIfEmpty(lawFirm.Address),
            LawfirmCity = NullIfEmpty(lawFirm.City),
            LawfirmState = NullIfEmpty(lawFirm.State),
            LawfirmZip = NullIfEmpty(lawFirm.Zip),
            LawfirmPhone = NullIfEmpty(lawFirm.Phone),
            CaseType = NullIfEmpty(FirstNonEmpty(caseFields.GetValueOrDefault("accidentType"), caseFields.GetValueOrDefault("caseType"))),
            StateOfIncident = NullIfEmpty(FirstNonEmpty(caseFields.GetValueOrDefault("accidentState"), lien.Jurisdiction)),
            CaseTrackingContact = NullIfEmpty(caseManagerName),
            CaseTrackingContactEmail = NullIfEmpty(caseManagerEmail),
            CaseManager = NullIfEmpty(caseManagerName),
            AmtToSettlement = FormatMoney(amountToSettlement),
            CaseStatus = NullIfEmpty(FirstNonEmpty(caseFields.GetValueOrDefault("statusLabel"), caseEntity?.Status)),
            MedicalStatus = NullIfEmpty(caseFields.GetValueOrDefault("currentMedicalStatus")),
            CaseTrackingFollowUpDate = FormatDate(ParseDate(caseFields.GetValueOrDefault("trackingFollowUpDate"))),
            LastActivityDate = FormatDate(lastActivity?.UpdatedAtUtc),
            LastActivity = NullIfEmpty(FirstNonEmpty(lastActivity?.Resolution, lastActivity?.Notes, lastActivity?.Description)),
            CaseEnteredBy = NullIfEmpty(FirstNonEmpty(
                caseFields.GetValueOrDefault("caseEnteredBy"),
                caseFields.GetValueOrDefault("createdBy"),
                caseNotes.OrderBy(note => note.CreatedAtUtc).FirstOrDefault()?.CreatedByName,
                caseEntity?.CreatedByUserId?.ToString())),
            LeadSource = NullIfEmpty(FirstNonEmpty(
                leadContact is null ? null : DisplayContact(leadContact),
                caseFields.GetValueOrDefault("leadDescription"),
                caseFields.GetValueOrDefault("leadSource"))),
            DateOfLoss = FormatDate(caseEntity?.DateOfIncident ?? lien.IncidentDate),
            LastCaseNote = latestTrackingNote?.Content ?? NullIfEmpty(ExtractNoteText(caseEntity?.Notes)),
            LastCaseNoteDate = FormatDate(latestTrackingNote?.CreatedAtUtc),
            Reduction = FormatMoney(reductionAmount),
        };
    }

    private static DateOnly? ResolveDateClosed(
        Lien lien,
        IReadOnlyCollection<LienSettlement> settlements,
        IReadOnlyCollection<SettlementPaymentDetail> payments,
        DateOnly asOfDate)
    {
        if (!string.Equals(lien.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase))
            return null;

        if (lien.ClosedAtUtc.HasValue)
        {
            var closedDate = DateOnly.FromDateTime(lien.ClosedAtUtc.Value);
            if (closedDate <= asOfDate)
                return closedDate;
        }

        return settlements.Where(item => item.SettlementDate.HasValue).Select(item => item.SettlementDate!.Value)
            .Concat(payments.Where(item => item.PaymentDate.HasValue).Select(item => item.PaymentDate!.Value))
            .DefaultIfEmpty()
            .Max() is var result && result != default ? result : null;
    }

    private static PartyDetails ResolveParty(Company? company, Contact? contact, string? fallback) => new(
        FirstNonEmpty(company?.Name, contact is null ? null : DisplayContact(contact), fallback),
        FirstNonEmpty(company?.AddressLine1, contact?.AddressLine1),
        FirstNonEmpty(company?.City, contact?.City),
        FirstNonEmpty(company?.State, contact?.State),
        FirstNonEmpty(company?.PostalCode, contact?.PostalCode),
        FirstNonEmpty(company?.Phone, contact?.Phone));

    private static Dictionary<string, string> ParseMetadata(string? value)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return fields;

        var markerIndex = value.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        var raw = markerIndex >= 0 ? value[(markerIndex + LegacyMetadataMarker.Length)..] : value;
        foreach (var segment in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
                continue;
            fields[segment[..separator].Trim()] = segment[(separator + 1)..].Trim();
        }
        return fields;
    }

    private static string ExtractNoteText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var markerIndex = value.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            return value[..markerIndex].Trim();
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(segment => segment.Contains('=')) ? string.Empty : value.Trim();
    }

    private static AddressParts SplitAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new AddressParts();
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            >= 4 => new AddressParts(string.Join(", ", parts.Take(parts.Length - 3)), parts[^3], parts[^2], parts[^1]),
            3 => new AddressParts(parts[0], parts[1], parts[2], string.Empty),
            2 => new AddressParts(parts[0], parts[1], string.Empty, string.Empty),
            _ => new AddressParts(value.Trim(), string.Empty, string.Empty, string.Empty),
        };
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;
    private static string? FormatDate(DateOnly? value) =>
        value?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    private static string? FormatDate(DateTime? value) =>
        value?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    private static string? FormatMoney(decimal? value) =>
        value?.ToString("N2", CultureInfo.InvariantCulture);
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid? FirstGuid(params string?[] values) => values
        .Select(value => Guid.TryParse(value, out var id) ? (Guid?)id : null)
        .FirstOrDefault(value => value.HasValue);
    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    private static string DisplayContact(Contact contact) =>
        FirstNonEmpty(contact.Organization, contact.DisplayName);
    private static string DisplayPerson(string firstName, string lastName) =>
        string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
    private static void Add(ISet<Guid> ids, Guid? id) { if (id.HasValue) ids.Add(id.Value); }
    private static void AddParsed(ISet<Guid> ids, string? value) { if (Guid.TryParse(value, out var id)) ids.Add(id); }

    private static readonly Dictionary<string, string> EmptyMetadata = new(StringComparer.OrdinalIgnoreCase);
    private sealed record AddressParts(string Address = "", string City = "", string State = "", string Zip = "");
    private sealed record PartyDetails(string Name, string Address, string City, string State, string Zip, string Phone);
}
