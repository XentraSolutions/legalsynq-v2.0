namespace Liens.Application.DTOs;

public sealed record SynqLienLienLookupOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    SynqLienLienLookupResult? Lien);

public sealed record SynqLienLienLookupResult(
    Guid LienId,
    string LienNumber,
    string Status,
    string LienType,
    string SubjectDisplayName,
    Guid? CaseId,
    string? CaseNumber,
    string? CaseTitle,
    decimal OriginalAmount,
    decimal? CurrentBalance,
    decimal? OfferPrice,
    decimal? PurchasePrice,
    decimal? PayoffAmount,
    string? Jurisdiction,
    bool IsConfidential,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateOnly? IncidentDate = null,
    string? PurchaseDate = null,
    DateOnly? InitialServiceDate = null,
    DateOnly? EndServiceDate = null,
    decimal? TotalPurchase = null,
    decimal? TotalBilling = null,
    decimal? ReductionAmount = null,
    bool? IsServicing = null,
    string? Description = null,
    int SupportingDocumentCount = 0);

public sealed record SynqLienLienSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<SynqLienLienSearchResult> Liens);

public sealed record SynqLienLienSearchResult(
    Guid LienId,
    string LienNumber,
    string Status,
    string LienType,
    string SubjectDisplayName,
    Guid? CaseId,
    string? CaseNumber,
    decimal OriginalAmount,
    decimal? CurrentBalance,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? PurchaseDate = null,
    decimal? TotalPurchase = null,
    decimal? TotalBilling = null,
    int SupportingDocumentCount = 0);

public sealed record SynqLienCaseLookupOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    SynqLienCaseLookupResult? Case);

public sealed record SynqLienCaseLookupResult(
    Guid CaseId,
    string CaseNumber,
    string ClientDisplayName,
    string Status,
    string? Title,
    string? CaseType,
    string? CurrentMedicalStatus,
    string? LawFirm,
    string? CaseManager,
    decimal? DemandAmount,
    decimal? SettlementAmount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<SynqLienLienSearchResult> Liens,
    DateOnly? DateOfLoss = null,
    DateOnly? ClientDateOfBirth = null,
    bool? IsClientMinor = null,
    string? ClientPhone = null,
    string? ClientEmail = null,
    string? ClientAddress = null,
    string? StateOfIncident = null,
    string? AccidentType = null,
    DateTime? OpenedAtUtc = null,
    DateTime? ClosedAtUtc = null);

public sealed record SynqLienCaseSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    IReadOnlyList<SynqLienCaseSearchResult> Cases);

public sealed record SynqLienCaseSearchResult(
    Guid CaseId,
    string CaseNumber,
    string ClientDisplayName,
    string Status,
    string? Title,
    string? CaseType,
    string? CurrentMedicalStatus,
    string? LawFirm,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CaseManager = null,
    string? StateOfIncident = null,
    string? AccidentType = null,
    DateOnly? DateOfLoss = null,
    DateTime? OpenedAtUtc = null,
    DateTime? ClosedAtUtc = null);

public sealed record SynqLienQueueSummaryOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalVisibleLiens,
    int WindowLienCount,
    int MatchingLienCount,
    int DraftLienCount,
    int OpenLienCount,
    int ClosedLienCount,
    DateTime? WindowFromUtc,
    DateTime? WindowToUtc,
    string? AppliedStatus,
    string? AppliedStatusGroup,
    IReadOnlyList<SynqLienStatusCount> StatusCounts,
    IReadOnlyList<SynqLienLienSearchResult> RecentLiens);

public sealed record SynqLienStatusCount(
    string Status,
    int Count);

public sealed record SynqLienDateWindow(
    string? Preset,
    DateTime? FromUtc,
    DateTime? ToUtc);

public sealed record SynqLienCaseInsightsOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    SynqLienCaseInsightsResult? Insights);

public sealed record SynqLienCaseInsightsResult(
    SynqLienCaseLookupResult Case,
    SynqLienDateWindow DateWindow,
    SynqLienCaseLienMetrics LienMetrics,
    SynqLienFinancialMetrics Financials,
    SynqLienDocumentMetrics Documents,
    SynqLienNoteMetrics Notes,
    SynqLienServicingMetrics Servicing,
    SynqLienTaskMetrics Tasks,
    IReadOnlyList<SynqLienLienInsight> Liens,
    IReadOnlyList<SynqLienDocumentInsight> RecentDocuments,
    IReadOnlyList<SynqLienNoteInsight> RecentNotes,
    IReadOnlyList<SynqLienServicingInsight> RecentServicing,
    IReadOnlyList<SynqLienTaskInsight> RecentTasks,
    IReadOnlyList<SynqLienActivityInsight> ImportantUpdates,
    IReadOnlyList<SynqLienCapabilityStatus> CapabilityStatuses,
    SynqLienCaseExport? Export);

