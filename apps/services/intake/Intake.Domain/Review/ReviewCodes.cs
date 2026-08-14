namespace Intake.Domain.Review;

public static class IntakeReviewStatuses
{
    public const string Pending = "PENDING";
    public const string Assigned = "ASSIGNED";
    public const string InReview = "IN_REVIEW";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string Superseded = "SUPERSEDED";
}

public static class IntakeReviewOutcomes
{
    public const string Approved = "APPROVED";
    public const string ApprovedWithCorrections = "APPROVED_WITH_CORRECTIONS";
    public const string Rejected = "REJECTED";
    public const string DuplicateConfirmed = "DUPLICATE_CONFIRMED";
    public const string NoDuplicate = "NOT_DUPLICATE";
    public const string NoMatchConfirmed = "NO_MATCH_CONFIRMED";
    public const string ReturnForReprocessing = "RETURN_FOR_REPROCESSING";
}

public static class IntakeReviewPriorities
{
    public const string Low = "LOW";
    public const string Normal = "NORMAL";
    public const string High = "HIGH";
    public const string Urgent = "URGENT";
}

public static class IntakeReviewCorrectionTypes
{
    public const string ValueCorrection = "VALUE_CORRECTION";
    public const string FactAdded = "FACT_ADDED";
    public const string FactRejected = "FACT_REJECTED";
    public const string ClassificationOverride = "CLASSIFICATION_OVERRIDE";
}

public static class IntakeReviewMatchDecisions
{
    public const string Confirmed = "CONFIRMED";
    public const string Rejected = "REJECTED";
    public const string NoMatch = "NO_MATCH";
    public const string ManualSelection = "MANUAL_SELECTION";
}

public static class IntakeReviewDuplicateDecisions
{
    public const string Confirmed = "DUPLICATE_CONFIRMED";
    public const string NotDuplicate = "NOT_DUPLICATE";
    public const string NeedsFurtherReview = "NEEDS_FURTHER_REVIEW";
}

public static class IntakeReviewFindingDecisions
{
    public const string Resolved = "RESOLVED";
    public const string Acknowledged = "ACKNOWLEDGED";
    public const string NotApplicable = "NOT_APPLICABLE";
}

public static class IntakeReviewActivityTypes
{
    public const string Created = "REVIEW_CREATED";
    public const string Assigned = "REVIEW_ASSIGNED";
    public const string Unassigned = "REVIEW_UNASSIGNED";
    public const string Claimed = "REVIEW_CLAIMED";
    public const string FactCorrected = "FACT_CORRECTED";
    public const string FactAdded = "FACT_ADDED";
    public const string FactRejected = "FACT_REJECTED";
    public const string ClassificationOverridden = "CLASSIFICATION_OVERRIDDEN";
    public const string MatchDecided = "MATCH_DECIDED";
    public const string DuplicateDecided = "DUPLICATE_DECIDED";
    public const string FindingDecided = "FINDING_DECIDED";
    public const string Completed = "REVIEW_COMPLETED";
    public const string Superseded = "REVIEW_SUPERSEDED";
}

public static class IntakeReviewErrorCodes
{
    public const string NotFound = "REVIEW_NOT_FOUND";
    public const string NotEligible = "REVIEW_NOT_ELIGIBLE";
    public const string Stale = "REVIEW_STALE";
    public const string AlreadyCompleted = "REVIEW_ALREADY_COMPLETED";
    public const string AssignmentConflict = "REVIEW_ASSIGNMENT_CONFLICT";
    public const string ConcurrencyConflict = "REVIEW_CONCURRENCY_CONFLICT";
    public const string CorrectionInvalid = "REVIEW_CORRECTION_INVALID";
    public const string MatchDecisionInvalid = "REVIEW_MATCH_DECISION_INVALID";
    public const string DuplicateDecisionRequired = "REVIEW_DUPLICATE_DECISION_REQUIRED";
    public const string FindingUnresolved = "REVIEW_FINDING_UNRESOLVED";
    public const string CompletionInvalid = "REVIEW_COMPLETION_INVALID";
    public const string TenantContextInvalid = "REVIEW_TENANT_CONTEXT_INVALID";
    public const string ReprocessingRequired = "REVIEW_REPROCESSING_REQUIRED";
    public const string UnauthorizedUser = "REVIEW_USER_UNAUTHORIZED";
}

public static class IntakeReviewCompletionReasonCodes
{
    public const string WrongDocument = "WRONG_DOCUMENT";
    public const string UnrelatedSubmission = "UNRELATED_SUBMISSION";
    public const string Unreadable = "UNREADABLE";
    public const string InsufficientInformation = "INSUFFICIENT_INFORMATION";
    public const string InvalidIntake = "INVALID_INTAKE";
    public const string Other = "OTHER";
}