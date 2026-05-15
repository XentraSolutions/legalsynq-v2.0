namespace Commerce.Application.Common.Exceptions;

/// <summary>
/// Reprocess was attempted on a provider event log row that is not in
/// a reprocessable state (e.g. already <c>Processed</c> without an
/// explicit force flag). Mapped to HTTP 409.
/// </summary>
public sealed class ProviderEventReprocessNotAllowedException : CatalogException
{
    public Guid EventLogId { get; }
    public string CurrentStatus { get; }

    public ProviderEventReprocessNotAllowedException(Guid eventLogId, string currentStatus)
        : base($"Provider event '{eventLogId}' cannot be reprocessed (status='{currentStatus}').")
    {
        EventLogId = eventLogId;
        CurrentStatus = currentStatus;
    }
}

/// <summary>
/// A financial uniqueness invariant was violated (e.g. duplicate
/// (Provider, ProviderPaymentId) on payments, or duplicate invoice
/// number). Mapped to HTTP 409.
/// </summary>
public sealed class FinancialRecordConflictException : CatalogException
{
    public string Resource { get; }
    public string Detail { get; }

    public FinancialRecordConflictException(string resource, string detail)
        : base($"Financial record conflict on '{resource}': {detail}")
    {
        Resource = resource;
        Detail = detail;
    }
}
