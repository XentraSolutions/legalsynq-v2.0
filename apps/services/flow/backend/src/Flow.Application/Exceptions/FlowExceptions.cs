namespace Flow.Application.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}

public class ValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(string error) : base(error)
    {
        Errors = new List<string> { error };
    }

    public ValidationException(IEnumerable<string> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors.ToList();
    }
}

public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string from, string to)
        : base($"Invalid state transition from '{from}' to '{to}'.") { }
}

/// <summary>
/// LS-FLOW-MERGE-P5 — raised by the workflow engine when an
/// advance/complete/cancel call cannot be honoured because the instance
/// is not in the expected state, the transition does not exist, or the
/// instance has already reached a terminal status. Mapped to HTTP 409 by
/// the API surface.
/// </summary>
public class InvalidWorkflowTransitionException : Exception
{
    public string Code { get; }

    public InvalidWorkflowTransitionException(string message, string code = "invalid_transition")
        : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// LS-FLOW-E11.4 — raised by
/// <see cref="Interfaces.IWorkflowTaskLifecycleService"/> when an atomic
/// status compare-and-swap fails because the row was modified between
/// the pre-check read and the conditional UPDATE. Callers may safely
/// re-read the task and retry. Mapped to HTTP 409 by the API surface.
/// </summary>
public class WorkflowTaskConcurrencyException : Exception
{
    public Guid TaskId { get; }
    public string ExpectedStatus { get; }

    public WorkflowTaskConcurrencyException(Guid taskId, string expectedStatus)
        : base($"WorkflowTask '{taskId}' status changed concurrently (expected '{expectedStatus}'). " +
               "Re-read the task and retry the transition.")
    {
        TaskId = taskId;
        ExpectedStatus = expectedStatus;
    }
}
