namespace Xenia.Domain.Common;

/// <summary>
/// Base class for Xenia domain entities that require auditable timestamps.
/// Inherit from this to get automatic CreatedAtUtc / UpdatedAtUtc stamping.
/// </summary>
public abstract class AuditableEntityBase : IAuditableEntity
{
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    void IAuditableEntity.SetCreatedAt(DateTime utc) => CreatedAtUtc = utc;
    void IAuditableEntity.SetUpdatedAt(DateTime utc) => UpdatedAtUtc = utc;
}
