using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Application.Sources;
using Intake.Contracts.Sources;

namespace Intake.Api.Endpoints;

public static class IntakeSourceEndpoints
{
    public static IEndpointRouteBuilder MapIntakeSourceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Intake Sources")
            .RequireAuthorization(IntakeAuthorizationPolicies.SourceRead);

        readGroup.MapGet("/sources", async (
                ICurrentRequestContext requestContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                RequireTenant(requestContext),
                cancellationToken)))
            .WithSummary("List current tenant Intake sources");

        readGroup.MapGet("/sources/{sourceId:guid}", async (
                Guid sourceId,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
            ResultsOrNotFound(
                await service.GetAsync(
                    RequireTenant(requestContext),
                    sourceId,
                    cancellationToken),
                httpContext))
            .WithSummary("Get a current tenant Intake source");

        readGroup.MapGet("/sources/types", (
                IIntakeSourceTypeRegistry registry) =>
            Results.Ok(registry.Supported
                .Select(item => new IntakeSourceTypeResponse(item.Code, item.DisplayName))))
            .WithSummary("List supported Intake source types");

        readGroup.MapGet("/sources/purposes", (
                IIntakeSourcePurposeRegistry registry) =>
            Results.Ok(registry.Supported
                .Select(item => new IntakeSourcePurposeResponse(item.Code, item.DisplayName))))
            .WithSummary("List supported Intake source purposes");

        readGroup.MapGet("/sources/providers", (
                IEmailConnectorRegistry registry) =>
            Results.Ok(registry.Supported.Select(MapConnector)))
            .WithSummary("List supported Intake email providers and capabilities");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Intake Sources")
            .RequireAuthorization(IntakeAuthorizationPolicies.SourceManage);

        manageGroup.MapPost("/sources", async (
                CreateIntakeSourceRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
        {
            var response = await service.CreateAsync(
                RequireTenant(requestContext),
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken);
            return Results.Created($"/sources/{response.SourceId}", response);
        }).WithSummary("Register a tenant-owned Intake source");

        manageGroup.MapPut("/sources/{sourceId:guid}", async (
                Guid sourceId,
                UpdateIntakeSourceRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateAsync(
                RequireTenant(requestContext),
                sourceId,
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Update a tenant-owned Intake source");

        manageGroup.MapPatch("/sources/{sourceId:guid}/status", async (
                Guid sourceId,
                UpdateIntakeSourceStatusRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateStatusAsync(
                RequireTenant(requestContext),
                sourceId,
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Enable or disable a tenant-owned Intake source");

        manageGroup.MapPost("/sources/{sourceId:guid}/validate", async (
                Guid sourceId,
                ValidateIntakeSourceRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ValidateAsync(
                RequireTenant(requestContext),
                sourceId,
                request.ConfigurationVersion,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Validate a tenant-owned Intake source configuration");

        manageGroup.MapPost("/sources/{sourceId:guid}/test", async (
                Guid sourceId,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeSourceService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.TestConnectorAsync(
                RequireTenant(requestContext),
                sourceId,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Test provider configuration without retrieving mailbox content");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext requestContext) =>
        requestContext.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");

    private static IResult ResultsOrNotFound(
        IntakeSourceResponse? value,
        HttpContext context) =>
        value is null
            ? Results.NotFound(new
            {
                error = "INTAKE_SOURCE_NOT_FOUND",
                detail = "The Intake source was not found for the current tenant.",
                correlationId = context.GetCorrelationId(),
            })
            : Results.Ok(value);

    private static EmailConnectorDefinitionResponse MapConnector(
        EmailConnectorDefinition definition) =>
        new(
            definition.Code,
            definition.DisplayName,
            definition.ConfigurationOnly,
            new EmailConnectorCapabilitiesResponse(
                definition.Capabilities.SupportsPolling,
                definition.Capabilities.SupportsWebhook,
                definition.Capabilities.SupportsOAuth,
                definition.Capabilities.SupportsAttachmentRetrieval,
                definition.Capabilities.SupportsMessageIdLookup,
                definition.Capabilities.SupportsMailboxFolders));
}