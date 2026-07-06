using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienService : ILienService
{
    private readonly ILienRepository           _lienRepo;
    private readonly ICaseRepository           _caseRepo;
    private readonly IContactRepository        _contactRepo;
    private readonly IFacilityRepository       _facilityRepo;
    private readonly IAuditPublisher           _audit;
    private readonly ILienTaskGenerationEngine _taskGenEngine;
    private readonly ILogger<LienService>      _logger;

    public LienService(
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IContactRepository contactRepo,
        IFacilityRepository facilityRepo,
        IAuditPublisher audit,
        ILienTaskGenerationEngine taskGenEngine,
        ILogger<LienService> logger)
    {
        _lienRepo      = lienRepo;
        _caseRepo      = caseRepo;
        _contactRepo   = contactRepo;
        _facilityRepo  = facilityRepo;
        _audit         = audit;
        _taskGenEngine = taskGenEngine;
        _logger        = logger;
    }

    public async Task<PaginatedResult<LienResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, string? lienType,
        Guid? caseId, Guid? facilityId, int page, int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _lienRepo.SearchAsync(
            tenantId, search, status, lienType, caseId, facilityId, page, pageSize, ct);

        return new PaginatedResult<LienResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<LienResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<LienResponse?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByLienNumberAsync(tenantId, lienNumber, ct);
        return entity is null ? null : MapToResponse(entity);
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
            description: request.Description);

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

        // Fire-and-observe: task generation failure must not block lien creation
        var lienId     = entity.Id;
        var genContext  = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      Domain.Enums.TaskGenerationEventType.LienCreated,
            EntityType:     "LIEN",
            EntityId:       lienId,
            CaseId:         entity.CaseId,
            LienId:         lienId,
            WorkflowStageId: null,
            ActorUserId:    actingUserId);

        _ = _taskGenEngine.TriggerAsync(genContext, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogWarning(t.Exception, "Task generation failed for lien {LienId}.", lienId);
            }, TaskContinuationOptions.OnlyOnFaulted);

        return MapToResponse(entity);
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
            description: request.Description);

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

        return MapToResponse(entity);
    }

    public async Task<LienResponse> SetLegacyMedicalStatusAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        string status, CancellationToken ct = default)
    {
        var entity = await _lienRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Lien '{id}' not found for tenant '{tenantId}'.");

        entity.SetLegacyMedicalStatus(status.Trim(), actingUserId);

        await _lienRepo.UpdateAsync(entity, ct);

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

        return MapToResponse(entity);
    }

    private static LienResponse MapToResponse(Lien entity)
    {
        return new LienResponse
        {
            Id = entity.Id,
            LienNumber = entity.LienNumber,
            ExternalReference = entity.ExternalReference,
            LienType = entity.LienType,
            Status = entity.Status,
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
            OrgId = entity.OrgId,
            SellingOrgId = entity.SellingOrgId,
            BuyingOrgId = entity.BuyingOrgId,
            HoldingOrgId = entity.HoldingOrgId,
            IncidentDate = entity.IncidentDate,
            PurchaseDate = entity.IncidentDate?.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture),
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

        // Already terminal — treat as success (idempotent soft-delete).
        if (LienStatus.Terminal.Contains(entity.Status))
        {
            return;
        }

        // Transition to the appropriate terminal state respecting the state machine.
        // Cancelled is reachable from Draft, Sold, Active, Disputed.
        // Withdrawn is reachable from Offered, UnderReview.
        if (LienStatus.AllowedTransitions.TryGetValue(entity.Status, out var allowed) &&
            allowed.Contains(LienStatus.Cancelled))
        {
            entity.TransitionStatus(LienStatus.Cancelled, actingUserId);
        }
        else
        {
            entity.TransitionStatus(LienStatus.Withdrawn, actingUserId);
        }

        await _lienRepo.UpdateAsync(entity, ct);

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
}
