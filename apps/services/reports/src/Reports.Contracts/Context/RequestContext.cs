namespace Reports.Contracts.Context;

public sealed class RequestContext
{
    public string CorrelationId { get; init; } = Guid.CreateVersion7().ToString();
    public string RequestId { get; init; } = Guid.CreateVersion7().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string>? Metadata { get; init; }

    public static RequestContext Default() => new();
}
