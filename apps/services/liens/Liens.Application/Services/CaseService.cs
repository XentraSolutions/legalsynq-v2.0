using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class CaseService : ICaseService
{
    private const string LegacyMetadataMarker = "[legacy-meta]";
    private readonly ICaseRepository           _caseRepo;
    private readonly IAuditPublisher           _audit;
    private readonly ILienTaskGenerationEngine _taskGenEngine;
    private readonly ILogger<CaseService>      _logger;

    public CaseService(
        ICaseRepository caseRepo,
        IAuditPublisher audit,
        ILienTaskGenerationEngine taskGenEngine,
        ILogger<CaseService> logger)
    {
        _caseRepo      = caseRepo;
        _audit         = audit;
        _taskGenEngine = taskGenEngine;
        _logger        = logger;
    }

    public async Task<PaginatedResult<CaseResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize,
        Guid? orgId = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _caseRepo.SearchAsync(
            tenantId,
            search,
            status,
            page,
            pageSize,
            orgId,
            ct: ct);

        return new PaginatedResult<CaseResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
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
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        var (items, totalCount) = await _caseRepo.SearchAsync(
            tenantId,
            keyword,
            statusId,
            page,
            limit,
            lawFirmOrgId,
            sortBy,
            sortDirection,
            accidentTypeId,
            caseManagerId,
            ct);

        return new PaginatedResult<CaseResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = limit,
            TotalCount = totalCount,
        };
    }

    public async Task<CaseResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<CaseResponse?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        var entity = await _caseRepo.GetByCaseNumberAsync(tenantId, caseNumber, ct);
        return entity is null ? null : MapToResponse(entity);
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
                    leadId: request.LeadId)));

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

        // Fire-and-observe: task generation failure must not block case creation
        var caseId    = entity.Id;
        var genContext = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      Domain.Enums.TaskGenerationEventType.CaseCreated,
            EntityType:     "CASE",
            EntityId:       caseId,
            CaseId:         caseId,
            LienId:         null,
            WorkflowStageId: null,
            ActorUserId:    actingUserId);

        _ = _taskGenEngine.TriggerAsync(genContext, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(t.Exception, "Task generation failed for case {CaseId}.", caseId);
            }, TaskContinuationOptions.OnlyOnFaulted);

        return MapToResponse(entity);
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
            notes: SerializeCaseNotes(
                request.Notes ?? noteBody,
                MergeMetadata(
                    metadata,
                    request.Sex,
                    request.CaseType,
                    request.CurrentMedicalStatus,
                    request.StateOfIncident,
                    request.TrackingFollowUpDate,
                    request.LeadId)));

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

        return MapToResponse(entity);
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

    private static CaseResponse MapToResponse(Case entity)
    {
        var noteBody = ExtractUserNotes(entity.Notes);
        var metadata = ParseCaseMetadata(entity.Notes);
        var address = SplitAddress(entity.ClientAddress);
        return new CaseResponse
        {
            Id = entity.Id,
            CaseNumber = entity.CaseNumber,
            ExternalReference = entity.ExternalReference,
            Title = entity.Title,
            ClientFirstName = entity.ClientFirstName,
            ClientLastName = entity.ClientLastName,
            ClientDisplayName = $"{entity.ClientFirstName} {entity.ClientLastName}".Trim(),
            Status = entity.Status,
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
            StateOfIncident = GetMetadataValue(metadata, "accidentState"),
            TrackingFollowUpDate = ParseMetadataDate(GetMetadataValue(metadata, "trackingFollowUpDate")),
            LeadId = GetMetadataValue(metadata, "leadId"),
            OpenedAtUtc = entity.OpenedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }

    private static Dictionary<string, string> BuildMetadata(
        string? sex,
        string? caseType,
        string? currentMedicalStatus,
        string? stateOfIncident,
        DateOnly? trackingFollowUpDate,
        string? leadId)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        SetMetadataValue(metadata, "gender", sex);
        SetMetadataValue(metadata, "accidentType", caseType);
        SetMetadataValue(metadata, "currentMedicalStatus", currentMedicalStatus);
        SetMetadataValue(metadata, "accidentState", stateOfIncident);
        SetMetadataValue(
            metadata,
            "trackingFollowUpDate",
            trackingFollowUpDate?.ToString("MM/dd/yyyy"));
        SetMetadataValue(metadata, "leadId", leadId);
        return metadata;
    }

    private static Dictionary<string, string> MergeMetadata(
        Dictionary<string, string> existing,
        string? sex,
        string? caseType,
        string? currentMedicalStatus,
        string? stateOfIncident,
        DateOnly? trackingFollowUpDate,
        string? leadId)
    {
        var metadata = new Dictionary<string, string>(existing, StringComparer.Ordinal);
        if (sex is not null)
            SetMetadataValue(metadata, "gender", sex);
        if (caseType is not null)
            SetMetadataValue(metadata, "accidentType", caseType);
        if (currentMedicalStatus is not null)
            SetMetadataValue(metadata, "currentMedicalStatus", currentMedicalStatus);
        if (stateOfIncident is not null)
            SetMetadataValue(metadata, "accidentState", stateOfIncident);
        if (trackingFollowUpDate.HasValue)
            SetMetadataValue(metadata, "trackingFollowUpDate", trackingFollowUpDate.Value.ToString("MM/dd/yyyy"));
        if (leadId is not null)
            SetMetadataValue(metadata, "leadId", leadId);
        return metadata;
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

    private static bool LooksLikeLegacyMetadata(string notes)
    {
        var segments = notes.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment.Contains('='));
    }

    private static string? GetMetadataValue(Dictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) ? value : null;

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
