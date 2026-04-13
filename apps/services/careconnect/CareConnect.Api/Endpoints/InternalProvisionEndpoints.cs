using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Api.Endpoints;

public static class InternalProvisionEndpoints
{
    private const string InternalTokenHeader = "X-Internal-Service-Token";
    private const string ExpectedToken = "legalsynq-internal-service-2024";

    public static IEndpointRouteBuilder MapInternalProvisionEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/internal/provision-provider", ProvisionProvider)
            .AllowAnonymous();

        return routes;
    }

    private static async Task<IResult> ProvisionProvider(
        HttpContext httpContext,
        ProvisionProviderRequest body,
        IProviderRepository providers,
        CancellationToken ct)
    {
        var token = httpContext.Request.Headers[InternalTokenHeader].FirstOrDefault();
        var configToken = httpContext.RequestServices
            .GetService<IConfiguration>()?["InternalServiceToken"] ?? ExpectedToken;
        if (string.IsNullOrEmpty(token) || token != configToken)
            return Results.Unauthorized();
        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "tenantId is required." });
        if (body.OrganizationId == Guid.Empty)
            return Results.BadRequest(new { error = "organizationId is required." });
        if (string.IsNullOrWhiteSpace(body.ProviderName))
            return Results.BadRequest(new { error = "providerName is required." });

        var existing = await providers.GetByOrganizationIdAsync(body.OrganizationId, ct);

        if (existing is not null)
        {
            if (!existing.IsActive || !existing.AcceptingReferrals)
            {
                existing.Activate();
                await providers.UpdateAsync(existing, ct);
            }

            return Results.Ok(new ProvisionProviderResponse(existing.Id, IsNew: false));
        }

        var provider = Provider.Create(
            tenantId: body.TenantId,
            name: body.ProviderName.Trim(),
            organizationName: body.ProviderName.Trim(),
            email: "",
            phone: "",
            addressLine1: "",
            city: "",
            state: "",
            postalCode: "",
            isActive: true,
            acceptingReferrals: true,
            createdByUserId: null);

        provider.LinkOrganization(body.OrganizationId);
        await providers.AddAsync(provider, ct);

        return Results.Ok(new ProvisionProviderResponse(provider.Id, IsNew: true));
    }
}

public sealed record ProvisionProviderRequest(
    Guid TenantId,
    Guid OrganizationId,
    string ProviderName);

public sealed record ProvisionProviderResponse(
    Guid ProviderId,
    bool IsNew);
