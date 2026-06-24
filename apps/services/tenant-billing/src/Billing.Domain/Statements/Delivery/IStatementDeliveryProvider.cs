namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-001 — Provider abstraction for outbound statement
/// email delivery. One implementation is registered per process;
/// adding a second provider (e.g. SES, SMTP, NCM) is a config + DI
/// change, NOT a controller change.
///
/// <para>
/// Implementations MUST:
/// </para>
/// <list type="bullet">
///   <item>be transport-isolated — no direct ASP.NET HttpContext,
///   DbContext, or controller types.</item>
///   <item>return <see cref="StatementDeliveryResult"/> for all
///   predictable failure modes; only programmer errors should throw.</item>
///   <item>NEVER log the rendered HTML body, the recipient address
///   verbatim, or any provider secret. Log the
///   <see cref="StatementDeliveryRequest.CorrelationId"/> only.</item>
///   <item>be safe to call concurrently from multiple admin sessions
///   (the controller is HTTP-scoped; the provider is registered as
///   a singleton in the default DI configuration).</item>
/// </list>
///
/// <para>
/// The default registered provider is
/// <see cref="NoOpStatementDeliveryProvider"/>, which always returns
/// <see cref="StatementDeliveryResult.ProviderNotConfigured"/>. A real
/// provider can be slotted in by replacing the DI registration; the
/// controller / BFF / UI surface stays unchanged.
/// </para>
/// </summary>
public interface IStatementDeliveryProvider
{
    /// <summary>
    /// Stable, short identifier for this provider (e.g. "noop",
    /// "ncm-http", "smtp"). Persisted on every delivery row so
    /// historical audit knows which provider produced the row.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns true when the provider has all the secrets / config
    /// it needs to attempt delivery. The orchestrator checks this
    /// once before composing the request body so we don't render the
    /// HTML when there is no chance of delivery.
    /// </summary>
    bool IsConfigured { get; }

    Task<StatementDeliveryResult> SendAsync(
        StatementDeliveryRequest request,
        CancellationToken ct = default);
}
