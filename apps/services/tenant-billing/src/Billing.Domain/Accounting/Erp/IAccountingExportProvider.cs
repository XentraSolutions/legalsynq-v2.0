namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Provider-independent contract for exporting an
/// immutable accounting projection to an external ERP/accounting
/// system (QuickBooks, NetSuite, Sage, Xero, Microsoft Dynamics, …).
///
/// <para>
/// The contract is intentionally one-way: payload in, deterministic
/// result out. Providers MUST NOT mutate Billing data, MUST NOT
/// emit events on a queue/event-bus, and MUST NOT call back into
/// Billing.Api. Bi-directional sync is forbidden by the prompt.
/// </para>
///
/// <para>
/// Concrete providers shipped in ERP-001:
///   - <see cref="Billing.Infrastructure.Accounting.Erp.Providers.NoOpAccountingExportProvider"/>
///     — the default fallback, always returns
///       <c>Status=ProviderUnavailable</c>.
///   - <see cref="Billing.Infrastructure.Accounting.Erp.Providers.JsonAccountingExportProvider"/>
///     — captures the payload as JSON for operator inspection /
///       file-based handoff. Stateless.
/// </para>
///
/// <para>
/// Future providers (QuickBooks, NetSuite, Sage, Xero, Dynamics)
/// plug in by registering a new <see cref="IAccountingExportProvider"/>
/// implementation with the same <see cref="ProviderName"/> contract
/// (lower-case, ascii). The orchestrator picks the provider by name.
/// </para>
/// </summary>
public interface IAccountingExportProvider
{
    /// <summary>
    /// Stable, lower-case provider identifier persisted on the
    /// <c>accounting_exports.Provider</c> column. Examples:
    /// <c>"noop"</c>, <c>"json"</c>, <c>"quickbooks"</c>,
    /// <c>"netsuite"</c>. MUST be unique across registered providers.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// True iff the provider has every credential / option it needs
    /// to perform a real export. The orchestrator uses this to
    /// surface a deterministic "ProviderUnavailable" result without
    /// invoking <see cref="ExportAsync"/>, mirroring the
    /// MS-BILL-INT-001 statement-delivery convention.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Hand the payload to the provider. The provider is read-only
    /// with respect to Billing — it MUST NOT call back into
    /// Billing.Api, MUST NOT mutate the payload, and MUST NOT
    /// retain references to it after the task completes.
    /// </summary>
    Task<AccountingExportProviderResult> ExportAsync(
        AccountingExportPayload payload,
        CancellationToken ct = default);
}
