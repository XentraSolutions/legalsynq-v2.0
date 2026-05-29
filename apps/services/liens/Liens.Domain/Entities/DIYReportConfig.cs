using BuildingBlocks.Domain;
using System.Text.Json;

namespace Liens.Domain.Entities;

/// <summary>
/// Stores a user-saved DIY report configuration (filters, columns, sort order).
/// The full filter JSON is stored as a text column for flexibility.
/// </summary>
public class DIYReportConfig : AuditableEntity
{
    public Guid   Id       { get; private set; }
    public Guid   TenantId { get; private set; }
    public Guid   UserId   { get; private set; }
    public string Name     { get; private set; } = string.Empty;

    /// <summary>JSON blob of the report filter/column configuration.</summary>
    public string ConfigJson { get; private set; } = "{}";

    public bool IsDeleted { get; private set; }

    private DIYReportConfig() { }

    public static DIYReportConfig Create(
        Guid tenantId, Guid userId, string name,
        string configJson, Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)   throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (userId == Guid.Empty)     throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new DIYReportConfig
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            UserId          = userId,
            Name            = name.Trim(),
            ConfigJson      = configJson,
            IsDeleted       = false,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(string name, string configJson, Guid updatedByUserId)
    {
        Name            = name.Trim();
        ConfigJson      = configJson;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SoftDelete(Guid updatedByUserId)
    {
        IsDeleted       = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
