using Commerce.Application.Abstractions;
using Commerce.Contracts.System;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/commerce/system")]
public sealed class SystemController : ControllerBase
{
    private readonly ISystemInfoService _systemInfo;

    public SystemController(ISystemInfoService systemInfo)
    {
        _systemInfo = systemInfo;
    }

    /// <summary>
    /// Returns safe, non-secret service metadata.
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(SystemInfoResponse), StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> GetInfo()
    {
        return Ok(_systemInfo.GetSystemInfo());
    }
}
