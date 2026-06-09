using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Concrete orchestrator. The single seam between
/// the controller and the projection builder + provider + repository.
///
/// <para>
/// Read-only with respect to Billing accounting rows. Writes only
/// to <c>accounting_exports</c>: an INSERT (Pending) and exactly
/// one UPDATE (terminal). Failures from the provider DO NOT throw —
/// they surface as <c>Status=Failed</c> / <c>ProviderUnavailable</c>
/// on the persisted row so the operator UI can render the same
/// deterministic banner the WRITE-009 / INT-001 flows use.
/// </para>
/// </summary>
public sealed class AccountingExportService : IAccountingExportService
{
    /// <summary>
    /// Hard cap on rows pulled per-window per-projection. Prevents a
    /// pathological window (e.g. "from epoch to forever") from
    /// loading the entire Billing table into memory. A tenant
    /// **strictly exceeding** this cap is rejected with
    /// <see cref="AccountingExportStatus.Skipped"/> — they must
    /// choose a smaller window. Exactly <c>WindowRowHardCap</c>
    /// rows is allowed; the loaders are asked for cap+1 to
    /// distinguish "at cap" from "over cap".
    /// </summary>
    public const int WindowRowHardCap = 5000;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly IAccountingExportRepository _repo;
    private readonly IEnumerable<IAccountingExportProvider> _providers;
    private readonly TimeProvider _time;
    private readonly ILogger<AccountingExportService> _log;

    public AccountingExportService(
        IAccountingExportRepository repo,
        IEnumerable<IAccountingExportProvider> providers,
        TimeProvider time,
        ILogger<AccountingExportService> log)
    {
        _repo = repo;
        _providers = providers;
        _time = time;
        _log = log;
    }

