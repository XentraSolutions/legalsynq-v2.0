namespace Xenia.Domain.Common;

/// <summary>
/// Marks a domain entity as having auditable timestamps.
/// The Xenia DbContext intercepts SaveChanges to stamp these automatically.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }
    DateTime UpdatedAtUtc { get; }

    void SetCreatedAt(DateTime utc);
    void SetUpdatedAt(DateTime utc);
}
