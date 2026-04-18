using Reports.Application.Scheduling;
using Reports.Application.Scheduling.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/report-schedules")
            .WithTags("Report Schedules")
            .RequireAuthorization();

        group.MapPost("/", CreateSchedule)
            .WithName("CreateSchedule")
            .Produces<ReportScheduleResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{scheduleId:guid}", UpdateSchedule)
            .WithName("UpdateSchedule")
            .Produces<ReportScheduleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{scheduleId:guid}", GetSchedule)
            .WithName("GetSchedule")
            .Produces<ReportScheduleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListSchedules)
            .WithName("ListSchedules")
            .Produces<IReadOnlyList<ReportScheduleResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/{scheduleId:guid}", DeactivateSchedule)
            .WithName("DeactivateSchedule")
            .Produces<ReportScheduleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{scheduleId:guid}/runs", ListRuns)
            .WithName("ListScheduleRuns")
            .Produces<IReadOnlyList<ReportScheduleRunResponse>>(StatusCodes.Status200OK);

        group.MapGet("/runs/{runId:guid}", GetRun)
            .WithName("GetScheduleRun")
            .Produces<ReportScheduleRunResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{scheduleId:guid}/run-now", RunNow)
            .WithName("RunScheduleNow")
            .Produces<ReportScheduleRunResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateSchedule(
        CreateReportScheduleRequest request, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.CreateScheduleAsync(request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> UpdateSchedule(
        Guid scheduleId, UpdateReportScheduleRequest request, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.UpdateScheduleAsync(scheduleId, request, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetSchedule(
        Guid scheduleId, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.GetScheduleByIdAsync(scheduleId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListSchedules(
        string tenantId, string? productCode, int page, int pageSize,
        IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.ListSchedulesAsync(tenantId, productCode, page > 0 ? page : 1, pageSize > 0 ? pageSize : 50, ct);
        return ToResult(result);
    }

    private static async Task<IResult> DeactivateSchedule(
        Guid scheduleId, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.DeactivateScheduleAsync(scheduleId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> ListRuns(
        Guid scheduleId, int page, int pageSize, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.ListRunsAsync(scheduleId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20, ct);
        return ToResult(result);
    }

    private static async Task<IResult> GetRun(
        Guid runId, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.GetRunByIdAsync(runId, ct);
        return ToResult(result);
    }

    private static async Task<IResult> RunNow(
        Guid scheduleId, IReportScheduleService service, CancellationToken ct)
    {
        var result = await service.TriggerRunNowAsync(scheduleId, ct);
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
