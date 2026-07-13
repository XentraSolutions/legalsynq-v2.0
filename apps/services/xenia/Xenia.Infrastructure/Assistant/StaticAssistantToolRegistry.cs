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
            Description: "Placeholder read-only lookup contract for authorized CareConnect referrals.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"referralId":{"type":"string"}},"required":["referralId"]}""",
            RequiredPermissions: ["SYNQ_CARECONNECT.referral:read:own", "SYNQ_CARECONNECT.referral:read:addressed"],
            RequiredProductCodes: ["CareConnect"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 4000),
    ];

    public IReadOnlyList<AssistantToolDefinitionDto> ListToolsForAgent(string agentKey)
    {
        if (agentKey.Equals(AssistantModuleKeys.LiensAgentKey, StringComparison.OrdinalIgnoreCase))
            return Tools.Where(t => t.RequiredProductCodes.Contains("SynqLien") || t.ToolKey == "tenant.context.summary").ToList();

        if (agentKey.Equals(AssistantModuleKeys.CareConnectAgentKey, StringComparison.OrdinalIgnoreCase))
            return Tools.Where(t => t.RequiredProductCodes.Contains("CareConnect") || t.ToolKey == "tenant.context.summary").ToList();

        return Tools.Where(t => t.ToolKey == "tenant.context.summary").ToList();
    }
}
