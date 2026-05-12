namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-021: Governance release package.
/// Groups governance changes (rule packs, rules, profiles) into an auditable,
/// approval-gated, optionally scheduled deployment unit.
///
/// State machine:
///   draft → pending_review → approved → active | scheduled → active
///   pending_review → rejected → archived | draft
///   active → superseded | archived
///   activation_failed → archived | draft
/// </summary>
public class SmsGovernanceReleasePackage
{
    public Guid    Id                    { get; set; }
    public Guid?   TenantId              { get; set; }   // null = platform/global release
    public string  Name                  { get; set; } = string.Empty;
    public string? Description           { get; set; }

    /// <summary>draft | pending_review | approved | scheduled | active | superseded | rejected | archived | activation_failed</summary>
    public string  ReleaseState          { get; set; } = ReleaseStates.Draft;

    /// <summary>rule_pack | rule_set | compliance_profile | mixed_governance</summary>
    public string  ReleaseType           { get; set; } = ReleaseTypes.MixedGovernance;

    public DateTime? ScheduledActivationAt { get; set; }
    public DateTime? ActivatedAt           { get; set; }
    public DateTime? SupersededAt          { get; set; }
    public Guid?     SupersededByReleaseId { get; set; }
    public DateTime? RejectedAt            { get; set; }
    public DateTime? ArchivedAt            { get; set; }

    public DateTime  CreatedAt   { get; set; }
    public DateTime  UpdatedAt   { get; set; }
    public string?   CreatedBy   { get; set; }
    public string?   UpdatedBy   { get; set; }
}

public static class ReleaseStates
{
    public const string Draft             = "draft";
    public const string PendingReview     = "pending_review";
    public const string Approved          = "approved";
    public const string Scheduled         = "scheduled";
    public const string Active            = "active";
    public const string Superseded        = "superseded";
    public const string Rejected          = "rejected";
    public const string Archived          = "archived";
    public const string ActivationFailed  = "activation_failed";

    public static readonly IReadOnlySet<string> EditableStates =
        new HashSet<string> { Draft };

    public static readonly IReadOnlySet<string> TerminalStates =
        new HashSet<string> { Archived, Superseded };

    public static bool IsEditable(string state) => EditableStates.Contains(state);
    public static bool IsTerminal(string state)  => TerminalStates.Contains(state);
}

public static class ReleaseTypes
{
    public const string RulePack          = "rule_pack";
    public const string RuleSet           = "rule_set";
    public const string ComplianceProfile = "compliance_profile";
    public const string MixedGovernance   = "mixed_governance";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { RulePack, RuleSet, ComplianceProfile, MixedGovernance };
}