public sealed record SynqLienCaseLienMetrics(
    int TotalLienCount,
    int OpenLienCount,
    int ClosedLienCount,
    int MedicalLienCount,
    int ServicingLienCount,
    int RejectedLienCount,
    int MissingPurchaseDateCount,
    int MissingDocumentCount,
    SynqLienLienInsight? HighestBalanceLien);

public sealed record SynqLienFinancialMetrics(
    decimal TotalPurchaseAmount,
    decimal TotalBillingAmount,
    decimal TotalReductionAmount,
    decimal OutstandingBalance,
    decimal? EstimatedSettlementAmount,
    decimal ExpectedLienPayoutAfterReductions,
    int LiensWithNoReductionCount);

public sealed record SynqLienDocumentMetrics(
    int CaseDocumentCount,
    int LienDocumentCount,
    int MedicalRecordCount,
    int MissingRequiredDocumentCount,
    DateTime? LatestDocumentUploadedAtUtc,
    IReadOnlyList<string> RequiredDocumentsStillMissing,
    bool DocumentContentSummarizationAvailable);

public sealed record SynqLienNoteMetrics(
    int TotalNoteCount,
    int WindowNoteCount,
    int ImportantNoteCount,
    DateTime? LatestNoteAtUtc);

public sealed record SynqLienServicingMetrics(
    int TotalServicingItemCount,
    int ActiveServicingItemCount,
    int OverdueServicingItemCount,
    IReadOnlyList<SynqLienStatusCount> StatusCounts);

public sealed record SynqLienTaskMetrics(
    int TotalTaskCount,
    int OpenTaskCount,
    int OverdueTaskCount,
    int DueTodayTaskCount,
    int HighPriorityTaskCount,
    int AssignedToCurrentUserCount,
    IReadOnlyList<SynqLienStatusCount> StatusCounts);

public sealed record SynqLienLienInsight(
    Guid LienId,
    string LienNumber,
    string Status,
    string LienType,
    string SubjectDisplayName,
    decimal OriginalAmount,
    decimal? CurrentBalance,
    decimal? TotalPurchase,
    decimal? TotalBilling,
    decimal ReductionAmount,
    string? PurchaseDate,
    DateOnly? InitialServiceDate,
    DateOnly? EndServiceDate,
    bool IsOpen,
    bool IsMedical,
    bool IsServicing,
    bool MissingPurchaseDate,
    bool MissingDocuments,
    int SupportingDocumentCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SynqLienDocumentInsight(
    Guid ServicingItemId,
    Guid? CaseId,
    Guid? LienId,
    string FileName,
    string? DocumentTypeId,
    string? Url,
    string Source,
    DateTime UploadedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SynqLienNoteInsight(
    Guid NoteId,
    string Content,
    string Category,
    bool IsPinned,
    string CreatedByName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    bool IsImportant);

public sealed record SynqLienServicingInsight(
    Guid ServicingItemId,
    string TaskNumber,
    string TaskType,
    string Description,
    string Status,
    string Priority,
    string AssignedTo,
    Guid? AssignedToUserId,
    Guid? CaseId,
    Guid? LienId,
    DateOnly? DueDate,
    bool IsActive,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SynqLienTaskInsight(
    Guid TaskId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssignedUserId,
    Guid? CaseId,
    IReadOnlyList<Guid> LienIds,
    DateTime? DueDateUtc,
    bool IsOpen,
    bool IsOverdue,
    bool IsDueToday,
    bool IsHighPriority,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SynqLienActivityInsight(
    string ActivityType,
    string Label,
    string? Detail,
    Guid? CaseId,
    Guid? LienId,
    Guid? SourceId,
    DateTime OccurredAtUtc,
    bool IsImportant);

public sealed record SynqLienCapabilityStatus(
    string Capability,
    bool Available,
    string Status,
    string? Detail);

public sealed record SynqLienCaseExport(
    string SuggestedFileName,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Sheets);

public sealed record SynqLienTaskSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    SynqLienDateWindow DateWindow,
    SynqLienTaskMetrics Metrics,
    IReadOnlyList<SynqLienTaskInsight> Tasks);

public sealed record SynqLienServicingSearchOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    int TotalCount,
    SynqLienDateWindow DateWindow,
    SynqLienServicingMetrics Metrics,
    IReadOnlyList<SynqLienServicingInsight> ServicingItems);

public sealed record SynqLienReportSummaryOutcome(
    bool Succeeded,
    string Status,
    string? SafeError,
    SynqLienDateWindow DateWindow,
    int TotalCaseCount,
    int ActiveCaseCount,
    int OpenedCaseCount,
    int TotalLienCount,
    int ClosedLienCount,
    IReadOnlyList<SynqLienGroupCount> ActiveCasesByCaseManager,
    IReadOnlyList<SynqLienGroupCount> ActiveCasesByLawFirm,
    IReadOnlyList<SynqLienCaseSearchResult> RecentCases,
    IReadOnlyList<SynqLienLienSearchResult> RecentLiens);

public sealed record SynqLienGroupCount(
    string Key,
    int Count);
