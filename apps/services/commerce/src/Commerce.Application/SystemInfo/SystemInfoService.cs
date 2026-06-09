using Commerce.Application.Abstractions;
using Commerce.Contracts.System;
using Microsoft.Extensions.Hosting;

namespace Commerce.Application.SystemInfo;

internal sealed class SystemInfoService : ISystemInfoService
{
    private readonly IHostEnvironment _env;

    public SystemInfoService(IHostEnvironment env)
    {
        _env = env;
    }

    public SystemInfoResponse GetSystemInfo()
    {
        var version = typeof(SystemInfoService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        return new SystemInfoResponse(
            ServiceName: "Commerce",
            Version: version,
            Environment: _env.EnvironmentName,
            TimestampUtc: DateTime.UtcNow);
    }
}
