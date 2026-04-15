using Reports.Application.Export;
using Reports.Application.Export.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/report-exports")
            .WithTags("Report Exports");

        group.MapPost("/", ExportReport)
            .WithName("ExportReport")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> ExportReport(
        ExportReportRequest request,
        IReportExportService service,
        CancellationToken ct)
    {
        var result = await service.ExportReportAsync(request, ct);

        if (!result.Success)
        {
            var error = new { error = result.ErrorMessage };
            return result.StatusCode switch
            {
                400 => Results.BadRequest(error),
                404 => Results.NotFound(error),
                409 => Results.Conflict(error),
                _ => Results.Json(error, statusCode: result.StatusCode)
            };
        }

        var data = result.Data!;
        return Results.File(
            data.FileContent,
            data.ContentType,
            data.FileName);
    }
}
