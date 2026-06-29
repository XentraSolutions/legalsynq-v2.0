using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class BatchUpload : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CaseId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string? BatchDate { get; private set; }
    public int Rows { get; private set; }
    public string DataContext { get; private set; } = string.Empty;
    public string Status { get; private set; } = "A";
    public string ProcessStatus { get; private set; } = "PENDING";
    public List<BatchUploadDetail> Details { get; private set; } = [];

    private BatchUpload() { }

    public static BatchUpload Create(
        Guid tenantId,
        Guid createdByUserId,
        string label,
        string template,
        string fileName,
        int rows,
        string dataContext,
        Guid? caseId = null,
        Guid? templateId = null,
        string? batchDate = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var now = DateTime.UtcNow;
        return new BatchUpload
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CaseId = caseId,
            TemplateId = templateId,
            Label = label.Trim(),
            Template = template.Trim(),
            FileName = fileName?.Trim() ?? string.Empty,
            BatchDate = string.IsNullOrWhiteSpace(batchDate) ? null : batchDate.Trim(),
            Rows = rows,
            DataContext = dataContext ?? string.Empty,
            Status = "A",
            ProcessStatus = "PENDING",
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        Guid updatedByUserId,
        string label,
        string template,
        string fileName,
        int rows,
        string dataContext,
        Guid? caseId = null,
        Guid? templateId = null,
        string? batchDate = null)
    {
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        CaseId = caseId;
        TemplateId = templateId;
        Label = label.Trim();
        Template = template.Trim();
        FileName = fileName?.Trim() ?? string.Empty;
        BatchDate = string.IsNullOrWhiteSpace(batchDate) ? null : batchDate.Trim();
        Rows = rows;
        DataContext = dataContext ?? string.Empty;
        ProcessStatus = "PENDING";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetProcessStatus(string processStatus, Guid updatedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processStatus);
        ProcessStatus = processStatus.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        Status = "D";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
