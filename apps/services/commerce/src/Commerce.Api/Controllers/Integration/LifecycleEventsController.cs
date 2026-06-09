using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Integration;

/// <summary>
/// Receives Commerce lifecycle event notifications from host platform services
/// (Identity, Tenant, product services).
///
/// <para>
/// This is the ingest side of the Commerce lifecycle integration contract.
/// Host services send events via <c>HttpCommerceLifecycleNotifier</c>
/// (BuildingBlocks) to this endpoint when <c>CommerceIntegration:Enabled=true</c>.
/// </para>
///
/// <para>
/// <b>FINAL-01 scope:</b> The endpoint validates and logs incoming events.
/// Persistence and downstream processing are deferred to a future phase.
/// Returning <c>202 Accepted</c> signals that Commerce has accepted the event;
/// it does not guarantee processing or persistence.
/// </para>
///
/// <para>
/// Auth: open to internal network in standalone mode. When
/// <c>LegalSynq:Identity:Enabled=true</c>, JWT bearer auth applies via the
/// standard Commerce JWT middleware, restricting to authenticated internal callers.
/// </para>
/// </summary>
[ApiController]
[Route("api/commerce/integration")]
public sealed class LifecycleEventsController : ControllerBase
{
    private readonly ILogger<LifecycleEventsController> _logger;

    public LifecycleEventsController(ILogger<LifecycleEventsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ingest a Commerce lifecycle event from a host platform service.
    /// </summary>
    /// <remarks>
    /// Accepted event types include <c>commerce.tenant.*</c> and
    /// <c>commerce.product.*</c> events from Identity and Tenant services.
    /// Unknown event types are accepted (logged at Debug level) to allow
    /// forward compatibility as new event types are introduced.
    /// </remarks>
    [HttpPost("lifecycle-events")]
    [ProducesResponseType(typeof(LifecycleEventAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult IngestLifecycleEvent(
        [FromBody] LifecycleEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
            return BadRequest(new { error = "eventType is required" });

        if (string.IsNullOrWhiteSpace(request.HostPlatformKey))
            return BadRequest(new { error = "hostPlatformKey is required" });

        if (string.IsNullOrWhiteSpace(request.ExternalTenantId))
            return BadRequest(new { error = "externalTenantId is required" });

        _logger.LogInformation(
            "Commerce lifecycle event accepted: {EventType} host={HostPlatformKey} tenant={ExternalTenantId} " +
            "product={ProductKey} occurredAt={OccurredAtUtc} correlationId={CorrelationId}",
            request.EventType,
            request.HostPlatformKey,
            request.ExternalTenantId,
            request.ProductKey ?? "(none)",
            request.OccurredAtUtc,
            request.CorrelationId ?? "(none)");

        return Accepted(new LifecycleEventAcceptedResponse(
            Accepted:         true,
            EventType:        request.EventType,
            ExternalTenantId: request.ExternalTenantId,
            ReceivedAtUtc:    DateTimeOffset.UtcNow));
    }
}

/// <summary>
/// Inbound lifecycle event request body — mirrors <c>CommerceLifecycleEvent</c>
/// from <c>shared/contracts</c> without introducing a cross-project reference.
/// Fields are kept in sync by contract; update when the shared event contract changes.
/// </summary>
public sealed class LifecycleEventRequest
{
    public string          EventType        { get; init; } = string.Empty;
    public string          HostPlatformKey  { get; init; } = string.Empty;
    public string          ExternalTenantId { get; init; } = string.Empty;
    public DateTimeOffset  OccurredAtUtc   { get; init; }
    public string?         CorrelationId   { get; init; }
    public string?         BillingAccountId { get; init; }
    public string?         SubscriptionId  { get; init; }
    public string?         ProductKey      { get; init; }
    public string?         PlanKey         { get; init; }
    public string?         AccessRecommendation { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Response returned by the lifecycle event ingest endpoint.
/// </summary>
public sealed record LifecycleEventAcceptedResponse(
    bool           Accepted,
    string         EventType,
    string         ExternalTenantId,
    DateTimeOffset ReceivedAtUtc);
