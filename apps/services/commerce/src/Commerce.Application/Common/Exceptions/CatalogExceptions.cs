namespace Commerce.Application.Common.Exceptions;

public abstract class CatalogException : Exception
{
    protected CatalogException(string message) : base(message) { }
}

public sealed class NotFoundException : CatalogException
{
    public string Resource { get; }
    public string? Identifier { get; }

    public NotFoundException(string resource, string? identifier = null)
        : base($"{resource} '{identifier}' was not found.")
    {
        Resource = resource;
        Identifier = identifier;
    }
}

public sealed class DuplicateKeyException : CatalogException
{
    public string Resource { get; }
    public string Key { get; }

    public DuplicateKeyException(string resource, string key)
        : base($"{resource} with key '{key}' already exists.")
    {
        Resource = resource;
        Key = key;
    }
}

public sealed class InvalidStateTransitionException : CatalogException
{
    public InvalidStateTransitionException(string message) : base(message) { }
}

public sealed class InvalidRelationshipException : CatalogException
{
    public InvalidRelationshipException(string message) : base(message) { }
}
