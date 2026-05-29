namespace Liens.Domain.Enums;

public static class LookupCategory
{
    public const string CaseStatus        = "CaseStatus";
    public const string LienStatus        = "LienStatus";
    public const string LienType          = "LienType";
    public const string ContactType       = "ContactType";
    public const string ServicingStatus   = "ServicingStatus";
    public const string ServicingPriority = "ServicingPriority";
    public const string DocumentCategory  = "DocumentCategory";
    public const string State             = "State";
    public const string AccidentType      = "AccidentType";

    // Legacy SynqLiens-Core categories
    public const string MedicalStatus     = "MedicalStatus";
    public const string SettlementStatus  = "SettlementStatus";
    public const string SettlementType    = "SettlementType";
    public const string CurrentAttributes = "CurrentAttributes";
    public const string ProcedureCode     = "ProcedureCode";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        CaseStatus, LienStatus, LienType, ContactType,
        ServicingStatus, ServicingPriority, DocumentCategory,
        State, AccidentType,
        MedicalStatus, SettlementStatus, SettlementType, CurrentAttributes, ProcedureCode,
    };
}
