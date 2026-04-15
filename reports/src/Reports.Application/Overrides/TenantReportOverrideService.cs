using Microsoft.Extensions.Logging;
using Reports.Application.Overrides.DTOs;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Overrides;

public sealed class TenantReportOverrideService : ITenantReportOverrideService
{
    private readonly ITenantReportOverrideRepository _overrideRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateAssignmentRepository _assignmentRepo;
    private readonly IAuditAdapter _audit;
    private readonly ILogger<TenantReportOverrideService> _log;

    public TenantReportOverrideService(
        ITenantReportOverrideRepository overrideRepo,
        ITemplateRepository templateRepo,
        ITemplateAssignmentRepository assignmentRepo,
        IAuditAdapter audit,
        ILogger<TenantReportOverrideService> log)
    {
        _overrideRepo = overrideRepo;
        _templateRepo = templateRepo;
        _assignmentRepo = assignmentRepo;
        _audit = audit;
        _log = log;
    }

    public async Task<ServiceResult<TenantReportOverrideResponse>> CreateOverrideAsync(
        Guid templateId, CreateTenantReportOverrideRequest request, CancellationToken ct)
    {
        var validation = ValidateCreateRequest(request);
        if (validation is not null)
            return ServiceResult<TenantReportOverrideResponse>.BadRequest(validation);

        var tenantId = request.TenantId.Trim();

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TenantReportOverrideResponse>.NotFound($"Template '{templateId}' not found.");

        var publishedVersion = await _templateRepo.GetPublishedVersionAsync(templateId, ct);
        if (publishedVersion is null)
            return ServiceResult<TenantReportOverrideResponse>.BadRequest(
                $"Template '{templateId}' has no published version. A published version is required to create an override.");

        var isAssigned = await IsTenantAssignedAsync(templateId, tenantId, ct);
        if (!isAssigned)
            return ServiceResult<TenantReportOverrideResponse>.BadRequest(
                $"Template '{templateId}' is not assigned to tenant '{tenantId}'. Assignment is required before creating an override.");

        if (request.IsActive)
        {
            var hasActive = await _overrideRepo.HasActiveOverrideAsync(tenantId, templateId, null, ct);
            if (hasActive)
                return ServiceResult<TenantReportOverrideResponse>.Conflict(
                    $"An active override already exists for tenant '{tenantId}' and template '{templateId}'.");
        }

        var entity = new TenantReportOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportTemplateId = templateId,
            BaseTemplateVersionNumber = publishedVersion.VersionNumber,
            NameOverride = request.NameOverride?.Trim(),
            DescriptionOverride = request.DescriptionOverride?.Trim(),
            LayoutConfigJson = request.LayoutConfigJson,
            ColumnConfigJson = request.ColumnConfigJson,
            FilterConfigJson = request.FilterConfigJson,
            FormulaConfigJson = request.FormulaConfigJson,
            HeaderConfigJson = request.HeaderConfigJson,
            FooterConfigJson = request.FooterConfigJson,
            IsActive = request.IsActive,
            RequiredFeatureCode = request.RequiredFeatureCode?.Trim(),
            MinimumTierCode = request.MinimumTierCode?.Trim(),
            CreatedByUserId = request.CreatedByUserId.Trim()
        };

        TenantReportOverride created;
        try
        {
            created = await _overrideRepo.CreateAsync(entity, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicting", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<TenantReportOverrideResponse>.Conflict(ex.Message);
        }

        await TryAuditAsync("tenant.override.created",
            $"Override '{created.Id}' created for tenant '{tenantId}' template '{templateId}' (base version: {created.BaseTemplateVersionNumber})");

        _log.LogInformation("Override created: {OverrideId} tenant={TenantId} template={TemplateId} baseVersion={BaseVersion}",
            created.Id, tenantId, templateId, created.BaseTemplateVersionNumber);

        return ServiceResult<TenantReportOverrideResponse>.Created(MapToResponse(created));
    }