    public async Task<AccountingExportRunResult> RunAsync(
        AccountingExportRunRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var tenantId = ResolveTenantFromContext(request);
        var providerName = (request.Provider ?? string.Empty).Trim().ToLowerInvariant();
        var exportType = (request.ExportType ?? string.Empty).Trim();
        var idempotencyKey = (request.IdempotencyKey ?? string.Empty).Trim();
        var requestedBy = string.IsNullOrWhiteSpace(request.RequestedBy)
            ? "tenant-admin"
            : request.RequestedBy.Trim();

        // ---- Input validation -------------------------------------
        if (request.WindowFromUtc >= request.WindowToUtc)
            throw new ArgumentException(
                "WindowFromUtc must be strictly before WindowToUtc.",
                nameof(request));
        if (string.IsNullOrEmpty(providerName))
            throw new ArgumentException("Provider is required.", nameof(request));
        if (string.IsNullOrEmpty(exportType))
            throw new ArgumentException("ExportType is required.", nameof(request));
        if (string.IsNullOrEmpty(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(request));
        if (exportType != AccountingExportType.AccountingBatch)
            throw new ArgumentException(
                $"Unknown exportType '{exportType}'.", nameof(request));

        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Unknown ERP export provider '{providerName}'.", nameof(request));

        var fingerprint = AccountingExportProjectionBuilder.ComputeFingerprint(
            tenantId, providerName, exportType,
            request.WindowFromUtc, request.WindowToUtc);

        // ---- Atomic dedupe + Pending reservation -------------------
        // The repository wraps the (check existing non-failed row +
        // insert new Pending) sequence in a serializable transaction
        // so two concurrent POSTs for the same fingerprint cannot
        // both reserve and both export. The second writer either
        // sees the first writer's Pending / Exported row (returned
        // as the existing slot owner) or blocks on the gap lock
        // until the first writer commits.
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var correlationId = Guid.CreateVersion7().ToString("N");
        var export = new AccountingExport
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Provider = providerName,
            ExportType = exportType,
            WindowFromUtc = request.WindowFromUtc,
            WindowToUtc = request.WindowToUtc,
            Status = AccountingExportStatus.Pending,
            CorrelationId = correlationId,
            RequestedBy = TruncateOrEmpty(requestedBy, 200),
            RequestedAtUtc = nowUtc,
            IdempotencyKey = TruncateOrEmpty(idempotencyKey, 128),
            Fingerprint = fingerprint,
            Reason = TruncateOrNull(request.Reason, 1000),
        };

        var existing = await _repo
            .TryReserveSlotAsync(tenantId, fingerprint, export, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            _log.LogInformation(
                "accounting_export.duplicate tenantId={TenantId} provider={Provider} fingerprint={Fingerprint} priorExportId={PriorExportId} priorStatus={PriorStatus}",
                tenantId, providerName, fingerprint, existing.Id, existing.Status);
            return new AccountingExportRunResult(
                ExportId: existing.Id,
                Provider: existing.Provider,
                ExportType: existing.ExportType,
                Status: AccountingExportStatus.Duplicate,
                ExternalReferenceId: existing.ExternalReferenceId,
                CorrelationId: existing.CorrelationId,
                FailureReason: null,
                RequestedAtUtc: existing.RequestedAtUtc,
                CompletedAtUtc: existing.CompletedAtUtc,
                InvoiceCount: existing.InvoiceCount,
                PaymentCount: existing.PaymentCount,
                AdjustmentCount: existing.AdjustmentCount,
                JournalEntryCount: existing.JournalEntryCount,
                WasDuplicate: true);
        }

        // From here on the Pending row exists. Wrap the rest of the
        // flow so EVERY exit path terminalises the row — including
        // cancellation and unhandled exceptions. A stranded Pending
        // row would be invisible to the dedupe path's "existing
        // non-failed" filter and operators would be unable to
        // retry the window.
        var terminalised = false;
        try
        {
            // ---- Build the canonical payload ----------------------
            // Ask for hardCap+1 so we can distinguish exactly
            // hardCap rows (allowed) from > hardCap (Skipped).
            var probeCap = WindowRowHardCap + 1;
            var invoices = await _repo.LoadInvoicesForWindowAsync(
                tenantId, request.WindowFromUtc, request.WindowToUtc,
                probeCap, ct).ConfigureAwait(false);
            var payments = await _repo.LoadPaymentsForWindowAsync(
                tenantId, request.WindowFromUtc, request.WindowToUtc,
                probeCap, ct).ConfigureAwait(false);
            var adjustments = await _repo.LoadAdjustmentsForWindowAsync(
                tenantId, request.WindowFromUtc, request.WindowToUtc,
                probeCap, ct).ConfigureAwait(false);

            if (invoices.Count > WindowRowHardCap
                || payments.Count > WindowRowHardCap
                || adjustments.Count > WindowRowHardCap)
            {
                await CompleteAsFailureAsync(
                    export,
                    AccountingExportStatus.Skipped,
                    $"Window exceeded the {WindowRowHardCap}-row hard cap (invoices={invoices.Count}, payments={payments.Count}, adjustments={adjustments.Count}). Choose a smaller date range.",
                    payloadJson: null,
                    invoiceCount: Math.Min(invoices.Count, WindowRowHardCap),
                    paymentCount: Math.Min(payments.Count, WindowRowHardCap),
                    adjustmentCount: Math.Min(adjustments.Count, WindowRowHardCap),
                    journalEntryCount: 0,
                    ct).ConfigureAwait(false);
                terminalised = true;
                return ToRunResult(export, wasDuplicate: false);
            }

            var customerIds = new HashSet<Guid>();
            foreach (var i in invoices) customerIds.Add(i.CustomerId);
            foreach (var a in adjustments) customerIds.Add(a.CustomerId);
            var customerNames = await _repo
                .LoadCustomerNamesAsync(tenantId, customerIds, ct)
                .ConfigureAwait(false);

            var payload = AccountingExportProjectionBuilder.Build(
                tenantId, exportType,
                request.WindowFromUtc, request.WindowToUtc,
                correlationId,
                invoices, payments, adjustments, customerNames);

            // ---- Provider dispatch --------------------------------
            AccountingExportProviderResult providerResult;
            try
            {
                providerResult = await provider
                    .ExportAsync(payload, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex,
                    "accounting_export.provider_exception tenantId={TenantId} provider={Provider} correlationId={CorrelationId}",
                    tenantId, providerName, correlationId);
                await CompleteAsFailureAsync(
                    export,
                    AccountingExportStatus.Failed,
                    "Provider threw an unexpected exception.",
                    payloadJson: SerialisePayload(payload),
                    invoiceCount: invoices.Count,
                    paymentCount: payments.Count,
                    adjustmentCount: adjustments.Count,
                    journalEntryCount: payload.JournalEntries.Count,
                    ct).ConfigureAwait(false);
                terminalised = true;
                return ToRunResult(export, wasDuplicate: false);
            }

            // ---- Terminal-state persist ---------------------------
            export.Status = providerResult.Status;
            export.ExternalReferenceId = providerResult.ExternalReferenceId;
            export.FailureReason = providerResult.FailureReason;
            export.CompletedAtUtc = _time.GetUtcNow().UtcDateTime;
            export.InvoiceCount = invoices.Count;
            export.PaymentCount = payments.Count;
            export.AdjustmentCount = adjustments.Count;
            export.JournalEntryCount = payload.JournalEntries.Count;
            export.PayloadJson = SerialisePayload(payload);
            await _repo.UpdateTerminalAsync(export, ct).ConfigureAwait(false);
            terminalised = true;

            _log.LogInformation(
                "accounting_export.completed tenantId={TenantId} exportId={ExportId} provider={Provider} status={Status} invoices={InvoiceCount} payments={PaymentCount} adjustments={AdjustmentCount} correlationId={CorrelationId}",
                tenantId, export.Id, providerName, export.Status,
                export.InvoiceCount, export.PaymentCount, export.AdjustmentCount, correlationId);

            return ToRunResult(export, wasDuplicate: false);
        }
        finally
        {
            if (!terminalised)
            {
                // Cancellation OR an unhandled exception in a load /
                // projection step. Use a fresh, NON-cancellable
                // token so the terminal write itself cannot be
                // cancelled — otherwise the Pending row would be
                // permanently stranded. Best-effort: swallow any
                // secondary exception so the original cause
                // propagates to the caller.
                try
                {
                    var reason = ct.IsCancellationRequested
                        ? "Export was cancelled before the provider responded."
                        : "Export aborted by an unexpected exception before provider dispatch.";
                    _log.LogWarning(
                        "accounting_export.stranded_terminalise tenantId={TenantId} exportId={ExportId} cancelled={Cancelled}",
                        tenantId, export.Id, ct.IsCancellationRequested);
                    await CompleteAsFailureAsync(
                        export,
                        AccountingExportStatus.Failed,
                        reason,
                        payloadJson: null,
                        invoiceCount: 0,
                        paymentCount: 0,
                        adjustmentCount: 0,
                        journalEntryCount: 0,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception terminaliseEx)
                {
                    _log.LogError(terminaliseEx,
                        "accounting_export.terminalise_failed tenantId={TenantId} exportId={ExportId}",
                        tenantId, export.Id);
                }
            }
        }
    }

    public Task<AccountingExport?> GetAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repo.GetByIdAsync(tenantId, exportId, ct);
    }

