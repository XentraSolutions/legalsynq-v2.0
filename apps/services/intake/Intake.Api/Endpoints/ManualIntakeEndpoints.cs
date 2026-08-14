using System.Globalization;
using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Manual;
using Intake.Application.Artifacts;

namespace Intake.Api.Endpoints;

public static class ManualIntakeEndpoints
{
    public static IEndpointRouteBuilder MapManualIntakeEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Manual Intake")
            .RequireAuthorization(IntakeAuthorizationPolicies.ManualRead);

        readGroup.MapGet("/manual-intake", async (
                [AsParameters] ManualIntakeListQuery query,
                ICurrentRequestContext context,
                IManualIntakeService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(context),
                query,
                cancellationToken)))
            .WithSummary("List manual Intake submissions for the current tenant");

        readGroup.MapGet("/manual-intake/{submissionId:guid}", async (
                Guid submissionId,
                ICurrentRequestContext context,
                IManualIntakeService service,
                CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(
                RequireTenant(context),
                submissionId,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithSummary("Get a manual Intake submission and its artifacts");

        readGroup.MapGet("/manual-intake/analytics", async (
                ICurrentRequestContext context,
                IManualIntakeService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAnalyticsAsync(
                RequireTenant(context),
                cancellationToken)))
            .WithSummary("Get manual Intake analytics for the current tenant");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Manual Intake")
            .RequireAuthorization(IntakeAuthorizationPolicies.ManualManage);

        manageGroup.MapPost("/manual-intake", async (
                HttpRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IManualIntakeService service,
                EmailArtifactProcessingOptions options,
                CancellationToken cancellationToken) =>
        {
            var form = await ReadFormAsync(request, cancellationToken);
            var files = await ReadFilesAsync(form, options, cancellationToken);
            var result = await service.CreateAndSubmitAsync(
                RequireTenant(context),
                context.OrgId,
                context.UserId,
                httpContext.GetCorrelationId(),
                new CreateManualIntakeRequest
                {
                    Purpose = Field(form, "purpose") ?? string.Empty,
                    ProcessingProfileCode = Field(form, "processingProfileCode"),
                    Title = Field(form, "title"),
                    ExternalReference = Field(form, "externalReference"),
                    Notes = Field(form, "notes"),
                    ClientRequestId = Field(form, "clientRequestId") ??
                                      request.Headers["Idempotency-Key"].FirstOrDefault(),
                    Files = files,
                },
                cancellationToken);
            return Results.Created($"/manual-intake/{result.Id}", result);
        }).WithSummary("Create and process a manual Intake submission");

        manageGroup.MapPost("/manual-intake/{submissionId:guid}/artifacts/{artifactId:guid}/retry", async (
                Guid submissionId,
                Guid artifactId,
                HttpRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IManualIntakeService service,
                EmailArtifactProcessingOptions options,
                CancellationToken cancellationToken) =>
        {
            var form = await ReadFormAsync(request, cancellationToken);
            var files = await ReadFilesAsync(form, options, cancellationToken);
            if (files.Count != 1)
                throw IntakeConfigurationException.BadRequest(
                    "MANUAL_RETRY_FILE_REQUIRED",
                    "Retrying a manual artifact requires exactly one replacement file.");
            return Results.Ok(await service.RetryArtifactAsync(
                RequireTenant(context),
                submissionId,
                artifactId,
                files[0],
                context.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken));
        }).WithSummary("Retry one failed manual Intake artifact");

        manageGroup.MapPost("/manual-intake/{submissionId:guid}/cancel", async (
                Guid submissionId,
                ManualCancelRequest request,
                ICurrentRequestContext context,
                HttpContext httpContext,
                IManualIntakeService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.CancelAsync(
                RequireTenant(context),
                submissionId,
                request.Version,
                context.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Cancel a manual Intake submission");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");

    private static string? Field(IFormCollection form, string name) =>
        form.TryGetValue(name, out var value) ? value.FirstOrDefault() : null;

    private static async Task<IFormCollection> ReadFormAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            throw IntakeConfigurationException.BadRequest(
                "MANUAL_MULTIPART_REQUIRED",
                "Manual Intake requests must use multipart/form-data.");
        return await request.ReadFormAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ManualIntakeFile>> ReadFilesAsync(
        IFormCollection form,
        EmailArtifactProcessingOptions options,
        CancellationToken cancellationToken)
    {
        var maxFiles = options.MaxManualFiles > 0 ? options.MaxManualFiles : options.MaxArtifactsPerEmail;
        var maxFileBytes = options.MaxManualFileBytes > 0 ? options.MaxManualFileBytes : options.MaxArtifactBytes;
        var maxTotalBytes = options.MaxTotalManualFileBytes > 0
            ? options.MaxTotalManualFileBytes
            : options.MaxTotalArtifactBytesPerEmail;
        if (form.Files.Count > maxFiles)
            throw IntakeConfigurationException.BadRequest(
                "ARTIFACT_COUNT_EXCEEDED",
                $"A manual submission may contain at most {maxFiles} files.");
        if (form.Files.Any(file => file.Length > maxFileBytes) ||
            form.Files.Sum(file => file.Length) > maxTotalBytes)
            throw IntakeConfigurationException.BadRequest(
                "ARTIFACT_BYTES_EXCEEDED",
                "The manual submission exceeds the configured file size limit.");

        var files = new List<ManualIntakeFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            await using var input = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);
            files.Add(new(
                file.FileName,
                file.ContentType,
                buffer.ToArray()));
        }
        return files;
    }

    private sealed class ManualCancelRequest
    {
        public int Version { get; set; }
    }
}