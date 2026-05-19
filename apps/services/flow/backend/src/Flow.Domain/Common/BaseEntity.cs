namespace Flow.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TenantId { get; set; } = string.Empty;
}
