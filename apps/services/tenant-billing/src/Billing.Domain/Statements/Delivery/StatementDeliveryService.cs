using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-001 — Default <see cref="IStatementDeliveryService"/>.
///
/// Pipeline (single tenant-scoped transaction at persistence time):
/// <list type="number">
///   <item>Load the snapshot via
///   <see cref="ICustomerStatementPersistenceService.GetHistoryAsync"/>.
///   Cross-tenant ids surface as null and propagate to the
///   controller's 404.</item>
///   <item>Resolve the recipient from the persisted billing customer
///   record (customer.Email). A missing/whitespace email short-
///   circuits to <see cref="StatementDeliveryStatus.InvalidRecipient"/>
///   without invoking the provider.</item>
///   <item>Render HTML via
///   <see cref="ICustomerStatementPersistenceService.RenderHtmlAsync"/>
///   — that path reads ONLY the persisted snapshot JSON / HTML, never
///   live invoices/payments, so an after-the-fact ledger change can
///   never alter the bytes that are sent.</item>
///   <item>Invoke
///   <see cref="IStatementDeliveryProvider.SendAsync"/>; predictable
///   failures return a result, programmer errors are caught and
///   coerced to <see cref="StatementDeliveryStatus.Failed"/>.</item>
///   <item>Persist the deterministic outcome on the snapshot row
///   via <see cref="ICustomerStatementPersistenceService.RecordDeliveryAttemptAsync"/>
///   (append-only retry counter; never overwrites snapshot
///   content).</item>
/// </list>
///
/// All structured logs use the per-attempt correlation id only —
/// never the recipient address (PII) and never the rendered HTML.
/// </summary>
public sealed class StatementDeliveryService : IStatementDeliveryService
{
    private readonly ICustomerStatementPersistenceService _persistence;
    private readonly ICustomerRepository _customers;
    private readonly IStatementDeliveryProvider _provider;
    private readonly IProviderHealthMonitor _health;
    private readonly IOptionsMonitor<StatementRetryOptions> _retryOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<StatementDeliveryService> _log;

    public StatementDeliveryService(
        ICustomerStatementPersistenceService persistence,
        ICustomerRepository customers,
        IStatementDeliveryProvider provider,
        IProviderHealthMonitor health,
        IOptionsMonitor<StatementRetryOptions> retryOptions,
        TimeProvider time,
        ILogger<StatementDeliveryService> log)
    {
        _persistence = persistence;
        _customers = customers;
        _provider = provider;
        _health = health;
        _retryOptions = retryOptions;
        _time = time;
        _log = log;
    }

