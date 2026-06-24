using Commerce.Domain.Common;

namespace Commerce.Domain.Infrastructure;

/// <summary>
/// Infrastructure-only marker entity used to anchor the Commerce EF Core schema.
/// Does NOT represent a domain concept. Future Commerce modules add their own tables;
/// this row simply allows the baseline migration to exist and provides a
/// schema-version anchor.
/// </summary>
public sealed class CommerceSchemaMarker : Entity<int>
{
    public string SchemaName { get; private set; } = "commerce";
    public string SchemaVersion { get; private set; } = "1.0.0";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private CommerceSchemaMarker() { }

    public CommerceSchemaMarker(int id, string schemaName, string schemaVersion, DateTime createdAtUtc)
    {
        Id = id;
        SchemaName = schemaName;
        SchemaVersion = schemaVersion;
        CreatedAtUtc = createdAtUtc;
    }
}
