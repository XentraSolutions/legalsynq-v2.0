using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly IAuditEventService _service;

    public HealthController(IAuditEventService service)
    {
        _service = service;
    }

    /// <summary>
    /// Health check endpoint. Returns service status and event count for diagnostics.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var count = await _service.CountAsync(ct);

        var payload = new HealthResponse
        {
            Status      = "Healthy",
            Service     = "Platform Audit/Event Service",
            Version     = "1.0.0",
            Timestamp   = DateTimeOffset.UtcNow,
            EventCount  = count,
        };

        return Ok(ApiResponse<HealthResponse>.Ok(payload, traceId: TraceIdAccessor.Current()));
    }
}

public sealed class HealthResponse
{
    public string         Status     { get; init; } = string.Empty;
    public string         Service    { get; init; } = string.Empty;
    public string         Version    { get; init; } = string.Empty;
    public DateTimeOffset Timestamp  { get; init; }
    public long           EventCount { get; init; }
}
