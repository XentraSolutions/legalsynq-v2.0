using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Application.Search;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class CaseService : ICaseService
{
    private const string LegacyMetadataMarker = "[legacy-meta]";
    private readonly ICaseRepository           _caseRepo;
    private readonly IContactRepository        _contactRepo;
    private readonly IAuditPublisher           _audit;
    private readonly ILienTaskGenerationDispatcher _taskGenDispatcher;
    private readonly ILogger<CaseService>          _logger;

    public CaseService(
        ICaseRepository caseRepo,
        IContactRepository contactRepo,
        IAuditPublisher audit,
        ILienTaskGenerationDispatcher taskGenDispatcher,
        ILogger<CaseService> logger)
    {
        _caseRepo          = caseRepo;
        _contactRepo       = contactRepo;
        _audit             = audit;
        _taskGenDispatcher = taskGenDispatcher;
        _logger            = logger;
    }

    public async Task<PaginatedResult<CaseResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize,
        Guid? orgId = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var hasKeyword = !string.IsNullOrWhiteSpace(search);
        var keyword = search?.Trim();
        var (items, totalCount) = await _caseRepo.SearchAsync(
            tenantId,
            hasKeyword ? null : search,
            status,
            hasKeyword ? 1 : page,
            hasKeyword ? FuzzySearchScorer.CandidateLimit : pageSize,
            orgId,
            ct: ct);

        if (hasKeyword)
        {
            var matches = items
                .Select(item => new { Item = item, Score = GetCaseKeywordScore(item, keyword!) })
                .Where(match => FuzzySearchScorer.IsAccepted(match.Score))
                .OrderByDescending(match => match.Score.Value)
                .ThenByDescending(match => match.Item.Id)
                .ToList();

            totalCount = matches.Count;
            items = matches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(match => match.Item)
                .ToList();
        }

        return new PaginatedResult<CaseResponse>
        {
            Items = items.Select(item => MapToResponse(item)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<PaginatedResult<CaseResponse>> SearchV3Async(
        Guid tenantId,
        string? keyword,
        string? statusId,
        int page,
        int limit,
        string? sortBy,
        string? sortDirection,
        Guid? lawFirmOrgId = null,
        string? accidentTypeId = null,
        string? caseManagerId = null,
        string? lawFirmIds = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        var hasKeyword = !string.IsNullOrWhiteSpace(keyword);
        var normalizedKeyword = keyword?.Trim();
        var (items, totalCount) = await _caseRepo.SearchAsync(
            tenantId,
            hasKeyword ? null : keyword,
            statusId,
            hasKeyword ? 1 : page,
            hasKeyword ? FuzzySearchScorer.CandidateLimit : limit,
            lawFirmOrgId,
            sortBy,
            sortDirection,
            accidentTypeId,
            caseManagerId,
            lawFirmIds,
            ct);

        var lawFirmContacts = await _contactRepo.GetAllByTypeAsync(
            tenantId,
            ContactType.LawFirm,
            isActive: null,
            ct);

        var lawFirmById = lawFirmContacts.ToDictionary(c => c.Id);
        var lawFirmByOrgId = lawFirmContacts
            .GroupBy(c => c.OrgId)
            .ToDictionary(g => g.Key, g => g.First());

        var needsCaseManagers = items.Any(item =>
            !string.IsNullOrWhiteSpace(GetMetadataValue(ParseCaseMetadata(item.Notes), "caseManagerId")));

        Dictionary<Guid, Contact> caseManagerById = new();
        if (needsCaseManagers)
        {
            caseManagerById = (await _contactRepo.GetAllByTypeAsync(
                    tenantId,
                    contactType: null,
                    isActive: null,
                    ct))
                .ToDictionary(c => c.Id);
        }

        var candidates = items.Select(item =>
        {
            var metadata = ParseCaseMetadata(item.Notes);
            var lawFirmId = GetMetadataValue(metadata, "lawFirmId");
            var caseManagerIdValue = GetMetadataValue(metadata, "caseManagerId");

            return new CaseSearchCandidate(
                item,
                ResolveLawFirmName(item.OrgId, lawFirmId, lawFirmById, lawFirmByOrgId),
                ResolveCaseManagerName(caseManagerIdValue, caseManagerById));
        }).ToList();

        if (hasKeyword)
        {
            var matches = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Score = GetCaseKeywordScore(
                        candidate.Case,
                        normalizedKeyword!,
                        candidate.LawFirm,
                        candidate.CaseManager),
                })
                .Where(match => FuzzySearchScorer.IsAccepted(match.Score))
                .OrderByDescending(match => match.Score.Value)
                .ThenByDescending(match => match.Candidate.Case.Id)
                .ToList();

            totalCount = matches.Count;
            candidates = matches
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(match => match.Candidate)
                .ToList();
        }

        return new PaginatedResult<CaseResponse>
        {
            Items = candidates.Select(candidate => MapToResponse(
                candidate.Case,
                lawFirm: candidate.LawFirm,
                caseManager: candidate.CaseManager)).ToList(),
            Page = page,
            PageSize = limit,
            TotalCount = totalCount,
        };
    }

    public async Task<CaseResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : await MapToResponseAsync(tenantId, entity, ct);
    }

    public async Task<CaseResponse?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByCaseNumberAsync(tenantId, caseNumber, ct);
        return entity is null ? null : await MapToResponseAsync(tenantId, entity, ct);
    }

    public async Task<CaseResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateCaseRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ClientFirstName))
            errors.Add("clientFirstName", ["Client first name is required."]);
        if (string.IsNullOrWhiteSpace(request.ClientLastName))
            errors.Add("clientLastName", ["Client last name is required."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing.", errors);

        if (!string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            var idempotent = await _caseRepo.GetByExternalReferenceAsync(
                tenantId, request.ExternalReference.Trim(), ct);
            if (idempotent is not null)
                return await MapToResponseAsync(tenantId, idempotent, ct);
        }

        var caseNumber = string.IsNullOrWhiteSpace(request.CaseNumber)
            ? await GenerateCaseNumberAsync(tenantId, ct)
            : request.CaseNumber.Trim();

        var existing = await _caseRepo.GetByCaseNumberAsync(tenantId, caseNumber, ct);
        if (existing is not null)
            throw new ConflictException(
                $"A case with number '{caseNumber}' already exists.",
                "CASE_NUMBER_DUPLICATE");

        var entity = Case.Create(
            tenantId: tenantId,
            orgId: orgId,
            caseNumber: caseNumber,
            clientFirstName: request.ClientFirstName,
            clientLastName: request.ClientLastName,
            createdByUserId: actingUserId,
            externalReference: request.ExternalReference,
            title: request.Title,
            clientDob: request.ClientDob,
            clientPhone: request.ClientPhone,
            clientEmail: request.ClientEmail,
            clientAddress: request.ClientAddress,
            dateOfIncident: request.DateOfIncident,
            insuranceCarrier: request.InsuranceCarrier,
            policyNumber: request.PolicyNumber,
            claimNumber: request.ClaimNumber,
            description: request.Description,
            notes: SerializeCaseNotes(
                request.Notes,
                BuildMetadata(
                    sex: request.Sex,
                    caseType: request.CaseType,
                    currentMedicalStatus: request.CurrentMedicalStatus,
                    stateOfIncident: request.StateOfIncident,
                    trackingFollowUpDate: request.TrackingFollowUpDate,
                    leadId: request.LeadId,
                    shareCase: request.ShareCase,
                    minorComp: request.MinorComp,
                    caseDropped: request.CaseDropped,
                    childSupportLiens: request.ChildSupportLiens,
                    isUccFiled: request.IsUccFiled,
                    lawFirmId: request.LawFirmId,
                    accidentTypeId: request.AccidentTypeId,
                    caseManagerId: request.CaseManagerId,
                    statusLabel: request.StatusLabel)));

        await _caseRepo.AddAsync(entity, ct);

        _logger.LogInformation(
            "Case created: {CaseId} CaseNumber={CaseNumber} Tenant={TenantId}",
            entity.Id, entity.CaseNumber, tenantId);

        _audit.Publish(
            eventType: "liens.case.created",
            action: "create",
            description: $"Case '{entity.CaseNumber}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Case",
            entityId: entity.Id.ToString());

        // Run task generation in an isolated scope so it never reuses the request DbContext.
        var caseId = entity.Id;
        var genContext = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      Domain.Enums.TaskGenerationEventType.CaseCreated,
            EntityType:     "CASE",
            EntityId:       caseId,
            CaseId:         caseId,
            LienId:         null,
            WorkflowStageId: null,
            ActorUserId:    actingUserId);

        _taskGenDispatcher.Dispatch(genContext);

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    private async Task<string> GenerateCaseNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");
        var prefix = $"{yearPrefix}-";
        var existingCases = await _caseRepo.GetByCaseNumberPrefixAsync(tenantId, prefix, ct);
        var maxSequence = existingCases
            .Select(c => TryGetCaseSequence(c.CaseNumber, prefix))
            .Where(sequence => sequence.HasValue)
            .Select(sequence => sequence!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxSequence + 1:000000}";
    }

    private static int? TryGetCaseSequence(string caseNumber, string prefix)
    {
        if (!caseNumber.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var suffix = caseNumber[prefix.Length..];
        return int.TryParse(suffix, out var sequence) ? sequence : null;
    }

    public async Task<CaseResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateCaseRequest request, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Case '{id}' not found for tenant '{tenantId}'.");
        var noteBody = ExtractUserNotes(entity.Notes);
        var metadata = ParseCaseMetadata(entity.Notes);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ClientFirstName))
            errors.Add("clientFirstName", ["Client first name is required."]);
        if (string.IsNullOrWhiteSpace(request.ClientLastName))
            errors.Add("clientLastName", ["Client last name is required."]);
        if (request.Status is not null && !CaseStatus.All.Contains(request.Status))
            errors.Add("status", [$"Invalid status: '{request.Status}'. Valid values: {string.Join(", ", CaseStatus.All)}"]);
        if (request.DemandAmount.HasValue && request.DemandAmount.Value < 0)
            errors.Add("demandAmount", ["Demand amount cannot be negative."]);
        if (request.SettlementAmount.HasValue && request.SettlementAmount.Value < 0)
            errors.Add("settlementAmount", ["Settlement amount cannot be negative."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more fields are invalid.", errors);

        var mergedMetadata = MergeMetadata(
            metadata,
            request.Sex,
            request.CaseType,
            request.CurrentMedicalStatus,
            request.StateOfIncident,
            request.TrackingFollowUpDate,
            request.LeadId,
            request.ShareCase,
            request.MinorComp,
            request.CaseDropped,
            request.ChildSupportLiens,
            request.IsUccFiled,
            request.LawFirmId,
            request.PendingLawFirmId,
            request.AccidentTypeId,
            request.CaseManagerId,
            request.AttorneyId,
            request.SwitchedDate);
        ApplyStatusLabelMetadata(mergedMetadata, request.Status, request.StatusLabel);

        entity.Update(
            clientFirstName: request.ClientFirstName,
            clientLastName: request.ClientLastName,
            updatedByUserId: actingUserId,
            title: request.Title,
            externalReference: request.ExternalReference,
            clientDob: request.ClientDob,
            clientPhone: request.ClientPhone,
            clientEmail: request.ClientEmail,
            clientAddress: request.ClientAddress,
            dateOfIncident: request.DateOfIncident,
            insuranceCarrier: request.InsuranceCarrier,
            policyNumber: request.PolicyNumber,
            claimNumber: request.ClaimNumber,
            description: request.Description,
            notes: SerializeCaseNotes(request.Notes ?? noteBody, mergedMetadata));

        if (request.Status is not null && request.Status != entity.Status)
            entity.TransitionStatus(request.Status, actingUserId);

        if (request.DemandAmount.HasValue)
            entity.SetDemandAmount(request.DemandAmount.Value, actingUserId);

        if (request.SettlementAmount.HasValue)
            entity.SetSettlementAmount(request.SettlementAmount.Value, actingUserId);

        await _caseRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Case updated: {CaseId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType: "liens.case.updated",
            action: "update",
            description: $"Case '{entity.CaseNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Case",
            entityId: entity.Id.ToString());

        return await MapToResponseAsync(tenantId, entity, ct);
    }

    private async Task<CaseResponse> MapToResponseAsync(
        Guid tenantId,
        Case entity,
        CancellationToken ct)
    {
        var metadata = ParseCaseMetadata(entity.Notes);
        var lawFirmId = GetMetadataValue(metadata, "lawFirmId");
        var caseManagerId = GetMetadataValue(metadata, "caseManagerId");

        string? lawFirm = null;
        if (Guid.TryParse(lawFirmId, out var parsedLawFirmId))
        {
            var lawFirmContact = await _contactRepo.GetByIdAsync(tenantId, parsedLawFirmId, ct);
            lawFirm = FirstNonEmpty(lawFirmContact?.Organization, lawFirmContact?.DisplayName);
        }

        if (string.IsNullOrWhiteSpace(lawFirm))
        {
            var defaultLawFirm = (await _contactRepo.GetAllByTypeAsync(
                    tenantId,
                    ContactType.LawFirm,
                    isActive: null,
                    ct))
                .FirstOrDefault(contact => contact.OrgId == entity.OrgId);

            lawFirm = FirstNonEmpty(defaultLawFirm?.Organization, defaultLawFirm?.DisplayName);
        }

        string? caseManager = null;
        if (Guid.TryParse(caseManagerId, out var parsedCaseManagerId))
        {
            var caseManagerContact = await _contactRepo.GetByIdAsync(tenantId, parsedCaseManagerId, ct);
            caseManager = caseManagerContact?.DisplayName;
        }

        return MapToResponse(entity, lawFirm, caseManager);
    }

    public async Task<bool> ReassignLawFirmAsync(
        Guid tenantId,
        Guid caseId,
        Guid lawFirmOrgId,
        Guid actingUserId,
        CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
        if (entity is null)
            return false;

        entity.ReassignLawFirm(lawFirmOrgId, actingUserId);
        await _caseRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Case law firm reassigned: {CaseId} NewOrg={OrgId} Tenant={TenantId}",
            entity.Id, lawFirmOrgId, tenantId);

        _audit.Publish(
            eventType: "liens.case.reassigned.lawfirm",
            action: "update",
            description: $"Case '{entity.CaseNumber}' reassigned to law firm '{lawFirmOrgId}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Case",
            entityId: entity.Id.ToString());

        return true;
    }

    public async Task<bool> ReassignCaseManagerAsync(
        Guid tenantId,
        Guid caseId,
        Guid caseManagerId,
        Guid actingUserId,
        CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
        if (entity is null)
            return false;

        entity.ReassignCaseManager(caseManagerId, actingUserId);
        await _caseRepo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Case manager reassigned: {CaseId} CaseManager={CaseManagerId} Tenant={TenantId}",
            entity.Id, caseManagerId, tenantId);

        _audit.Publish(
            eventType: "liens.case.reassigned.casemanager",
            action: "update",
            description: $"Case '{entity.CaseNumber}' reassigned to case manager '{caseManagerId}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Case",
            entityId: entity.Id.ToString());

        return true;
    }

    private sealed record CaseSearchCandidate(Case Case, string? LawFirm, string? CaseManager);

    private static FuzzyMatchScore GetCaseKeywordScore(
        Case caseEntity,
        string keyword,
        string? lawFirm = null,
        string? caseManager = null) =>
        FuzzySearchScorer.Best(
            FuzzySearchScorer.ScorePersonName(
                caseEntity.ClientFirstName,
                caseEntity.ClientLastName,
                keyword),
            FuzzySearchScorer.ScoreFields(
                keyword,
                caseEntity.CaseNumber,
                caseEntity.ExternalReference,
                caseEntity.Title,
                lawFirm,
                caseManager));

    private static CaseResponse MapToResponse(
        Case entity,
        string? lawFirm = null,
        string? caseManager = null)
    {
        var noteBody = ExtractUserNotes(entity.Notes);
        var metadata = ParseCaseMetadata(entity.Notes);
        var address = SplitAddress(entity.ClientAddress);
        var lawFirmId = GetMetadataValue(metadata, "lawFirmId");
        var lawFirmName = FirstNonEmpty(GetMetadataValue(metadata, "lawFirm"), lawFirm);
        var caseManagerId = GetMetadataValue(metadata, "caseManagerId");
        var caseManagerName = FirstNonEmpty(GetMetadataValue(metadata, "caseManager"), caseManager);
        var accidentTypeId = GetMetadataValue(metadata, "accidentTypeId");
        var accidentType = GetMetadataValue(metadata, "accidentType");

        return new CaseResponse
        {
            Id = entity.Id,
            CaseNumber = entity.CaseNumber,
            ExternalReference = entity.ExternalReference,
            Title = entity.Title,
            ClientFirstName = entity.ClientFirstName,
            ClientLastName = entity.ClientLastName,
            ClientDisplayName = $"{entity.ClientFirstName} {entity.ClientLastName}".Trim(),
            Status = ResolveCaseStatusValue(entity.Status, GetMetadataValue(metadata, "statusLabel")),
            StatusLabel = ResolveCaseStatusLabel(entity.Status, GetMetadataValue(metadata, "statusLabel")),
            DateOfIncident = entity.DateOfIncident,
            ClientDob = entity.ClientDob,
            ClientPhone = entity.ClientPhone,
            ClientEmail = entity.ClientEmail,
            ClientAddress = entity.ClientAddress,
            ClientStreetAddress = address.Address,
            ClientCity = address.City,
            ClientState = address.State,
            ClientZipcode = address.Zipcode,
            InsuranceCarrier = entity.InsuranceCarrier,
            PolicyNumber = entity.PolicyNumber,
            ClaimNumber = entity.ClaimNumber,
            DemandAmount = entity.DemandAmount,
            SettlementAmount = entity.SettlementAmount,
            Description = entity.Description,
            Notes = noteBody,
            Sex = GetMetadataValue(metadata, "gender"),
            CaseType = GetMetadataValue(metadata, "accidentType"),
            CurrentMedicalStatus = GetMetadataValue(metadata, "currentMedicalStatus"),
            StateOfIncident = GetMetadataValue(metadata, "stateOfIncident", "accidentState", "state"),
            TrackingFollowUpDate = ParseMetadataDate(GetMetadataValue(metadata, "trackingFollowUpDate")),
            LeadId = GetMetadataValue(metadata, "leadId"),
            ShareCase = NormalizeCaseFlagForResponseOrDefaultFalse(GetMetadataValue(metadata, "shareCase")),
            MinorComp = NormalizeCaseFlagForResponseOrDefaultFalse(GetMetadataValue(metadata, "minorComp")),
            CaseDropped = NormalizeCaseFlagForResponseOrDefaultFalse(GetMetadataValue(metadata, "caseDropped")),
            ChildSupportLiens = NormalizeCaseFlagForResponseOrDefaultFalse(GetMetadataValue(metadata, "childSupportLiens")),
            IsUccFiled = NormalizeCaseFlagForResponseOrDefaultFalse(
                FirstNonEmpty(
                    GetMetadataValue(metadata, "isUccFiled"),
                    GetMetadataValue(metadata, "isUCCFiled"))),
            LawFirmId = lawFirmId,
            PendingLawFirmId = GetMetadataValue(metadata, "pendingLawFirmId"),
            LawFirm = lawFirmName,
            CaseManagerId = caseManagerId,
            CaseManager = caseManagerName,
            AttorneyId = FirstNonEmpty(
                GetMetadataValue(metadata, "attorneyId"),
                GetMetadataValue(metadata, "attorney")),
            SwitchedDate = GetMetadataValue(metadata, "switchedDate"),
            AccidentTypeId = accidentTypeId,
            AccidentType = accidentType,
            OpenedAtUtc = entity.OpenedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private static string? GetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static Dictionary<string, string> BuildMetadata(
        string? sex,
        string? caseType,
        string? currentMedicalStatus,
        string? stateOfIncident,
        DateOnly? trackingFollowUpDate,
        string? leadId,
        string? shareCase,
        string? minorComp,
        string? caseDropped,
        string? childSupportLiens,
        string? isUccFiled,
        string? lawFirmId,
        string? accidentTypeId,
        string? caseManagerId,
        string? statusLabel)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        SetMetadataValue(metadata, "gender", sex);
        SetMetadataValue(metadata, "accidentType", caseType);
        SetMetadataValue(metadata, "accidentTypeId", accidentTypeId);
        SetMetadataValue(metadata, "currentMedicalStatus", currentMedicalStatus);
        SetMetadataValue(metadata, "accidentState", stateOfIncident);
        SetMetadataValue(
            metadata,
            "trackingFollowUpDate",
            trackingFollowUpDate?.ToString("MM/dd/yyyy"));
        SetMetadataValue(metadata, "leadId", leadId);
        SetMetadataValue(metadata, "shareCase", NormalizeCaseFlagForStorage(shareCase));
        SetMetadataValue(metadata, "minorComp", NormalizeCaseFlagForStorage(minorComp));
        SetMetadataValue(metadata, "caseDropped", NormalizeCaseFlagForStorage(caseDropped));
        SetMetadataValue(metadata, "childSupportLiens", NormalizeCaseFlagForStorage(childSupportLiens));
        SetMetadataValue(metadata, "isUccFiled", NormalizeCaseFlagForStorage(isUccFiled));
        SetMetadataValue(metadata, "lawFirmId", lawFirmId);
        SetMetadataValue(metadata, "caseManagerId", caseManagerId);
        SetMetadataValue(metadata, "statusLabel", statusLabel);
        return metadata;
    }

    private static Dictionary<string, string> MergeMetadata(
        Dictionary<string, string> existing,
        string? sex,
        string? caseType,
        string? currentMedicalStatus,
        string? stateOfIncident,
        DateOnly? trackingFollowUpDate,
        string? leadId,
        string? shareCase,
        string? minorComp,
        string? caseDropped,
        string? childSupportLiens,
        string? isUccFiled,
        string? lawFirmId,
        string? pendingLawFirmId,
        string? accidentTypeId,
        string? caseManagerId,
        string? attorneyId,
        string? switchedDate)
    {
        var metadata = new Dictionary<string, string>(existing, StringComparer.Ordinal);
        if (sex is not null)
            SetMetadataValue(metadata, "gender", sex);
        if (caseType is not null)
            SetMetadataValue(metadata, "accidentType", caseType);
        if (accidentTypeId is not null)
            SetMetadataValue(metadata, "accidentTypeId", accidentTypeId);
        if (currentMedicalStatus is not null)
            SetMetadataValue(metadata, "currentMedicalStatus", currentMedicalStatus);
        if (stateOfIncident is not null)
            SetMetadataValue(metadata, "accidentState", stateOfIncident);
        if (trackingFollowUpDate.HasValue)
            SetMetadataValue(metadata, "trackingFollowUpDate", trackingFollowUpDate.Value.ToString("MM/dd/yyyy"));
        if (leadId is not null)
            SetMetadataValue(metadata, "leadId", leadId);
        if (shareCase is not null)
            SetMetadataValue(metadata, "shareCase", NormalizeCaseFlagForStorage(shareCase));
        if (minorComp is not null)
            SetMetadataValue(metadata, "minorComp", NormalizeCaseFlagForStorage(minorComp));
        if (caseDropped is not null)
            SetMetadataValue(metadata, "caseDropped", NormalizeCaseFlagForStorage(caseDropped));
        if (childSupportLiens is not null)
            SetMetadataValue(metadata, "childSupportLiens", NormalizeCaseFlagForStorage(childSupportLiens));
        if (isUccFiled is not null)
        {
            metadata.Remove("isUCCFiled");
            SetMetadataValue(metadata, "isUccFiled", NormalizeCaseFlagForStorage(isUccFiled));
        }
        if (lawFirmId is not null)
        {
            metadata.Remove("lawFirm");
            SetMetadataValue(metadata, "lawFirmId", lawFirmId);
        }
        if (pendingLawFirmId is not null)
            SetMetadataValue(metadata, "pendingLawFirmId", pendingLawFirmId);
        if (caseManagerId is not null)
            SetMetadataValue(metadata, "caseManagerId", caseManagerId);
        if (attorneyId is not null)
        {
            metadata.Remove("attorney");
            SetMetadataValue(metadata, "attorneyId", attorneyId);
        }
        if (switchedDate is not null)
            SetMetadataValue(metadata, "switchedDate", switchedDate);
        return metadata;
    }

    private static void ApplyStatusLabelMetadata(
        Dictionary<string, string> metadata,
        string? status,
        string? statusLabel)
    {
        if (statusLabel is not null)
        {
            SetMetadataValue(metadata, "statusLabel", statusLabel);
            return;
        }

        if (status is not null && !string.Equals(status, CaseStatus.InNegotiation, StringComparison.Ordinal))
            metadata.Remove("statusLabel");
    }

    private static void SetMetadataValue(Dictionary<string, string> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            metadata.Remove(key);
            return;
        }

        metadata[key] = value.Trim();
    }

    private static string? SerializeCaseNotes(string? noteBody, Dictionary<string, string> metadata)
    {
        var cleanBody = string.IsNullOrWhiteSpace(noteBody) ? null : noteBody.Trim();
        if (metadata.Count == 0)
            return cleanBody;

        var serialized = string.Join("; ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
        return cleanBody is null
            ? $"{LegacyMetadataMarker}{Environment.NewLine}{serialized}"
            : $"{cleanBody}{Environment.NewLine}{Environment.NewLine}{LegacyMetadataMarker}{Environment.NewLine}{serialized}";
    }

    private static string? ExtractUserNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        var markerIndex = notes.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            var body = notes[..markerIndex].Trim();
            return body.Length == 0 ? null : body;
        }

        return LooksLikeLegacyMetadata(notes) ? null : notes;
    }

    private static Dictionary<string, string> ParseCaseMetadata(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        var rawMetadata = notes;
        var markerIndex = notes.IndexOf(LegacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            rawMetadata = notes[(markerIndex + LegacyMetadataMarker.Length)..].Trim();
        }
        else if (!LooksLikeLegacyMetadata(notes))
        {
            return result;
        }

        foreach (var segment in rawMetadata.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (key.Length > 0)
                result[key] = value;
        }

        return result;
    }

    private static string? ResolveLawFirmName(
        Guid orgId,
        string? lawFirmId,
        IReadOnlyDictionary<Guid, Contact> lawFirmById,
        IReadOnlyDictionary<Guid, Contact> lawFirmByOrgId)
    {
        if (Guid.TryParse(lawFirmId, out var parsedLawFirmId) &&
            lawFirmById.TryGetValue(parsedLawFirmId, out var lawFirmContactById))
        {
            return FirstNonEmpty(lawFirmContactById.Organization, lawFirmContactById.DisplayName);
        }

        if (lawFirmByOrgId.TryGetValue(orgId, out var lawFirmContactByOrg))
        {
            return FirstNonEmpty(lawFirmContactByOrg.Organization, lawFirmContactByOrg.DisplayName);
        }

        return null;
    }

    private static string? ResolveCaseManagerName(
        string? caseManagerId,
        IReadOnlyDictionary<Guid, Contact> caseManagerById)
    {
        if (Guid.TryParse(caseManagerId, out var parsedCaseManagerId) &&
            caseManagerById.TryGetValue(parsedCaseManagerId, out var caseManagerContact))
        {
            return caseManagerContact.DisplayName;
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string ResolveCaseStatusLabel(string status, string? customStatusLabel)
    {
        if (!string.IsNullOrWhiteSpace(customStatusLabel))
            return customStatusLabel.Trim();

        return status switch
        {
            CaseStatus.PreDemand => "Pre-Demand",
            CaseStatus.DemandSent => "Demand Sent",
            CaseStatus.InNegotiation => "In Negotiation",
            CaseStatus.CaseSettled => "Case Settled",
            CaseStatus.Closed => "Closed",
            _ => status,
        };
    }

    private static string ResolveCaseStatusValue(string status, string? customStatusLabel)
    {
        if (!string.IsNullOrWhiteSpace(customStatusLabel))
            return customStatusLabel.Trim();

        return status;
    }

    private static string? NormalizeCaseFlagForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "Y" => "Yes",
            "FALSE" or "NO" or "N" => "No",
            _ => value.Trim(),
        };
    }

    private static string? NormalizeCaseFlagForResponse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "Y" => "Yes",
            "FALSE" or "NO" or "N" => "No",
            _ => value.Trim(),
        };
    }

    private static string NormalizeCaseFlagForResponseOrDefaultFalse(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "false"
            : NormalizeCaseFlagForResponse(value) ?? "false";

    private static bool LooksLikeLegacyMetadata(string notes)
    {
        var segments = notes.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment.Contains('='));
    }

    private static string? GetMetadataValue(Dictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value))
            return value;

        foreach (var pair in metadata)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }

    private static DateOnly? ParseMetadataDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }

    private static (string? Address, string? City, string? State, string? Zipcode) SplitAddress(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return (null, null, null, null);

        var parts = rawAddress
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length >= 4)
        {
            return (
                string.Join(", ", parts.Take(parts.Length - 3)),
                parts[^3],
                parts[^2],
                parts[^1]);
        }

        if (parts.Length == 3)
            return (parts[0], parts[1], parts[2], null);

        if (parts.Length == 2)
            return (parts[0], parts[1], null, null);

        return (rawAddress.Trim(), null, null, null);
    }
}
