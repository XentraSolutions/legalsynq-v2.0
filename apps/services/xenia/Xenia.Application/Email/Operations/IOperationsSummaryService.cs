using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Produces aggregated operational summary metrics for the Xenia Email module.
///
/// Uses efficient database aggregation — never loads full record sets into memory.
/// </summary>
public interface IOperationsSummaryService
{
    Task<OperationsSummaryResult> GetSummaryAsync(OperationsSummaryQuery query, CancellationToken ct = default);
}

public sealed record OperationsSummaryQuery(
    Guid TenantId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    EmailProviderType? Provider = null,
    Guid? SourceId = null,
    IngestionRunStatus? RunStatus = null);

public sealed record OperationsSummaryResult(
    int TotalSources,
    int EnabledSources,
    int HealthySources,
    int DegradedSources,
    int UnhealthySources,
    int NeverValidatedSources,
    int CurrentlySyncingSources,
    int QueuedRuns,
    int ActiveRuns,
    int CompletedRuns,
    int CompletedWithErrorsRuns,
    int FailedRuns,
    int MessagesImported,
    int DuplicateMessages,
    int MessageFailures,
    int AttachmentsDispatched,
    int AttachmentFailures,
    double AverageSyncDurationMs,
    int ProviderThrottlingIncidents,
    int RetryCount,
    int LockContentionIncidents,
    int CursorResets,
    int OpenAlertsCritical,
    int OpenAlertsWarning,
    int OpenAlertsInformational,
    DateTime GeneratedAt);
