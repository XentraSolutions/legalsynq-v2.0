using Xenia.Api.Authentication;
using Xenia.Application;

namespace Xenia.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/xenia/admin")
            .RequireAuthorization(XeniaPolicies.PlatformAdmin);

        admin.MapGet("/overview", (IXeniaService service) => Results.Ok(service.GetAdminOverview()));
        admin.MapGet("/managed-configuration", (IXeniaService service) =>
            Results.Ok(service.GetManagedConfiguration()));
        admin.MapPut("/managed-configuration", (XeniaTenantConfigurationRequest request, HttpContext context, IXeniaService service) =>
            Results.Ok(service.SaveManagedConfiguration(request, XeniaEndpointContext.ResolveActorUserId(context))));
        admin.MapGet("/providers", (IXeniaService service) => Results.Ok(service.ListProviders()));
        admin.MapPost("/providers", (XeniaProviderConfigurationRequest request, HttpContext context, IXeniaService service) =>
            Results.Created("/xenia/admin/providers", service.CreatePlatformProvider(request, XeniaEndpointContext.ResolveActorUserId(context))));
        admin.MapPut("/providers/{providerConfigurationId:guid}", (Guid providerConfigurationId, XeniaProviderConfigurationRequest request, HttpContext context, IXeniaService service) =>
            Results.Ok(service.UpdatePlatformProvider(providerConfigurationId, request, XeniaEndpointContext.ResolveActorUserId(context))));
        admin.MapPost("/providers/{providerConfigurationId:guid}/test", (Guid providerConfigurationId, HttpContext context, IXeniaService service) =>
            Results.Ok(service.TestProvider(null, providerConfigurationId, null, XeniaEndpointContext.ResolveActorUserId(context))));
        admin.MapGet("/models", (IXeniaService service) => Results.Ok(service.ListModels()));
        admin.MapGet("/prompts", (IXeniaService service) => Results.Ok(service.ListPromptTemplates()));
        admin.MapGet("/prompt-versions", (Guid? promptTemplateId, IXeniaService service) => Results.Ok(service.ListPromptVersions(promptTemplateId)));
        admin.MapGet("/skills", (IXeniaService service) => Results.Ok(service.ListSkills()));
        admin.MapGet("/skill-versions", (Guid? skillId, IXeniaService service) => Results.Ok(service.ListSkillVersions(skillId)));
        admin.MapGet("/agents", (IXeniaService service) => Results.Ok(service.ListAgents()));
        admin.MapGet("/agent-versions", (Guid? agentId, IXeniaService service) => Results.Ok(service.ListAgentVersions(agentId)));
        admin.MapGet("/knowledge-sources", (Guid? tenantId, IXeniaService service) => Results.Ok(service.ListKnowledgeSources(tenantId)));
        admin.MapGet("/marketplace/assets", (IXeniaService service) => Results.Ok(service.ListMarketplaceAssets()));
        admin.MapGet("/marketplace/installations", (Guid? tenantId, IXeniaService service) => Results.Ok(service.ListMarketplaceInstallations(tenantId)));
        admin.MapGet("/usage", (IXeniaService service) => Results.Ok(service.GetUsage()));
        admin.MapGet("/audit", (IXeniaService service) => Results.Ok(service.GetAudit()));
        admin.MapGet("/health/providers", (IXeniaService service) => Results.Ok(service.GetProviderHealth()));

        return routes;
    }
}
