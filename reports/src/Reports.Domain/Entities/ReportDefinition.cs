namespace Reports.Domain.Entities;

public sealed class ReportDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
