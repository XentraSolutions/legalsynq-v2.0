using Microsoft.Extensions.Logging;
using Billing.Domain.Accounting.Erp;

namespace Billing.Infrastructure.Accounting.Erp.Providers;

/// <summary>
/// MS-BILL-ERP-001 — Default <see cref="IAccountingExportProvider"/>
/// when no real ERP provider is wired. Always returns
/// <see cref="AccountingExportStatus.ProviderUnavailable"/> so the
/// API contract, the BFF, and the tenant UI all see the same
/// deterministic outcome that future QuickBooks / NetSuite providers
/// surface when their credentials are missing.
///
/// <para>
/// This is the supported production fallback for a deployment that
/// has not yet wired an ERP provider. Operators see the documented
/// "ERP export provider is not configured yet" banner and can still
/// inspect the persisted batch row + payload JSON for manual
/// transport.
/// </para>
/// </summary>
public sealed class NoOpAccountingExportProvider : IAccountingExportProvider
{
    private readonly ILogger<NoOpAccountingExportProvider> _log;

    public NoOpAccountingExportProvider(ILogger<NoOpAccountingExportProvider> log)
    {
        _log = log;
    }

    public string ProviderName => "noop";

    public bool IsConfigured => false;

    public Task<AccountingExportProviderResult> ExportAsync(
        AccountingExportPayload payload,
        CancellationToken ct = default)
    {
        // Structured log only — never log the payload body. The
        // correlation id is enough to tie this back to the
        // persisted accounting_exports row.
        _log.LogInformation(
            "accounting_export.noop tenantId={TenantId} exportType={ExportType} correlationId={CorrelationId} invoiceCount={InvoiceCount} paymentCount={PaymentCount} adjustmentCount={AdjustmentCount}",
            payload.TenantId, payload.ExportType, payload.CorrelationId,
            payload.Invoices.Count, payload.Payments.Count, payload.Adjustments.Count);

        return Task.FromResult(
            AccountingExportProviderResult.ProviderUnavailable(
                provider: ProviderName,
                correlationId: payload.CorrelationId,
                failureReason: "ERP export provider is not configured."));
    }
}