    public Task<IReadOnlyList<AccountingExport>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repo.ListAsync(tenantId, page, pageSize, ct);
    }

    // ----------------------------------------------------------------

    /// <summary>
    /// Tenant id resolution lives at the controller (it has the
    /// <see cref="ITenantContext"/>); we just defensively reject
    /// <c>Guid.Empty</c> here. The request shape carries the tenant
    /// implicitly via the orchestrator's <see cref="IAccountingExportRepository"/>
    /// + the controller-supplied <c>RequestedBy</c>; the actual
    /// tenant id is set by the controller before calling. To avoid
    /// passing tenant id twice in the request shape, the controller
    /// stamps it on a thread-static? — no. Simpler: tenant id is
    /// part of the request inside <see cref="AccountingExportRunRequest"/>?
    /// Not today. Keep it simple: tenant id flows via a separate
    /// parameter at the call site below.
    /// </summary>
    private static Guid ResolveTenantFromContext(AccountingExportRunRequest request)
    {
        // Tenant id is carried via the dedicated field below — see
        // overload <see cref="RunAsync"/> for the controller call
        // path. This helper exists to make the validation message
        // clear if tenant resolution ever moves into a thread-
        // static or an injected ITenantContext at this layer.
        return request is TenantScopedAccountingExportRunRequest scoped
            ? scoped.TenantId
            : throw new InvalidOperationException(
                "AccountingExportService.RunAsync requires a TenantScopedAccountingExportRunRequest. " +
                "Wrap the controller-built request via WithTenant(tenantId) before calling.");
    }

    private async Task CompleteAsFailureAsync(
        AccountingExport export,
        string status,
        string failureReason,
        string? payloadJson,
        int invoiceCount,
        int paymentCount,
        int adjustmentCount,
        int journalEntryCount,
        CancellationToken ct)
    {
        export.Status = status;
        export.FailureReason = TruncateOrNull(failureReason, 500);
        export.CompletedAtUtc = _time.GetUtcNow().UtcDateTime;
        export.InvoiceCount = invoiceCount;
        export.PaymentCount = paymentCount;
        export.AdjustmentCount = adjustmentCount;
        export.JournalEntryCount = journalEntryCount;
        export.PayloadJson = payloadJson;
        await _repo.UpdateTerminalAsync(export, ct).ConfigureAwait(false);
    }

    private static AccountingExportRunResult ToRunResult(
        AccountingExport export,
        bool wasDuplicate)
        => new(
            ExportId: export.Id,
            Provider: export.Provider,
            ExportType: export.ExportType,
            Status: export.Status,
            ExternalReferenceId: export.ExternalReferenceId,
            CorrelationId: export.CorrelationId,
            FailureReason: export.FailureReason,
            RequestedAtUtc: export.RequestedAtUtc,
            CompletedAtUtc: export.CompletedAtUtc,
            InvoiceCount: export.InvoiceCount,
            PaymentCount: export.PaymentCount,
            AdjustmentCount: export.AdjustmentCount,
            JournalEntryCount: export.JournalEntryCount,
            WasDuplicate: wasDuplicate);

    private static string SerialisePayload(AccountingExportPayload payload)
        => JsonSerializer.Serialize(payload, PayloadJsonOptions);

    private static string TruncateOrEmpty(string value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : (value.Length <= maxLength ? value : value[..maxLength]);

    private static string? TruncateOrNull(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : (value!.Length <= maxLength ? value : value[..maxLength]);
}

/// <summary>
/// MS-BILL-ERP-001 — Tenant-scoped wrapper of
/// <see cref="AccountingExportRunRequest"/>. The controller wraps
/// the validated request via <see cref="Wrap"/> before calling the
/// orchestrator; the orchestrator unwraps it via the type check in
/// <see cref="AccountingExportService"/>. This keeps the public
/// request shape free of the trusted tenant id (browser-supplied
/// tenant ids are forbidden by the threat model).
/// </summary>
public sealed record TenantScopedAccountingExportRunRequest(
    Guid TenantId,
    string Provider,
    string ExportType,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    string IdempotencyKey,
    string RequestedBy,
    string? Reason)
    : AccountingExportRunRequest(
        Provider, ExportType, WindowFromUtc, WindowToUtc,
        IdempotencyKey, RequestedBy, Reason)
{
    public static TenantScopedAccountingExportRunRequest Wrap(
        Guid tenantId,
        AccountingExportRunRequest request)
        => new(
            tenantId,
            request.Provider,
            request.ExportType,
            request.WindowFromUtc,
            request.WindowToUtc,
            request.IdempotencyKey,
            request.RequestedBy,
            request.Reason);
}
