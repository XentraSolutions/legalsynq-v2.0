using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class StaticAssistantToolRegistry : IAssistantToolRegistry
{
    private static readonly string[] SynqLienReadPermissions =
    [
        "SYNQ_LIENS.lien:read",
        "SYNQ_LIENS.lien:read:own",
        "SYNQ_LIENS.lien:browse",
        "SYNQ_LIENS.lien:read:held",
    ];

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
            Description: "Compatibility alias for read-only authorized SynqLien lien lookup by record id.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"recordId":{"type":"string"}},"required":["recordId"]}""",
            RequiredPermissions: SynqLienReadPermissions,
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 4000),
        new(
            ToolKey: "synqlien.lien.lookup",
            Name: "SynqLien lien lookup",
            Description: "Read-only lookup for an authorized SynqLien lien by lien id or lien number.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"lienId":{"type":"string"},"lienNumber":{"type":"string"}}}""",
            RequiredPermissions: SynqLienReadPermissions,
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 5000),
        new(
            ToolKey: "synqlien.lien.search",
            Name: "SynqLien lien search",
            Description: "Searches authorized SynqLien liens by lien number, subject/client name, case number, status, status group, lien type, or created date window.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"subjectName":{"type":"string"},"clientName":{"type":"string"},"caseNumber":{"type":"string"},"status":{"type":"string"},"statusGroup":{"type":"string","enum":["draft","open","closed","marketplace","servicing"]},"lienType":{"type":"string"},"createdFromUtc":{"type":"string","format":"date-time"},"createdToUtc":{"type":"string","format":"date-time"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: SynqLienReadPermissions,
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 7000),
        new(
            ToolKey: "synqlien.lien.queue.summary",
            Name: "SynqLien lien queue summary",
            Description: "Summarizes authorized SynqLien lien counts, lifecycle status mix, time-window totals, and recent visible liens.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"subjectName":{"type":"string"},"caseNumber":{"type":"string"},"status":{"type":"string"},"statusGroup":{"type":"string","enum":["draft","open","closed","marketplace","servicing"]},"lienType":{"type":"string"},"days":{"type":"integer","minimum":1,"maximum":365},"createdFromUtc":{"type":"string","format":"date-time"},"createdToUtc":{"type":"string","format":"date-time"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"recentTop":{"type":"integer","minimum":1,"maximum":10}}}""",
            RequiredPermissions: SynqLienReadPermissions,
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 6000),
        new(
            ToolKey: "synqlien.case.lookup",
            Name: "SynqLien case lookup",
            Description: "Read-only lookup for an authorized SynqLien case by case id or case number, including linked liens.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"caseId":{"type":"string"},"caseNumber":{"type":"string"},"liensTop":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_LIENS.case:read"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 6000),
        new(
            ToolKey: "synqlien.case.insights",
            Name: "SynqLien case insights",
            Description: "Returns a comprehensive read-only case snapshot: case summary, contact/date-of-loss/minor status, linked liens, financial totals/reductions, documents, notes, activity, servicing, tasks, missing-document flags, and optional Excel-ready sheets.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"caseId":{"type":"string"},"caseNumber":{"type":"string"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"dateFromUtc":{"type":"string","format":"date-time"},"dateToUtc":{"type":"string","format":"date-time"},"top":{"type":"integer","minimum":1,"maximum":15},"includeExport":{"type":"boolean"}}}""",
            RequiredPermissions: ["SYNQ_LIENS.case:read"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 16000),
        new(
            ToolKey: "synqlien.case.search",
            Name: "SynqLien case search",
            Description: "Searches authorized SynqLien cases by client name, case number, status, title, or external reference.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"clientName":{"type":"string"},"caseNumber":{"type":"string"},"status":{"type":"string"},"lawFirm":{"type":"string"},"caseManager":{"type":"string"},"caseType":{"type":"string"},"accidentType":{"type":"string"},"state":{"type":"string"},"openedFromUtc":{"type":"string","format":"date-time"},"openedToUtc":{"type":"string","format":"date-time"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_LIENS.case:read"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 6000),
        new(
            ToolKey: "synqlien.task.search",
            Name: "SynqLien task search",
            Description: "Searches authorized SynqLien tasks by assignment, case, lien, status, priority, due date window, overdue, due today, or high priority.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"status":{"type":"string"},"statusGroup":{"type":"string","enum":["open","closed"]},"priority":{"type":"string"},"assignedUserId":{"type":"string"},"assignmentScope":{"type":"string","enum":["me","unassigned","others"]},"caseId":{"type":"string"},"lienId":{"type":"string"},"dueFromUtc":{"type":"string","format":"date-time"},"dueToUtc":{"type":"string","format":"date-time"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"overdue":{"type":"boolean"},"dueToday":{"type":"boolean"},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_LIENS.task:read"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 7000),
        new(
            ToolKey: "synqlien.servicing.search",
            Name: "SynqLien servicing search",
            Description: "Searches authorized SynqLien servicing items by case, lien, assignee, status, priority, due date window, or overdue state.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"status":{"type":"string"},"statusGroup":{"type":"string","enum":["open","closed"]},"priority":{"type":"string"},"assignedTo":{"type":"string"},"caseId":{"type":"string"},"lienId":{"type":"string"},"dueFromUtc":{"type":"string","format":"date-time"},"dueToUtc":{"type":"string","format":"date-time"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"overdue":{"type":"boolean"},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_LIENS.lien:service"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 7000),
        new(
            ToolKey: "synqlien.report.summary",
            Name: "SynqLien report summary",
            Description: "Builds read-only SynqLien reporting summaries for cases and liens, including opened cases, closed liens, active cases by case manager, and active cases by law firm.",
            InputSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"searchText":{"type":"string"},"caseStatus":{"type":"string"},"caseStatusGroup":{"type":"string","enum":["open","closed"]},"lienStatus":{"type":"string"},"lienStatusGroup":{"type":"string","enum":["draft","open","closed","marketplace","servicing"]},"lawFirm":{"type":"string"},"caseManager":{"type":"string"},"caseType":{"type":"string"},"accidentType":{"type":"string"},"state":{"type":"string"},"dateFromUtc":{"type":"string","format":"date-time"},"dateToUtc":{"type":"string","format":"date-time"},"datePreset":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_30_days","last_60_days","last_90_days","life_to_date"]},"top":{"type":"integer","minimum":1,"maximum":15}}}""",
            RequiredPermissions: ["SYNQ_LIENS.case:read"],
            RequiredProductCodes: ["SynqLien"],
            ConfirmationRequired: false,
            MaxOutputCharacters: 9000),
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
                t.RequiredProductCodes.Contains("CareConnect") ||
                t.RequiredProductCodes.Contains("SynqLien"))
            .ToList();
    }
}