    public async Task<StatementSendOutcome?> SendAsync(
        Guid tenantId,
        Guid statementId,
        string? sentBy,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId required", nameof(tenantId));
        if (statementId == Guid.Empty) throw new ArgumentException("statementId required", nameof(statementId));

        var snapshot = await _persistence.GetHistoryAsync(tenantId, statementId, ct);
        if (snapshot is null) return null;

        var correlationId = Guid.CreateVersion7().ToString("N");

        // MS-BILL-INT-003 — Governance short-circuit. Evaluate
        // retryability against the persisted last-attempt state
        // BEFORE rendering or invoking the provider. Cooldown +
        // retry-limit + non-retryable-terminal all collapse here
        // into a single deterministic outcome the controller maps
        // to 429 / 409 with the same response shape. The snapshot
        // row is NOT mutated on this branch — the prior last-attempt
        // truth (and its DeliveryAttemptedAtUtc that drives the
        // cooldown clock) MUST stay intact, otherwise an operator
        // could reset their own cooldown by clicking faster.
        var retryOptions = _retryOptions.CurrentValue;
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var decision = StatementRetryability.Evaluate(snapshot, retryOptions, nowUtc);
        if (!decision.IsRetryable)
        {
            _log.LogInformation(
                "Statement send governance-rejected. tenantId={TenantId} statementId={StatementId} reason={Reason} retryCount={RetryCount} correlationId={CorrelationId}",
                tenantId, statementId, decision.Reason, snapshot.DeliveryRetryCount, correlationId);
            // Return the un-mutated snapshot AND the typed
            // rejection so the controller can map to 429 / 409
            // without re-evaluating the matrix.
            return new StatementSendOutcome(snapshot, decision);
        }

        // Resolve recipient from the immutable snapshot's customer
        // pointer. We deliberately re-read the customer's current
        // email rather than encoding a stale one in the snapshot:
        // the snapshot CONTENT is immutable, but where we deliver
        // it is allowed to follow a changed customer email.
        var customer = await _customers.GetByIdAsync(tenantId, snapshot.CustomerId, ct);
        var recipientEmail = customer?.Email?.Trim() ?? string.Empty;
        var recipientName = customer?.Name ?? string.Empty;

        StatementDeliveryResult result;

        if (!_provider.IsConfigured)
        {
            // MS-BILL-INT-001 — Provider-config short-circuit MUST
            // run BEFORE recipient validation so the WRITE-009
            // deterministic-unavailable contract holds for EVERY
            // send attempt under the NoOp default — including
            // snapshots whose customer has no email on file. If
            // we validated the recipient first, a missing email
            // would surface as 422 InvalidRecipient and mask the
            // real story (no provider is wired). Operators must
            // see "Email delivery is not configured yet" first
            // and only get a recipient error once a real provider
            // is bound.
            result = StatementDeliveryResult.ProviderNotConfigured(
                provider: _provider.ProviderName,
                correlationId: correlationId);
        }
        else if (recipientEmail.Length == 0)
        {
            result = StatementDeliveryResult.InvalidRecipient(
                provider: _provider.ProviderName,
                correlationId: correlationId,
                reason: "RecipientEmailMissing");
        }
        else
        {
            string html;
            try
            {
                html = await _persistence.RenderHtmlAsync(tenantId, statementId, ct)
                       ?? throw new InvalidOperationException("Snapshot vanished between load and render");
            }
            catch (Exception renderEx)
            {
                _log.LogError(renderEx,
                    "Statement render failed before delivery. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
                    tenantId, statementId, correlationId);
                result = StatementDeliveryResult.Failed(
                    provider: _provider.ProviderName,
                    correlationId: correlationId,
                    reason: "RenderFailed");
                // MS-BILL-INT-003 — Render failure is a pre-provider
                // condition (template/data, not transport). It does
                // NOT contribute to provider-health classification —
                // same rationale as InvalidRecipient. We DO persist
                // it as Failed so the snapshot row reflects the
                // attempt, then return the typed outcome so the
                // controller can render the deterministic banner.
                var rfPersisted = await PersistAsync(snapshot, recipientEmail, sentBy, result, ct);
                return rfPersisted is null ? null : new StatementSendOutcome(rfPersisted, Rejection: null);
            }

            var request = new StatementDeliveryRequest(
                TenantId: tenantId,
                StatementId: statementId,
                StatementNumber: snapshot.StatementNumber,
                RecipientEmail: recipientEmail,
                RecipientName: recipientName,
                Subject: $"Statement {snapshot.StatementNumber}",
                HtmlBody: html,
                FilenameHint: $"{snapshot.StatementNumber}.html",
                CorrelationId: correlationId);

            try
            {
                result = await _provider.SendAsync(request, ct);
                if (!StatementDeliveryStatus.IsValid(result.Status))
                {
                    // Provider broke the contract; coerce to Failed
                    // rather than persist an unknown status string.
                    _log.LogError(
                        "Provider {Provider} returned invalid delivery status '{Status}' — coerced to Failed. correlationId={CorrelationId}",
                        _provider.ProviderName, result.Status, correlationId);
                    result = StatementDeliveryResult.Failed(
                        provider: _provider.ProviderName,
                        correlationId: correlationId,
                        reason: "ProviderReturnedInvalidStatus");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Statement delivery threw unexpectedly. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
                    tenantId, statementId, correlationId);
                result = StatementDeliveryResult.Failed(
                    provider: _provider.ProviderName,
                    correlationId: correlationId,
                    reason: "ProviderThrew:" + ex.GetType().Name);
            }
        }

        // MS-BILL-INT-003 — Record the deterministic outcome on the
        // in-memory provider-health monitor. Skipped on the
        // RetryNotAllowed branch above (we never get here) AND on
        // InvalidRecipient inside the monitor itself (customer-record
        // problem, not a provider problem). The monitor is process-
        // local, never gates the next click, and exists for operator
        // visibility only.
        try
        {
            _health.RecordOutcome(_provider.ProviderName, result.Status, nowUtc);
        }
        catch (Exception healthEx)
        {
            // Defensive — monitor MUST NOT bring down a successful
            // send. Log and continue.
            _log.LogWarning(healthEx,
                "Provider health monitor RecordOutcome threw (non-fatal). correlationId={CorrelationId}",
                correlationId);
        }

        var persisted = await PersistAsync(snapshot, recipientEmail, sentBy, result, ct);
        // Persistence returns null only if the snapshot vanished
        // between load and write (cross-tenant deletion race) —
        // surface as a "not found" outcome to the controller.
        return persisted is null ? null : new StatementSendOutcome(persisted, Rejection: null);
    }

    private async Task<CustomerStatement?> PersistAsync(
        CustomerStatement snapshot,
        string recipientEmail,
        string? sentBy,
        StatementDeliveryResult result,
        CancellationToken ct)
    {
        return await _persistence.RecordDeliveryAttemptAsync(
            tenantId: snapshot.TenantId,
            statementId: snapshot.Id,
            provider: result.Provider,
            deliveryStatus: result.Status,
            failureReason: result.FailureReason,
            recipientEmail: recipientEmail.Length > 0 ? recipientEmail : null,
            sentBy: sentBy,
            deliveryId: result.DeliveryId,
            correlationId: result.CorrelationId,
            ct: ct);
    }
}
