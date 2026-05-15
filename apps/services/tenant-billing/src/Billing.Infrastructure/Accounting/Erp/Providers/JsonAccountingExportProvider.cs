using System.Text.Json;
using Microsoft.Extensions.Logging;
using Billing.Domain.Accounting.Erp;

namespace Billing.Infrastructure.Accounting.Erp.Providers;

/// <summary>
/// MS-BILL-ERP-001 — In-process payload-capture provider. Returns
/// <see cref="AccountingExportStatus.Exported"/> with a deterministic
/// pseudo-external-reference id of the form
/// <c>"json:{correlationId}"</c>.
///
/// <para>
/// This provider is NOT a network call: it simply demonstrates the
/// successful-path lifecycle (Pending → Exported), captures the
/// canonical JSON the operator can hand-deliver to a downstream
/// system, and exercises the duplicate-prevention path in tests
/// and demos. There is no third-party dependency, no credential
/// requirement, no outbound HTTP.
/// </para>
///
/// <para>
/// Forbidden surfaces explicitly avoided here:
/// no QuickBooks / NetSuite / Sage / Xero API call, no scheduled
/// job, no queue / outbox / event-bus emission, no callback into
/// Billing.Api, no mutation of any Billing row.
/// </para>
/// </summary>
public sealed class JsonAccountingExportProvider : IAccountingExportProvider
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly ILogger<JsonAccountingExportProvider> _log;

    public JsonAccountingExportProvider(ILogger<JsonAccountingExportProvider> log)
    {
        _log = log;
    }

    public string ProviderName => "json";

    /// <summary>
    /// Always true — the provider has no credential or option
    /// dependency. The payload-shape contract is the configuration.
    /// </summary>
    public bool IsConfigured => true;

    public Task<AccountingExportProviderResult> ExportAsync(
        AccountingExportPayload payload,
        CancellationToken ct = default)
    {
        // Render the payload once so the orchestrator can persist it
        // verbatim on the accounting_exports.PayloadJson column. We
        // serialise here (instead of in the orchestrator) because
        // future providers (e.g. QuickBooks) may transform the
        // payload before sending; the persisted JSON should match
        // what the provider actually saw.
        var json = JsonSerializer.Serialize(payload, PayloadJsonOptions);

        _log.LogInformation(
            "accounting_export.json tenantId={TenantId} exportType={ExportType} correlationId={CorrelationId} bytes={Bytes}",
            payload.TenantId, payload.ExportType, payload.CorrelationId, json.Length);

        // Echo the JSON back via the FailureReason channel? No — it
        // is persisted by the orchestrator from a separate route.
        // Here we only return the deterministic external reference.
        var externalReference = $"json:{payload.CorrelationId}";

        return Task.FromResult(
            AccountingExportProviderResult.Exported(
                provider: ProviderName,
                correlationId: payload.CorrelationId,
                externalReferenceId: externalReference));
    }
}
