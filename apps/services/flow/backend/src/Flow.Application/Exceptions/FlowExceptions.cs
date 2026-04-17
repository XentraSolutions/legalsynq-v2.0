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
