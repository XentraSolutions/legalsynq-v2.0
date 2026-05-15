using Microsoft.Extensions.Logging;

namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-001 — Default <see cref="IStatementDeliveryProvider"/>
/// when no real notification provider is wired. Always returns
/// <see cref="StatementDeliveryResult.ProviderNotConfigured"/> so the
/// API contract, the BFF, and the tenant UI all see the same
/// deterministic "ProviderUnavailable / ProviderNotConfigured"
/// outcome they did under the WRITE-009 placeholder.
///
/// <para>
/// This is NOT a development-only stub: it is the supported
/// production fallback for a deployment that has not yet wired a
/// notification provider. Operators see the documented "Email
/// delivery is not configured yet" banner and can download the HTML
/// for manual delivery without losing audit trail (the orchestrator
/// still persists every attempt).
/// </para>
/// </summary>
public sealed class NoOpStatementDeliveryProvider : IStatementDeliveryProvider
{
    private readonly ILogger<NoOpStatementDeliveryProvider> _log;

    public NoOpStatementDeliveryProvider(ILogger<NoOpStatementDeliveryProvider> log)
    {
        _log = log;
    }

    public string ProviderName => "noop";

    public bool IsConfigured => false;

    public Task<StatementDeliveryResult> SendAsync(
        StatementDeliveryRequest request,
        CancellationToken ct = default)
    {
        // Structured log only. NEVER log the rendered HTML body or
        // the recipient address: the recipient is PII and the body
        // contains the full statement. The correlation id is enough
        // to tie this back to the persisted delivery row.
        _log.LogInformation(
            "Statement delivery skipped: no provider configured. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
            request.TenantId, request.StatementId, request.CorrelationId);

        return Task.FromResult(
            StatementDeliveryResult.ProviderNotConfigured(
                provider: ProviderName,
                correlationId: request.CorrelationId));
    }
}
