using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Application.Search;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienService : ILienService
{
    private readonly ILienRepository           _lienRepo;
    private readonly ILienStatusHistoryRepository _lienStatusHistoryRepo;
    private readonly ICaseRepository           _caseRepo;
    private readonly IContactRepository        _contactRepo;
    private readonly IFacilityRepository       _facilityRepo;
    private readonly IAuditPublisher           _audit;
    private readonly ILienTaskGenerationDispatcher _taskGenDispatcher;
    private readonly ILogger<LienService>          _logger;

    public LienService(
        ILienRepository lienRepo,
        ILienStatusHistoryRepository lienStatusHistoryRepo,
        ICaseRepository caseRepo,
        IContactRepository contactRepo,
        IFacilityRepository facilityRepo,
        IAuditPublisher audit,
        ILienTaskGenerationDispatcher taskGenDispatcher,
        ILogger<LienService> logger)
    {
        _lienRepo          = lienRepo;
        _lienStatusHistoryRepo = lienStatusHistoryRepo;
        _caseRepo          = caseRepo;
        _contactRepo       = contactRepo;
        _facilityRepo      = facilityRepo;
        _audit             = audit;
        _taskGenDispatcher = taskGenDispatcher;
        _logger            = logger;
    }

    public async Task<PaginatedResult<LienResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, string? lienType,
        Guid? caseId, Guid? facilityId, int page, int pageSize,
        CancellationToken ct = default,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        Guid? visibleOrgId = null,
        bool includeSellerOrg = false,
        bool includeBuyerOrg = false,
        bool includeHolderOrg = false,
        bool includeMarketplace = false,
        bool excludeRejectedAndCancelled = false)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var hasKeyword = !string.IsNullOrWhiteSpace(search);
        var keyword = search?.Trim();
        var (items, totalCount) = await _lienRepo.SearchAsync(
            tenantId,
            hasKeyword ? null : search,
            status,
            lienType,
            caseId,
            facilityId,
            hasKeyword ? 1 : page,
            hasKeyword ? FuzzySearchScorer.CandidateLimit : pageSize,
            ct,
            createdFromUtc,
            createdToUtc,
            visibleOrgId,
            includeSellerOrg,
            includeBuyerOrg,
            includeHolderOrg,
            includeMarketplace,
            excludeRejectedAndCancelled);

        var casesById = await LoadCasesByIdAsync(tenantId, items, ct);
        var facilitiesById = await LoadFacilitiesByIdAsync(tenantId, items, ct);
        var caseManagerById = await LoadCaseManagersByIdAsync(tenantId, casesById.Values, ct);
        var lawFirms = await _contactRepo.GetAllByTypeAsync(tenantId, ContactType.LawFirm, isActive: null, ct);
        var lawFirmById = lawFirms.ToDictionary(contact => contact.Id);
        var lawFirmByOrgId = lawFirms
            .GroupBy(contact => contact.OrgId)
            .ToDictionary(group => group.Key, group => group.First());

        var candidates = items.Select(item =>
        {
            var caseEntity = casesById.GetValueOrDefault(item.CaseId ?? Guid.Empty);
            return new LienSearchCandidate(
                item,
                caseEntity,
                facilitiesById.GetValueOrDefault(item.FacilityId ?? Guid.Empty),
                ResolveLawFirmName(item, caseEntity, lawFirmById, lawFirmByOrgId),
                ResolveCaseManagerName(caseEntity, caseManagerById));
        }).ToList();

        if (hasKeyword)
        {
            var matches = candidates
                .Select(candidate => new { Candidate = candidate, Score = GetLienKeywordScore(candidate, keyword!) })
                .Where(match => FuzzySearchScorer.IsAccepted(match.Score))
                .OrderByDescending(match => match.Score.Value)
                .ThenByDescending(match => match.Candidate.Lien.Id)
                .ToList();

            totalCount = matches.Count;
            candidates = matches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(match => match.Candidate)
                .ToList();
        }

        return new PaginatedResult<LienResponse>
        {
            Items = candidates.Select(candidate => MapToResponse(
                candidate.Lien,
                candidate.Case,
                candidate.Facility,
                candidate.LawFirm,
                candidate.CaseManager)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<LienResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct);
        if (entity is null)
            return null;

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    public async Task<LienResponse?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByLienNumberAsync(tenantId, lienNumber, ct);
        if (entity is null)
            return null;

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    public async Task<LienResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateLienRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.LienNumber) && !request.CaseId.HasValue)
            errors.Add("lienNumber", ["Lien number is required when no case is provided."]);
        if (string.IsNullOrWhiteSpace(request.LienType))
            errors.Add("lienType", ["Lien type is required."]);
        else if (!LienType.All.Contains(request.LienType))
            errors.Add("lienType", [$"Invalid lien type: '{request.LienType}'. Valid values: {string.Join(", ", LienType.All)}"]);
        if (request.OriginalAmount < 0)
            errors.Add("originalAmount", ["Original amount cannot be negative."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        if (!string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            var idempotent = await _lienRepo.GetByExternalReferenceAsync(
                tenantId, request.ExternalReference.Trim(), ct);
            if (idempotent is not null)
                return await MapToResponseAsync(tenantId, idempotent, ct);
        }

        Case? caseEntity = null;
        if (request.CaseId.HasValue)
        {
            caseEntity = await _caseRepo.GetByIdAsync(tenantId, request.CaseId.Value, ct);
            if (caseEntity is null)
                throw new ValidationException("Referenced case does not exist.",
                    new Dictionary<string, string[]> { ["caseId"] = [$"Case '{request.CaseId.Value}' not found."] });
        }

        var lienNumber = string.IsNullOrWhiteSpace(request.LienNumber)
            ? await GenerateLienNumberAsync(tenantId, caseEntity!, ct)
            : request.LienNumber.Trim();

        var existing = await _lienRepo.GetByLienNumberAsync(tenantId, lienNumber, ct);
        if (existing is not null)
            throw new ConflictException(
                $"A lien with number '{lienNumber}' already exists.",
                "LIEN_NUMBER_DUPLICATE");

        var resolvedFacilityId = request.FacilityId.HasValue
            ? await ResolveFacilityIdAsync(tenantId, request.FacilityId.Value, actingUserId, ct)
            : null;

        if (request.FacilityId.HasValue && !resolvedFacilityId.HasValue)
        {
            throw new ValidationException("Referenced facility does not exist.",
                new Dictionary<string, string[]> { ["facilityId"] = [$"Facility '{request.FacilityId.Value}' not found."] });
        }

        var entity = Lien.Create(
            tenantId: tenantId,
            orgId: orgId,
            lienNumber: lienNumber,
            lienType: request.LienType,
            originalAmount: request.OriginalAmount,
            createdByUserId: actingUserId,
            externalReference: request.ExternalReference,
            caseId: request.CaseId,
            facilityId: resolvedFacilityId,
            subjectFirstName: request.SubjectFirstName,
            subjectLastName: request.SubjectLastName,
            isConfidential: request.IsConfidential,
            jurisdiction: request.Jurisdiction,
            incidentDate: request.IncidentDate,
            initialServiceDate: request.InitialServiceDate,
            endServiceDate: request.EndServiceDate,
            isBulk: request.IsBulk,
            isServicing: request.IsServicing,
            description: request.Description,
            purchaseDate: request.PurchaseDate);

        await _lienRepo.AddAsync(entity, ct);

        _logger.LogInformation(
            "Lien created: {LienId} LienNumber={LienNumber} Tenant={TenantId}",
            entity.Id, entity.LienNumber, tenantId);

        _audit.Publish(
            eventType: "liens.lien.created",
            action: "create",
            description: $"Lien '{entity.LienNumber}' created (type={entity.LienType}, amount={entity.OriginalAmount})",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: entity.Id.ToString());

        // Run task generation in an isolated scope so it never reuses the request DbContext.
        var lienId = entity.Id;
        var genContext = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      Domain.Enums.TaskGenerationEventType.LienCreated,
            EntityType:     "LIEN",
            EntityId:       lienId,
            CaseId:         entity.CaseId,
            LienId:         lienId,
            WorkflowStageId: null,
            ActorUserId:    actingUserId);

        _taskGenDispatcher.Dispatch(genContext);

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    private async Task<string> GenerateLienNumberAsync(Guid tenantId, Case caseEntity, CancellationToken ct)
    {
        var prefix = caseEntity.CaseNumber.Trim();
        var existingLiens = await _lienRepo.GetByCaseIdAsync(tenantId, caseEntity.Id, ct);
        var maxSequence = existingLiens
            .Select(l => TryGetLienSequence(l.LienNumber, prefix))
            .Where(sequence => sequence.HasValue)
            .Select(sequence => sequence!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{maxSequence + 1:00}";
    }

    private static int? TryGetLienSequence(string lienNumber, string caseNumber)
    {
        var prefix = $"{caseNumber}-";
        if (!lienNumber.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var suffix = lienNumber[prefix.Length..];
        return int.TryParse(suffix, out var sequence) ? sequence : null;
    }

    public async Task<LienResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateLienRequest request, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Lien '{id}' not found for tenant '{tenantId}'.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.LienType))
            errors.Add("lienType", ["Lien type is required."]);
        else if (!LienType.All.Contains(request.LienType))
            errors.Add("lienType", [$"Invalid lien type: '{request.LienType}'. Valid values: {string.Join(", ", LienType.All)}"]);
        if (request.OriginalAmount < 0)
            errors.Add("originalAmount", ["Original amount cannot be negative."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        if (request.CaseId.HasValue && request.CaseId != entity.CaseId)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, request.CaseId.Value, ct);
            if (caseEntity is null)
                throw new ValidationException("Referenced case does not exist.",
                    new Dictionary<string, string[]> { ["caseId"] = [$"Case '{request.CaseId.Value}' not found."] });
        }

        var resolvedFacilityId = request.FacilityId.HasValue
            ? await ResolveFacilityIdAsync(tenantId, request.FacilityId.Value, actingUserId, ct)
            : null;

        if (request.FacilityId.HasValue && !resolvedFacilityId.HasValue)
        {
            throw new ValidationException("Referenced facility does not exist.",
                new Dictionary<string, string[]> { ["facilityId"] = [$"Facility '{request.FacilityId.Value}' not found."] });
        }

        entity.Update(
            lienType: request.LienType,
            originalAmount: request.OriginalAmount,
            updatedByUserId: actingUserId,
            externalReference: request.ExternalReference,
            subjectFirstName: request.SubjectFirstName,
            subjectLastName: request.SubjectLastName,
            isConfidential: request.IsConfidential,
            jurisdiction: request.Jurisdiction,
            incidentDate: request.IncidentDate,
            initialServiceDate: request.InitialServiceDate ?? entity.InitialServiceDate,
            endServiceDate: request.EndServiceDate ?? entity.EndServiceDate,
            isBulk: request.IsBulk ?? entity.IsBulk,
            isServicing: request.IsServicing ?? entity.IsServicing,
            description: request.Description,
            purchaseDate: request.PurchaseDate ?? entity.PurchaseDate);

        if (request.CaseId.HasValue)
            entity.AttachCase(request.CaseId.Value, actingUserId);

        if (resolvedFacilityId.HasValue && resolvedFacilityId != entity.FacilityId)
            entity.AttachFacility(resolvedFacilityId.Value, actingUserId);

        await _lienRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Lien updated: {LienId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.lien.updated",
            action: "update",
            description: $"Lien '{entity.LienNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: entity.Id.ToString());

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    public async Task<LienResponse> SetLegacyMedicalStatusAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        string status, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Lien '{id}' not found for tenant '{tenantId}'.");

        entity.SetLegacyMedicalStatus(status.Trim(), actingUserId);

        await _lienRepo.UpdateAsync(entity, ct);
        await RecordStatusHistoryAsync(
            entity,
            actingUserId,
            $"Lien status updated to {MapBusinessStatusLabel(entity.Status)}.",
            ct);

        _logger.LogInformation(
            "Legacy medical lien status updated: {LienId} Status={Status} Tenant={TenantId}",
            entity.Id, entity.Status, tenantId);

        _audit.Publish(
            eventType: "liens.lien.legacy_medical_status_updated",
            action: "update",
            description: $"Legacy medical status for lien '{entity.LienNumber}' updated to '{entity.Status}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: entity.Id.ToString());

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    private async Task<LienResponse> MapToResponseAsync(Guid tenantId, Lien entity, CancellationToken ct)
    {
        var caseEntity = entity.CaseId.HasValue
            ? await _caseRepo.GetByIdAsync(tenantId, entity.CaseId.Value, ct)
            : null;
        var facility = entity.FacilityId.HasValue
            ? await _facilityRepo.GetByIdAsync(tenantId, entity.FacilityId.Value, ct)
            : null;

        var caseManagerById = await LoadCaseManagersByIdAsync(
            tenantId,
            caseEntity is null ? [] : [caseEntity],
            ct);
        var lawFirms = await _contactRepo.GetAllByTypeAsync(tenantId, ContactType.LawFirm, isActive: null, ct);
        var lawFirmById = lawFirms.ToDictionary(contact => contact.Id);
        var lawFirmByOrgId = lawFirms
            .GroupBy(contact => contact.OrgId)
            .ToDictionary(group => group.Key, group => group.First());

        return MapToResponse(
            entity,
            caseEntity,
            facility,
            ResolveLawFirmName(entity, caseEntity, lawFirmById, lawFirmByOrgId),
            ResolveCaseManagerName(caseEntity, caseManagerById));
    }

    private sealed record LienSearchCandidate(
        Lien Lien,
        Case? Case,
        Facility? Facility,
        string? LawFirm,
        string? CaseManager);

    private static FuzzyMatchScore GetLienKeywordScore(LienSearchCandidate candidate, string keyword) =>
        FuzzySearchScorer.Best(
            FuzzySearchScorer.ScorePersonName(
                candidate.Case?.ClientFirstName,
                candidate.Case?.ClientLastName,
                keyword),
            FuzzySearchScorer.ScorePersonName(
                candidate.Lien.SubjectFirstName,
                candidate.Lien.SubjectLastName,
                keyword),
            FuzzySearchScorer.ScoreFields(
                keyword,
                candidate.Lien.LienNumber,
                candidate.Lien.ExternalReference,
                candidate.Lien.Description,
                candidate.LawFirm,
                candidate.CaseManager,
                candidate.Facility?.Name));

    private static LienResponse MapToResponse(
        Lien entity,
        Case? caseEntity = null,
        Facility? facility = null,
        string? lawFirm = null,
        string? caseManager = null)
    {
        return new LienResponse
        {
            Id = entity.Id,
            LienNumber = entity.LienNumber,
            ExternalReference = entity.ExternalReference,
            LienType = entity.LienType,
            Status = entity.Status,
            StatusLabel = MapBusinessStatusLabel(entity.Status),
            CaseId = entity.CaseId,
            FacilityId = entity.FacilityId,
            OriginalAmount = entity.OriginalAmount,
            CurrentBalance = entity.CurrentBalance,
            OfferPrice = entity.OfferPrice,
            PurchasePrice = entity.PurchasePrice,
            PayoffAmount = entity.PayoffAmount,
            Jurisdiction = entity.Jurisdiction,
            IsConfidential = entity.IsConfidential,
            SubjectFirstName = entity.SubjectFirstName,
            SubjectLastName = entity.SubjectLastName,
            SubjectDisplayName = BuildDisplayName(entity.SubjectFirstName, entity.SubjectLastName),
            Plaintiff = caseEntity is null ? null : BuildDisplayName(caseEntity.ClientFirstName, caseEntity.ClientLastName),
            LawFirm = lawFirm,
            MedicalFacility = facility?.Name,
            CaseManager = caseManager,
            OrgId = entity.OrgId,
            SellingOrgId = entity.SellingOrgId,
            BuyingOrgId = entity.BuyingOrgId,
            HoldingOrgId = entity.HoldingOrgId,
            IncidentDate = entity.IncidentDate,
            PurchaseDate = entity.PurchaseDate?.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture),
            InitialServiceDate = entity.InitialServiceDate,
            EndServiceDate = entity.EndServiceDate,
            TotalPurchase = null,
            TotalBilling = null,
            IsBulk = entity.IsBulk,
            IsServicing = entity.IsServicing,
            Description = entity.Description,
            OpenedAtUtc = entity.OpenedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private static string MapBusinessStatusLabel(string status) => status switch
    {
        LienStatus.Cancelled or LienStatus.Declined => "Rejected",
        LienStatus.Settled or LienStatus.Withdrawn => "Closed",
        _ => "Open",
    };

    private async Task<Dictionary<Guid, Case>> LoadCasesByIdAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> items,
        CancellationToken ct)
    {
        var caseIds = items
            .Where(item => item.CaseId.HasValue)
            .Select(item => item.CaseId!.Value)
            .Distinct()
            .ToArray();

        return (await _caseRepo.GetByIdsAsync(tenantId, caseIds, ct))
            .ToDictionary(item => item.Id);
    }

    private async Task<Dictionary<Guid, Facility>> LoadFacilitiesByIdAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> items,
        CancellationToken ct)
    {
        var facilityIds = items
            .Where(item => item.FacilityId.HasValue)
            .Select(item => item.FacilityId!.Value)
            .Distinct()
            .ToArray();

        return (await _facilityRepo.GetByIdsAsync(tenantId, facilityIds, ct))
            .ToDictionary(item => item.Id);
    }

    private async Task<Dictionary<Guid, Contact>> LoadCaseManagersByIdAsync(
        Guid tenantId,
        IReadOnlyCollection<Case> cases,
        CancellationToken ct)
    {
        var caseManagerIds = cases
            .Select(caseEntity => GetMetadataValue(ParseCaseMetadata(caseEntity.Notes), "caseManagerId"))
            .Where(value => Guid.TryParse(value, out _))
            .Select(value => Guid.Parse(value!))
            .Distinct()
            .ToArray();

        return (await _contactRepo.GetByIdsAsync(tenantId, caseManagerIds, ct))
            .ToDictionary(contact => contact.Id);
    }

    private static string? ResolveLawFirmName(
        Lien lien,
        Case? caseEntity,
        IReadOnlyDictionary<Guid, Contact> lawFirmById,
        IReadOnlyDictionary<Guid, Contact> lawFirmByOrgId)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseCaseMetadata(caseEntity.Notes);
        var lawFirmId = GetMetadataValue(metadata, "lawFirmId");
        if (Guid.TryParse(lawFirmId, out var parsedLawFirmId) &&
            lawFirmById.TryGetValue(parsedLawFirmId, out var lawFirmContact))
        {
            return FirstNonEmpty(lawFirmContact.Organization, lawFirmContact.DisplayName);
        }

        if (lawFirmByOrgId.TryGetValue(caseEntity.OrgId, out var orgLawFirmContact))
            return FirstNonEmpty(orgLawFirmContact.Organization, orgLawFirmContact.DisplayName);

        if (lawFirmByOrgId.TryGetValue(lien.OrgId, out var lienOrgLawFirmContact))
            return FirstNonEmpty(lienOrgLawFirmContact.Organization, lienOrgLawFirmContact.DisplayName);

        return FirstNonEmpty(GetMetadataValue(metadata, "lawFirm"));
    }

    private static string? ResolveCaseManagerName(
        Case? caseEntity,
        IReadOnlyDictionary<Guid, Contact> caseManagerById)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseCaseMetadata(caseEntity.Notes);
        var caseManagerId = GetMetadataValue(metadata, "caseManagerId");
        if (Guid.TryParse(caseManagerId, out var parsedCaseManagerId) &&
            caseManagerById.TryGetValue(parsedCaseManagerId, out var caseManagerContact))
        {
            return caseManagerContact.DisplayName;
        }

        return FirstNonEmpty(GetMetadataValue(metadata, "caseManager"));
    }

    private static Dictionary<string, string> ParseCaseMetadata(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        const string legacyMetadataMarker = "[legacy-meta]";
        var rawMetadata = notes;
        var markerIndex = notes.IndexOf(legacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            rawMetadata = notes[(markerIndex + legacyMetadataMarker.Length)..].Trim();
        }
        else if (!LooksLikeLegacyMetadata(notes))
        {
            return result;
        }

        foreach (var segment in rawMetadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (key.Length > 0)
                result[key] = value;
        }

        return result;
    }

    private static bool LooksLikeLegacyMetadata(string notes)
    {
        var segments = notes.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 && segments.All(segment => segment.Contains('='));
    }

    private static string? GetMetadataValue(Dictionary<string, string> metadata, string key)
        => metadata.GetValueOrDefault(key);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var display = $"{firstName} {lastName}".Trim();
        return string.IsNullOrEmpty(display) ? null : display;
    }

    private async Task<Guid?> ResolveFacilityIdAsync(
        Guid tenantId,
        Guid requestedFacilityId,
        Guid actingUserId,
        CancellationToken ct)
    {
        if (requestedFacilityId == Guid.Empty)
            return null;

        var facility = await _facilityRepo.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (facility is not null)
            return facility.Id;

        var legacyFacilityContact = await _contactRepo.GetByIdAsync(tenantId, requestedFacilityId, ct);
        if (legacyFacilityContact is null || !IsStandaloneFacilityContact(legacyFacilityContact))
            return null;

        if (legacyFacilityContact.FacilityId.HasValue)
        {
            var linkedFacility = await _facilityRepo.GetByIdAsync(tenantId, legacyFacilityContact.FacilityId.Value, ct);
            if (linkedFacility is not null)
                return linkedFacility.Id;
        }

        var createdFacility = Facility.Create(
            legacyFacilityContact.TenantId,
            legacyFacilityContact.OrgId,
            ResolveFacilityName(legacyFacilityContact),
            actingUserId,
            addressLine1: legacyFacilityContact.AddressLine1,
            city: legacyFacilityContact.City,
            state: legacyFacilityContact.State,
            postalCode: legacyFacilityContact.PostalCode,
            phone: legacyFacilityContact.Phone,
            email: legacyFacilityContact.Email,
            fax: legacyFacilityContact.Fax);

        await _facilityRepo.AddAsync(createdFacility, ct);

        legacyFacilityContact.Update(
            legacyFacilityContact.FirstName,
            legacyFacilityContact.LastName,
            legacyFacilityContact.ContactType,
            actingUserId,
            facilityId: createdFacility.Id,
            contactSubtype: legacyFacilityContact.ContactSubtype,
            title: legacyFacilityContact.Title,
            organization: legacyFacilityContact.Organization,
            email: legacyFacilityContact.Email,
            phone: legacyFacilityContact.Phone,
            fax: legacyFacilityContact.Fax,
            website: legacyFacilityContact.Website,
            addressLine1: legacyFacilityContact.AddressLine1,
            city: legacyFacilityContact.City,
            state: legacyFacilityContact.State,
            postalCode: legacyFacilityContact.PostalCode,
            notes: legacyFacilityContact.Notes);

        await _contactRepo.UpdateAsync(legacyFacilityContact, ct);

        _logger.LogInformation(
            "Legacy facility contact {ContactId} linked to facility {FacilityId} for tenant {TenantId}",
            legacyFacilityContact.Id,
            createdFacility.Id,
            tenantId);

        return createdFacility.Id;
    }

    private static bool IsStandaloneFacilityContact(Contact contact) =>
        (contact.ContactType == ContactType.Facility || contact.ContactType == ContactType.MedicalFacility)
        && string.IsNullOrWhiteSpace(contact.ContactSubtype);

    private static string ResolveFacilityName(Contact contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.Organization))
            return contact.Organization.Trim();

        return contact.DisplayName;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Lien '{id}' not found for tenant '{tenantId}'.");

        if (LienStatus.Terminal.Contains(entity.Status))
        {
            // A closed lien can still be removed from the case. Use the same
            // hidden terminal state as other delete requests so list APIs omit it.
            entity.SetLegacyMedicalStatus(LienStatus.Cancelled, actingUserId);
        }
        else if (LienStatus.AllowedTransitions.TryGetValue(entity.Status, out var allowed) &&
            allowed.Contains(LienStatus.Cancelled))
        {
            entity.TransitionStatus(LienStatus.Cancelled, actingUserId);
        }
        else
        {
            entity.TransitionStatus(LienStatus.Withdrawn, actingUserId);
        }

        await _lienRepo.UpdateAsync(entity, ct);
        await RecordStatusHistoryAsync(entity, actingUserId, "Lien status updated to Delete.", ct);

        _logger.LogInformation(
            "Lien deleted (status={Status}): {LienId} Tenant={TenantId}", entity.Status, entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.lien.deleted",
            action: "delete",
            description: $"Lien '{entity.LienNumber}' deleted (transitioned to {entity.Status})",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: entity.Id.ToString());
    }

    private Task RecordStatusHistoryAsync(
        Lien entity,
        Guid actingUserId,
        string description,
        CancellationToken ct) =>
        _lienStatusHistoryRepo.AddAsync(
            LienStatusHistory.Create(entity.TenantId, entity.Id, entity.CaseId, description, actingUserId),
            ct);
}
