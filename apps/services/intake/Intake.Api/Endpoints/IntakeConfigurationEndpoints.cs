using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Middleware;
using Intake.Application.Configuration;
using Intake.Contracts.Configuration;

namespace Intake.Api.Endpoints;

public static class IntakeConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapIntakeConfigurationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var readGroup = app.MapGroup(string.Empty)
            .WithTags("Intake Configuration")
            .RequireAuthorization(IntakeAuthorizationPolicies.ConfigurationRead);

        readGroup.MapGet("/processing-profiles", async (
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAvailableProfilesAsync(cancellationToken)))
            .WithSummary("List active Intake processing profile definitions");

        readGroup.MapGet("/configuration", async (
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            ResultsOrNotFound(
                await service.GetConfigurationAsync(
                    RequireTenant(requestContext),
                    cancellationToken),
                "TENANT_CONFIGURATION_NOT_FOUND",
                "No Intake configuration exists for the current tenant.",
                httpContext))
            .WithSummary("Get the current tenant Intake configuration");

        readGroup.MapGet("/configuration/processing-profiles", async (
                ICurrentRequestContext requestContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListTenantProfilesAsync(
                RequireTenant(requestContext),
                cancellationToken)))
            .WithSummary("List current tenant Intake profile assignments");

        readGroup.MapGet("/configuration/processing-profiles/{profileCode}", async (
                string profileCode,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            ResultsOrNotFound(
                await service.GetTenantProfileAsync(
                    RequireTenant(requestContext),
                    profileCode,
                    cancellationToken),
                "PROFILE_ASSIGNMENT_NOT_FOUND",
                $"Tenant has no assignment for '{profileCode}'.",
                httpContext))
            .WithSummary("Get a tenant Intake profile assignment");

        var manageGroup = app.MapGroup(string.Empty)
            .WithTags("Intake Configuration")
            .RequireAuthorization(IntakeAuthorizationPolicies.ConfigurationManage);

        manageGroup.MapPut("/configuration", async (
                UpsertTenantIntakeConfigurationRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpsertConfigurationAsync(
                RequireTenant(requestContext),
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Create or update the current tenant Intake configuration");

        manageGroup.MapPost("/configuration/processing-profiles", async (
                AssignTenantProcessingProfileRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
        {
            var response = await service.AssignProfileAsync(
                RequireTenant(requestContext),
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken);
            return Results.Created(
                $"/configuration/processing-profiles/{response.ProcessingProfileCode}",
                response);
        }).WithSummary("Assign an Intake processing profile to the current tenant");

        manageGroup.MapPut("/configuration/processing-profiles/{profileCode}", async (
                string profileCode,
                UpdateTenantProcessingProfileRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateTenantProfileAsync(
                RequireTenant(requestContext),
                profileCode,
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Update a tenant Intake profile configuration");

        manageGroup.MapPatch("/configuration/processing-profiles/{profileCode}/status", async (
                string profileCode,
                UpdateTenantProcessingProfileStatusRequest request,
                ICurrentRequestContext requestContext,
                HttpContext httpContext,
                IIntakeConfigurationService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateTenantProfileStatusAsync(
                RequireTenant(requestContext),
                profileCode,
                request,
                requestContext.UserId,
                httpContext.GetCorrelationId(),
                cancellationToken)))
            .WithSummary("Enable or disable a tenant Intake profile");

        return app;
    }

    private static Guid RequireTenant(ICurrentRequestContext requestContext) =>
        requestContext.TenantId ?? throw IntakeConfigurationException.Forbidden(
            "TENANT_CONTEXT_REQUIRED",
            "An authenticated tenant context is required for this operation.");

    private static IResult ResultsOrNotFound<T>(
        T? value,
        string error,
        string detail,
        HttpContext context)
        where T : class =>
        value is null
            ? Results.NotFound(new
            {
                error,
                detail,
                correlationId = context.GetCorrelationId(),
            })
            : Results.Ok(value);
}