    public async Task<ServiceResult<TenantReportOverrideResponse>> UpdateOverrideAsync(
        Guid templateId, Guid overrideId, UpdateTenantReportOverrideRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UpdatedByUserId))
            return ServiceResult<TenantReportOverrideResponse>.BadRequest("UpdatedByUserId is required.");

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TenantReportOverrideResponse>.NotFound($"Template '{templateId}' not found.");

        var existing = await _overrideRepo.GetByIdAsync(overrideId, ct);
        if (existing is null || existing.ReportTemplateId != templateId)
            return ServiceResult<TenantReportOverrideResponse>.NotFound(
                $"Override '{overrideId}' not found for template '{templateId}'.");

        var isAssigned = await IsTenantAssignedAsync(templateId, existing.TenantId, ct);
        if (!isAssigned)
            return ServiceResult<TenantReportOverrideResponse>.BadRequest(
                $"Template '{templateId}' is no longer assigned to tenant '{existing.TenantId}'.");

        if (request.IsActive && !existing.IsActive)
        {
            var hasActive = await _overrideRepo.HasActiveOverrideAsync(existing.TenantId, templateId, overrideId, ct);
            if (hasActive)
                return ServiceResult<TenantReportOverrideResponse>.Conflict(
                    $"An active override already exists for tenant '{existing.TenantId}' and template '{templateId}'.");
        }

        existing.NameOverride = request.NameOverride?.Trim();
        existing.DescriptionOverride = request.DescriptionOverride?.Trim();
        existing.LayoutConfigJson = request.LayoutConfigJson;
        existing.ColumnConfigJson = request.ColumnConfigJson;
        existing.FilterConfigJson = request.FilterConfigJson;
        existing.FormulaConfigJson = request.FormulaConfigJson;
        existing.HeaderConfigJson = request.HeaderConfigJson;
        existing.FooterConfigJson = request.FooterConfigJson;
        existing.IsActive = request.IsActive;
        existing.RequiredFeatureCode = request.RequiredFeatureCode?.Trim();
        existing.MinimumTierCode = request.MinimumTierCode?.Trim();
        existing.UpdatedByUserId = request.UpdatedByUserId.Trim();

        TenantReportOverride updated;
        try
        {
            updated = await _overrideRepo.UpdateAsync(existing, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicting", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<TenantReportOverrideResponse>.Conflict(ex.Message);
        }

        await TryAuditAsync("tenant.override.updated",
            $"Override '{updated.Id}' updated for tenant '{updated.TenantId}' template '{templateId}'");

        _log.LogInformation("Override updated: {OverrideId} tenant={TenantId} template={TemplateId}",
            updated.Id, updated.TenantId, templateId);

        return ServiceResult<TenantReportOverrideResponse>.Ok(MapToResponse(updated));
    }

    public async Task<ServiceResult<TenantReportOverrideResponse>> GetOverrideByIdAsync(
        Guid templateId, Guid overrideId, CancellationToken ct)
    {
        var existing = await _overrideRepo.GetByIdAsync(overrideId, ct);
        if (existing is null || existing.ReportTemplateId != templateId)
            return ServiceResult<TenantReportOverrideResponse>.NotFound(
                $"Override '{overrideId}' not found for template '{templateId}'.");

        return ServiceResult<TenantReportOverrideResponse>.Ok(MapToResponse(existing));
    }

    public async Task<ServiceResult<IReadOnlyList<TenantReportOverrideResponse>>> ListOverridesAsync(
        Guid templateId, string? tenantId, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<IReadOnlyList<TenantReportOverrideResponse>>.NotFound(
                $"Template '{templateId}' not found.");

        var overrides = await _overrideRepo.ListByTemplateAsync(templateId, tenantId?.Trim(), ct);
        var responses = overrides.Select(MapToResponse).ToList().AsReadOnly();
        return ServiceResult<IReadOnlyList<TenantReportOverrideResponse>>.Ok(responses);
    }

    public async Task<ServiceResult<TenantReportOverrideResponse>> DeactivateOverrideAsync(
        Guid templateId, Guid overrideId, CancellationToken ct)
    {
        var existing = await _overrideRepo.GetByIdAsync(overrideId, ct);
        if (existing is null || existing.ReportTemplateId != templateId)
            return ServiceResult<TenantReportOverrideResponse>.NotFound(
                $"Override '{overrideId}' not found for template '{templateId}'.");

        existing.IsActive = false;

        var updated = await _overrideRepo.UpdateAsync(existing, ct);

        await TryAuditAsync("tenant.override.deactivated",
            $"Override '{updated.Id}' deactivated for tenant '{updated.TenantId}' template '{templateId}'");

        _log.LogInformation("Override deactivated: {OverrideId} tenant={TenantId} template={TemplateId}",
            updated.Id, updated.TenantId, templateId);

        return ServiceResult<TenantReportOverrideResponse>.Ok(MapToResponse(updated));
    }

    public async Task<ServiceResult<TenantEffectiveReportResponse>> ResolveEffectiveReportAsync(
        Guid templateId, string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<TenantEffectiveReportResponse>.BadRequest("TenantId is required.");

        tenantId = tenantId.Trim();

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<TenantEffectiveReportResponse>.NotFound($"Template '{templateId}' not found.");

        var isAssigned = await IsTenantAssignedAsync(templateId, tenantId, ct);
        if (!isAssigned)
            return ServiceResult<TenantEffectiveReportResponse>.BadRequest(
                $"Template '{templateId}' is not assigned to tenant '{tenantId}'.");

        var publishedVersion = await _templateRepo.GetPublishedVersionAsync(templateId, ct);
        if (publishedVersion is null)
            return ServiceResult<TenantEffectiveReportResponse>.BadRequest(
                $"Template '{templateId}' has no published version.");

        var activeOverride = await _overrideRepo.GetByTenantAndTemplateAsync(tenantId, templateId, ct);

        var response = new TenantEffectiveReportResponse
        {
            TemplateId = template.Id,
            TenantId = tenantId,
            TemplateCode = template.Code,
            ProductCode = template.ProductCode,
            OrganizationType = template.OrganizationType,
            PublishedVersionNumber = publishedVersion.VersionNumber,
            IsActive = template.IsActive,
            HasOverride = activeOverride is not null,
            OverrideId = activeOverride?.Id,
            BaseTemplateVersionNumber = activeOverride?.BaseTemplateVersionNumber,
            EffectiveName = activeOverride?.NameOverride ?? template.Name,
            EffectiveDescription = activeOverride?.DescriptionOverride ?? template.Description,
            EffectiveLayoutConfigJson = activeOverride?.LayoutConfigJson,
            EffectiveColumnConfigJson = activeOverride?.ColumnConfigJson,
            EffectiveFilterConfigJson = activeOverride?.FilterConfigJson,
            EffectiveFormulaConfigJson = activeOverride?.FormulaConfigJson,
            EffectiveHeaderConfigJson = activeOverride?.HeaderConfigJson,
            EffectiveFooterConfigJson = activeOverride?.FooterConfigJson,
            RequiredFeatureCode = activeOverride?.RequiredFeatureCode,
            MinimumTierCode = activeOverride?.MinimumTierCode
        };

        await TryAuditAsync("tenant.effective.report.resolved",
            $"Effective report resolved for tenant '{tenantId}' template '{templateId}' (hasOverride: {response.HasOverride})");

        return ServiceResult<TenantEffectiveReportResponse>.Ok(response);
    }

    private async Task<bool> IsTenantAssignedAsync(Guid templateId, string tenantId, CancellationToken ct)
    {
        var hasGlobal = await _assignmentRepo.HasActiveGlobalAssignmentAsync(templateId, null, ct);
        if (hasGlobal) return true;

        var hasTenant = await _assignmentRepo.HasActiveTenantAssignmentAsync(templateId, tenantId, null, ct);
        return hasTenant;
    }

    private static string? ValidateCreateRequest(CreateTenantReportOverrideRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return "TenantId is required.";
        if (string.IsNullOrWhiteSpace(request.CreatedByUserId))
            return "CreatedByUserId is required.";
        return null;
    }

    private static TenantReportOverrideResponse MapToResponse(TenantReportOverride entity) => new()
    {
        OverrideId = entity.Id,
        TenantId = entity.TenantId,
        TemplateId = entity.ReportTemplateId,
        BaseTemplateVersionNumber = entity.BaseTemplateVersionNumber,
        NameOverride = entity.NameOverride,
        DescriptionOverride = entity.DescriptionOverride,
        LayoutConfigJson = entity.LayoutConfigJson,
        ColumnConfigJson = entity.ColumnConfigJson,
        FilterConfigJson = entity.FilterConfigJson,
        FormulaConfigJson = entity.FormulaConfigJson,
        HeaderConfigJson = entity.HeaderConfigJson,
        FooterConfigJson = entity.FooterConfigJson,
        IsActive = entity.IsActive,
        RequiredFeatureCode = entity.RequiredFeatureCode,
        MinimumTierCode = entity.MinimumTierCode,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
    };

    private async Task TryAuditAsync(string action, string description)
    {
        try
        {
            var ctx = RequestContext.Default();
            var tenant = new TenantContext { TenantId = "system", IsActive = true };
            await _audit.RecordEventAsync(ctx, tenant, "system", action, description);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit hook failed for action {Action}", action);
        }
    }
}
