namespace Commerce.Contracts.System;

public sealed record SystemInfoResponse(
    string ServiceName,
    string Version,
    string Environment,
    DateTime TimestampUtc);
