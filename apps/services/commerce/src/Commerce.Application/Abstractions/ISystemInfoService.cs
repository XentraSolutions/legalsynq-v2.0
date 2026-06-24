using Commerce.Contracts.System;

namespace Commerce.Application.Abstractions;

public interface ISystemInfoService
{
    SystemInfoResponse GetSystemInfo();
}
