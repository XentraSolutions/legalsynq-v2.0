namespace Reports.Contracts.Adapters;

public interface IAuditAdapter
{
    Task RecordEventAsync(string tenantId, string userId, string action, string description, CancellationToken ct = default);
}
