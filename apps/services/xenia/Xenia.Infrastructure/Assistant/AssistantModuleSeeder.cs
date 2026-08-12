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
            "1.4.0",
            "You are Xenia, LegalSynq's tenant-aware assistant. Answer concisely, avoid exposing secrets, and use authorized product tools whenever grounded lookup, KPI counts, or queue summaries are needed.",
            """["tenant.context.summary","careconnect.referral.lookup","careconnect.referral.history.lookup","careconnect.referral.search","careconnect.provider.search","careconnect.referrer.search","careconnect.referral.queue.summary","synqlien.lien.lookup","synqlien.lien.search","synqlien.lien.queue.summary","synqlien.case.lookup","synqlien.case.insights","synqlien.case.search","synqlien.task.search","synqlien.servicing.search","synqlien.report.summary"]""",
            "[]",
            cancellationToken);

        await UpsertAgentAsync(
            db,
            AssistantModuleKeys.LiensAgentKey,
            "SynqLien Agent",
            "Read-only SynqLien assistant for lien, case, financial, document, task, servicing, reporting, and KPI workflow context.",
            "1.2.0",
            "You are Xenia's SynqLien agent. Use authorized lien, case insight, task, servicing, and report tools proactively for grounded answers. Stay within tenant-visible SynqLien data, cite product records when available, and clearly state when a requested capability only has metadata or Excel-ready payload support.",
            """["tenant.context.summary","synqlien.lien.lookup","synqlien.lien.search","synqlien.lien.queue.summary","synqlien.case.lookup","synqlien.case.insights","synqlien.case.search","synqlien.task.search","synqlien.servicing.search","synqlien.report.summary"]""",
            """["SynqLien"]""",
            cancellationToken);

        await UpsertAgentAsync(
            db,
            AssistantModuleKeys.CareConnectAgentKey,
            "CareConnect Agent",
            "Read-only CareConnect assistant for referral, provider, and KPI workflow context.",
            "1.2.0",
            "You are Xenia's CareConnect agent. Use authorized referral, provider, and KPI summary tools proactively, stay within tenant-visible CareConnect data, and cite product records when available.",
            """["tenant.context.summary","careconnect.referral.lookup","careconnect.referral.history.lookup","careconnect.referral.search","careconnect.provider.search","careconnect.referrer.search","careconnect.referral.queue.summary"]""",
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
