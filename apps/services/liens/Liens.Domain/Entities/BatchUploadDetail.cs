using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class BatchUploadDetail : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BatchUploadId { get; private set; }
    public int RowNumber { get; private set; }
    public string Status { get; private set; } = "PENDING";
    public string? Reason { get; private set; }
    public string DataJson { get; private set; } = "{}";
    public string RecordStatus { get; private set; } = "A";

    private BatchUploadDetail() { }

    public static BatchUploadDetail Create(
        Guid tenantId,
        Guid batchUploadId,
        int rowNumber,
        string dataJson,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (batchUploadId == Guid.Empty) throw new ArgumentException("BatchUploadId is required.", nameof(batchUploadId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new BatchUploadDetail
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BatchUploadId = batchUploadId,
            RowNumber = rowNumber,
            Status = "PENDING",
            DataJson = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson.Trim(),
            RecordStatus = "A",
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void SetResult(string status, string? reason, Guid updatedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        Status = status.Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        RecordStatus = "D";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
