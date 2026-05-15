namespace Commerce.Application.Common.Exceptions;

/// <summary>
/// Raised when a primary-uniqueness invariant is violated (e.g. trying
/// to demote the only primary external ref via an update). Mapped by the
/// ProblemDetails middleware as 422 — same code as
/// <see cref="InvalidRelationshipException"/>.
/// </summary>
public sealed class InvalidPrimaryReferenceException : CatalogException
{
    public InvalidPrimaryReferenceException(string message) : base(message) { }
}
