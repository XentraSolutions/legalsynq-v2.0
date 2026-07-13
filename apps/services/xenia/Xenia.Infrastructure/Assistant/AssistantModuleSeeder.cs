using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xenia.Application.Assistant;
using Xenia.Application.Modules;
using Xenia.Domain.Assistant;
using Xenia.Domain.Modules;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Assistant;

internal sealed class AssistantModuleSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AssistantModuleSeeder> _logger;

    public AssistantModuleSeeder(IServiceProvider services, ILogger<AssistantModuleSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

        var module = await db.Modules.FirstOrDefaultAsync(
            m => m.ModuleKey == AssistantModuleKeys.ModuleKey,
            cancellationToken);

        if (module is null)
        {
            module = new XeniaModule(
                Guid.CreateVersion7(),
                AssistantModuleKeys.ModuleKey,
                "Xenia Assistant",
                "1.0.0",
                "Tenant-aware AI assistant, agent, and tool orchestration module.",
                AssistantModuleKeys.ConfigurationNamespace);
            module.Enable();
            module.UpdateStatus(ModuleStatus.Healthy);
            db.Modules.Add(module);
        }
        else
        {
            module.Enable();
            module.UpdateStatus(ModuleStatus.Healthy);
        }

        await UpsertAgentAsync(
            db,
            AssistantModuleKeys.GenericAgentKey,
            "Generic Assistant",
            "General LegalSynq assistant for product-neutral questions and drafting.",
            "1.0.0",
            "You are Xenia, LegalSynq's tenant-aware assistant. Answer concisely, avoid exposing secrets, and use only authorized context.",
            """["tenant.context.summary","careconnect.referral.lookup"]""",
            "[]",
            cancellationToken);

        await UpsertAgentAsync(
            db,
            AssistantModuleKeys.LiensAgentKey,
            "SynqLien Agent",
            "Read-only SynqLien assistant for lien lifecycle context.",
            "1.0.0",
            "You are Xenia's SynqLien agent. Use authorized lien context only and cite product records when available.",
            """["tenant.context.summary","synqlien.record.lookup"]""",
            """["SynqLien"]""",
            cancellationToken);

        await UpsertAgentAsync(
            db,
            AssistantModuleKeys.CareConnectAgentKey,
            "CareConnect Agent",
            "Read-only CareConnect assistant for referral and provider workflow context.",
            "1.0.0",
            "You are Xenia's CareConnect agent. Use authorized referral/provider context only and cite product records when available.",
            """["tenant.context.summary","careconnect.referral.lookup"]""",
            """["CareConnect"]""",
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Xenia assistant module and default agents seeded.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task UpsertAgentAsync(
        XeniaDbContext db,
        string agentKey,
        string name,
        string description,
        string version,
        string systemPrompt,
        string allowedToolsJson,
        string requiredProductCodesJson,
        CancellationToken ct)
    {
        var existing = await db.AssistantAgents.FirstOrDefaultAsync(a => a.AgentKey == agentKey, ct);
        if (existing is null)
        {
            db.AssistantAgents.Add(new AssistantAgent(
                Guid.CreateVersion7(),
                agentKey,
                name,
                description,
                version,
                systemPrompt,
                allowedToolsJson,
                requiredProductCodesJson,
                isEnabled: true));
            return;
        }

        existing.UpdateDefinition(name, description, version, systemPrompt, allowedToolsJson, requiredProductCodesJson);
        existing.Enable();
    }
}
