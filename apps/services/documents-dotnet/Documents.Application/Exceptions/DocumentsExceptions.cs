namespace Documents.Application.Exceptions;

public abstract class DocumentsException : Exception
{
    public abstract int    StatusCode { get; }
    public abstract string ErrorCode  { get; }

    protected DocumentsException(string message) : base(message) { }
}

public sealed class NotFoundException : DocumentsException
{
    public override int    StatusCode => 404;
    public override string ErrorCode  => "NOT_FOUND";

    public NotFoundException(string resource, object id)
        : base($"{resource} not found: {id}") { }
}

public sealed class ForbiddenException : DocumentsException
{
    public override int    StatusCode => 403;
    public override string ErrorCode  => "ACCESS_DENIED";

    public ForbiddenException(string message) : base(message) { }
}

public sealed class ScanBlockedException : DocumentsException
{
    public override int    StatusCode => 403;
    public override string ErrorCode  => "SCAN_BLOCKED";

    public ScanBlockedException(string message) : base(message) { }
}

public sealed class InfectedFileException : DocumentsException
{
    public override int    StatusCode => 422;
    public override string ErrorCode  => "INFECTED_FILE";

    public InfectedFileException(string message) : base(message) { }
}

public sealed class UnsupportedFileTypeException : DocumentsException
{
    public override int    StatusCode => 422;
    public override string ErrorCode  => "UNSUPPORTED_FILE_TYPE";

    public UnsupportedFileTypeException(string message) : base(message) { }
}

public sealed class TenantIsolationException : DocumentsException
{
    public override int    StatusCode => 403;
    public override string ErrorCode  => "TENANT_ISOLATION_VIOLATION";

    public TenantIsolationException() : base("Tenant isolation violation") { }
}

public sealed class TokenExpiredException : DocumentsException
{
    public override int    StatusCode => 401;
    public override string ErrorCode  => "TOKEN_EXPIRED";

    public TokenExpiredException(string message) : base(message) { }
}

public sealed class TokenInvalidException : DocumentsException
{
    public override int    StatusCode => 401;
    public override string ErrorCode  => "TOKEN_INVALID";

    public TokenInvalidException(string message) : base(message) { }
}

public sealed class ValidationException : DocumentsException
{
    public override int    StatusCode => 400;
    public override string ErrorCode  => "VALIDATION_ERROR";

    public Dictionary<string, string[]> Details { get; }

    public ValidationException(Dictionary<string, string[]> details)
        : base("Request validation failed")
    {
        Details = details;
    }
}
