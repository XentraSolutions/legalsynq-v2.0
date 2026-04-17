using Reports.Application.Templates.DTOs;
using Reports.Application.Views;
using Reports.Application.Views.DTOs;

namespace Reports.Api.Endpoints;

public static class ViewEndpoints
{
    public static void MapViewEndpoints(this IEndpointRouteBuilder routes)
    {
        var viewGroup = routes.MapGroup("/api/v1/tenant-templates/{templateId:guid}/views")
            .WithTags("Tenant Report Views")
            .RequireAuthorization();

        viewGroup.MapPost("/", CreateView)
            .WithName("CreateTenantReportView")
            .Produces<TenantReportViewResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        viewGroup.MapPut("/{viewId:guid}", UpdateView)
            .WithName("UpdateTenantReportView")
            .Produces<TenantReportViewResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        viewGroup.MapGet("/{viewId:guid}", GetViewById)
            .WithName("GetTenantReportView")
            .Produces<TenantReportViewResponse>()
            .Produces(StatusCodes.Status404NotFound);

        viewGroup.MapGet("/", ListViews)
            .WithName("ListTenantReportViews")
            .Produces<IReadOnlyList<TenantReportViewResponse>>();

        viewGroup.MapDelete("/{viewId:guid}", DeleteView)
            .WithName("DeleteTenantReportView")
            .Produces<TenantReportViewResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateView(
        Guid templateId,
        CreateTenantReportViewRequest request,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.CreateViewAsync(templateId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateView(
        Guid templateId,
        Guid viewId,
        UpdateTenantReportViewRequest request,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.UpdateViewAsync(templateId, viewId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetViewById(
        Guid templateId,
        Guid viewId,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.GetViewByIdAsync(templateId, viewId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListViews(
        Guid templateId,
        ITenantReportViewService service,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await service.ListViewsAsync(templateId, tenantId ?? string.Empty, ct);
        return ToResult(result);
    }

    private static async Task<IResult> DeleteView(
        Guid templateId,
        Guid viewId,
        ITenantReportViewService service,
        CancellationToken ct)
    {
        var result = await service.DeleteViewAsync(templateId, viewId, ct);
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
