using Commerce.Application.Admin.Abstractions;
using Commerce.Contracts.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Admin;

/// <summary>
/// Read-only admin dashboard endpoints. These power the standalone Commerce
/// Admin frontend and are intentionally projection-only (no writes, no
/// long-running queries). All routes are prefixed with
/// <c>api/commerce/admin/dashboard</c>.
/// </summary>
[ApiController]
[Route("api/commerce/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;

    public AdminDashboardController(IAdminDashboardService service)
        => _service = service;

    [HttpGet("summary")]
    [ProducesResponseType(typeof(AdminDashboardSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardSummaryResponse>> GetSummary(CancellationToken ct)
        => Ok(await _service.GetSummaryAsync(ct));

    [HttpGet("revenue-summary")]
    [ProducesResponseType(typeof(RevenueSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueSummaryResponse>> GetRevenueSummary(CancellationToken ct)
        => Ok(await _service.GetRevenueSummaryAsync(ct));

    [HttpGet("account-standing-summary")]
    [ProducesResponseType(typeof(AccountStandingSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountStandingSummaryResponse>> GetAccountStandingSummary(
        CancellationToken ct)
        => Ok(await _service.GetAccountStandingSummaryAsync(ct));

    [HttpGet("provider-event-summary")]
    [ProducesResponseType(typeof(ProviderEventSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProviderEventSummaryResponse>> GetProviderEventSummary(
        CancellationToken ct)
        => Ok(await _service.GetProviderEventSummaryAsync(ct));

    [HttpGet("recent-activity")]
    [ProducesResponseType(typeof(RecentActivityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecentActivityResponse>> GetRecentActivity(
        [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _service.GetRecentActivityAsync(take, ct));
}
