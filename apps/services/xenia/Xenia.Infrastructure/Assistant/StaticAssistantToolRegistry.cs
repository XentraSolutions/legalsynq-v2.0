using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class StaticAssistantToolRegistry : IAssistantToolRegistry
{
    private static readonly IReadOnlyList<AssistantToolDefinitionDto> Tools =
    [
        new(
            ToolKey: "tenant.context.summary",
            Name: "Tenant context summary",
            Description: "Summarizes the current tenant and product context supplied by Xenia.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"context":{"type":"object"}}}""",
            RequiredPermissions: [],
            RequiredProductCodes: [],
            ConfirmationRequired: false,
            MaxOutputCharacters: 2000),
        new(
            ToolKey: "synqlien.record.lookup",
            Name: "SynqLien record lookup",
            Description: "Placeholder read-only lookup contract for authorized SynqLien records.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"recordId":{"type":"string"}},"required":["recordId"]}""",
            RequiredPermissions: ["SYNQ_LIENS.lien:read:own"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 4000),
        new(
            ToolKey: "careconnect.referral.lookup",
            Name: "CareConnect referral lookup",
            Description: "Read-only lookup for the current authorized CareConnect referral context.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"referralId":{"type":"string"}},"required":["referralId"]}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.referral:read:own", "SYNQ_CARECONNECT.referral:read:addressed"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 5000),
        new(
            ToolKey: "careconnect.referral.history.lookup",
            Name: "CareConnect referral history lookup",
            Description: "Reads the recent status history for an authorized CareConnect referral.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"referralId":{"type":"string"},"top":{"type":"integer","minimum":1,"maximum":25}},"required":["referralId"]}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.referral:read:own", "SYNQ_CARECONNECT.referral:read:addressed"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 5000),
        new(
            ToolKey: "careconnect.referral.search",
            Name: "CareConnect referral search",
            Description: "Searches authorized CareConnect referrals by patient/client, provider, provider organization, law firm, or referrer without requiring a specific referral page.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"clientName":{"type":"string"},"patientName":{"type":"string"},"caseNumber":{"type":"string"},"providerName":{"type":"string"},"providerOrganizationName":{"type":"string"},"referrerName":{"type":"string"},"lawFirmName":{"type":"string"},"referringOrganizationName":{"type":"string"},"status":{"type":"string"},"createdFromUtc":{"type":"string","format":"date-time"},"createdToUtc":{"type":"string","format":"date-time"},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.referral:read:own", "SYNQ_CARECONNECT.referral:read:addressed"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 7000),
        new(
            ToolKey: "careconnect.provider.search",
            Name: "CareConnect provider search",
            Description: "Searches the authorized CareConnect provider directory.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"name":{"type":"string"},"city":{"type":"string"},"state":{"type":"string"},"acceptingReferrals":{"type":"boolean"},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.provider:search"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 6000),
        new(
            ToolKey: "careconnect.referrer.search",
            Name: "CareConnect referrer search",
            Description: "Finds law firms or referrers represented in the current authorized referral queue.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"referrerName":{"type":"string"},"status":{"type":"string"},"top":{"type":"integer","minimum":1,"maximum":10}}}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.referral:read:own", "SYNQ_CARECONNECT.referral:read:addressed"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 5000),
        new(
            ToolKey: "careconnect.referral.queue.summary",
            Name: "CareConnect referral queue summary",
            Description: "Summarizes authorized referral counts, KPI-style status groups, time-window totals, and recent visible referrals.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"providerName":{"type":"string"},"referrerName":{"type":"string"},"status":{"type":"string","enum":["New","NewOpened","Accepted","InProgress","Completed","Declined","Cancelled"]},"statusGroup":{"type":"string","enum":["new","open","closed"]},"days":{"type":"integer","minimum":1,"maximum":365},"createdFromUtc":{"type":"string","format":"date-time"},"createdToUtc":{"type":"string","format":"date-time"},"recentTop":{"type":"integer","minimum":1,"maximum":10}}}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.referral:read:own", "SYNQ_CARECONNECT.referral:read:addressed"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 6000),
    ];

    public IReadOnlyList<AssistantToolDefinitionDto> ListToolsForAgent(string agentKey)
    {
        if (agentKey.Equals(AssistantModuleKeys.LiensAgentKey, StringComparison.OrdinalIgnoreCase))
            return Tools.Where(t => t.RequiredProductCodes.Contains("SynqLien") || t.ToolKey == "tenant.context.summary").ToList();

        if (agentKey.Equals(AssistantModuleKeys.CareConnectAgentKey, StringComparison.OrdinalIgnoreCase))
            return Tools.Where(t => t.RequiredProductCodes.Contains("CareConnect") || t.ToolKey == "tenant.context.summary").ToList();

        return Tools.Where(t =>
                t.ToolKey == "tenant.context.summary" ||
                t.RequiredProductCodes.Contains("CareConnect"))
            .ToList();
    }
}
