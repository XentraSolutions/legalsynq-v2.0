using BuildingBlocks.Domain;

namespace Task.Domain.Entities;

public class TaskNote : AuditableEntity
{
    public Guid   Id                { get; private set; }
    public Guid   TaskId            { get; private set; }
    public Guid   TenantId          { get; private set; }
    public string Note              { get; private set; } = string.Empty;

    private TaskNote() { }

    public static TaskNote Create(
        Guid   taskId,
        Guid   tenantId,
        string note,
        Guid   createdByUserId)
    {
        if (taskId == Guid.Empty)          throw new ArgumentException("TaskId is required.", nameof(taskId));
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        var now = DateTime.UtcNow;
        return new TaskNote
        {
            Id              = Guid.NewGuid(),
            TaskId          = taskId,
            TenantId        = tenantId,
            Note            = note.Trim(),
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }
}
