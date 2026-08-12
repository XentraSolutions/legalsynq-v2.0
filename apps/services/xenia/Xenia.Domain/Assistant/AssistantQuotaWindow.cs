using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantQuotaWindow : AuditableEntityBase
{
    public const int WindowKeyMaxLength = 100;

    private AssistantQuotaWindow() { }

    public AssistantQuotaWindow(
        Guid id,
        Guid tenantId,
        Guid? actorId,
        string windowKey,
        DateTime startsAtUtc,
        DateTime endsAtUtc)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Quota window id must not be empty.", nameof(id)) : id;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        ActorId = actorId;
        WindowKey = string.IsNullOrWhiteSpace(windowKey)
            ? throw new ArgumentException("Window key is required.", nameof(windowKey))
            : windowKey.Trim();
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc <= startsAtUtc
            ? throw new ArgumentException("Quota window end must be after start.", nameof(endsAtUtc))
            : endsAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string WindowKey { get; private set; } = string.Empty;
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public int RequestCount { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public uint RowVersion { get; private set; }

    public void AddUsage(int inputTokens, int outputTokens, decimal estimatedCostUsd)
    {
        RequestCount += 1;
        InputTokens += Math.Max(0, inputTokens);
        OutputTokens += Math.Max(0, outputTokens);
        EstimatedCostUsd += estimatedCostUsd < 0 ? 0 : estimatedCostUsd;
    }
}
