namespace Reports.Contracts.Queue;

public interface IJobQueue
{
    Task EnqueueAsync(ReportJob job, CancellationToken ct = default);
    Task<ReportJob?> DequeueAsync(CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
}

public sealed class ReportJob
{
    public string JobId { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string ReportTypeCode { get; init; } = string.Empty;
    public IDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
