namespace Task.Domain.Entities;

public class TaskHistory
{
    public Guid     Id                  { get; private set; }
    public Guid     TaskId              { get; private set; }
    public Guid     TenantId            { get; private set; }
    public string   Action              { get; private set; } = string.Empty;
    public string?  Details             { get; private set; }
    public Guid     PerformedByUserId   { get; private set; }
    public DateTime CreatedAtUtc        { get; private set; }

    private TaskHistory() { }

    public static TaskHistory Record(
        Guid    taskId,
        Guid    tenantId,
        string  action,
        Guid    performedByUserId,
        string? details = null)
    {
        if (taskId == Guid.Empty)            throw new ArgumentException("TaskId is required.", nameof(taskId));
        if (tenantId == Guid.Empty)          throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (performedByUserId == Guid.Empty) throw new ArgumentException("PerformedByUserId is required.", nameof(performedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return new TaskHistory
        {
            Id                = Guid.NewGuid(),
            TaskId            = taskId,
            TenantId          = tenantId,
            Action            = action.Trim(),
            Details           = details?.Trim(),
            PerformedByUserId = performedByUserId,
            CreatedAtUtc      = DateTime.UtcNow,
        };
    }
}

public static class TaskActions
{
    public const string Created        = "TASK_CREATED";
    public const string Updated        = "TASK_UPDATED";
    public const string StatusChanged  = "STATUS_CHANGED";
    public const string Assigned       = "ASSIGNED";
    public const string NoteAdded      = "NOTE_ADDED";
    public const string Completed      = "COMPLETED";
    public const string Cancelled      = "CANCELLED";
}
