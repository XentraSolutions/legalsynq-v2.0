using Reports.Application.Overrides;
using Reports.Application.Overrides.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class OverrideEndpoints
{
    public static void MapOverrideEndpoints(this IEndpointRouteBuilder routes)
    {
        var overrideGroup = routes.MapGroup("/api/v1/tenant-templates/{templateId:guid}/overrides")
            .WithTags("Tenant Report Overrides")
            .RequireAuthorization();

        overrideGroup.MapPost("/", CreateOverride)
            .WithName("CreateTenantReportOverride")
            .Produces<TenantReportOverrideResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        overrideGroup.MapPut("/{overrideId:guid}", UpdateOverride)
            .WithName("UpdateTenantReportOverride")
            .Produces<TenantReportOverrideResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        overrideGroup.MapGet("/{overrideId:guid}", GetOverrideById)
            .WithName("GetTenantReportOverride")
            .Produces<TenantReportOverrideResponse>()
            .Produces(StatusCodes.Status404NotFound);

        overrideGroup.MapGet("/", ListOverrides)
            .WithName("ListTenantReportOverrides")
            .Produces<IReadOnlyList<TenantReportOverrideResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        overrideGroup.MapDelete("/{overrideId:guid}", DeactivateOverride)
            .WithName("DeactivateTenantReportOverride")
            .Produces<TenantReportOverrideResponse>()
            .Produces(StatusCodes.Status404NotFound);

        var effectiveGroup = routes.MapGroup("/api/v1/tenant-templates/{templateId:guid}")
            .WithTags("Tenant Effective Report")
            .RequireAuthorization();

        effectiveGroup.MapGet("/effective", ResolveEffectiveReport)
            .WithName("ResolveEffectiveReport")
            .Produces<TenantEffectiveReportResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateOverride(
        Guid templateId,
        CreateTenantReportOverrideRequest request,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.CreateOverrideAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateOverride(
        Guid templateId,
        Guid overrideId,
        UpdateTenantReportOverrideRequest request,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.UpdateOverrideAsync(templateId, overrideId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetOverrideById(
        Guid templateId,
        Guid overrideId,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.GetOverrideByIdAsync(templateId, overrideId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListOverrides(
        Guid templateId,
        ITenantReportOverrideService service,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await service.ListOverridesAsync(templateId, tenantId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> DeactivateOverride(
        Guid templateId,
        Guid overrideId,
        ITenantReportOverrideService service,
        CancellationToken ct)
    {
        var result = await service.DeactivateOverrideAsync(templateId, overrideId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ResolveEffectiveReport(
        Guid templateId,
        ITenantReportOverrideService service,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await service.ResolveEffectiveReportAsync(templateId, tenantId ?? string.Empty, ct);
        return ToResult(result);
    }

    private static IResult ToResult<T>(ServiceResult<T> result)
    {
        if (result.Success)
        {
            return result.StatusCode == 201
                ? Results.Created((string?)null, result.Data)
                : Results.Ok(result.Data);
        }

        var error = new { error = result.ErrorMessage };
        return result.StatusCode switch
        {
            400 => Results.BadRequest(error),
            404 => Results.NotFound(error),
            409 => Results.Conflict(error),
            _ => Results.Json(error, statusCode: result.StatusCode)
        };
    }
}
