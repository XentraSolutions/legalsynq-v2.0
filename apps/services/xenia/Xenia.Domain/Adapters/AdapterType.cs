namespace Xenia.Domain.Adapters;

/// <summary>
/// Canonical adapter type identifiers.
/// Each type corresponds to one platform capability that Xenia accesses
/// through an adapter interface rather than a direct dependency.
/// </summary>
public enum AdapterType
{
    Tenant,
    Identity,
    Document,
    Audit,
    Notification,
    Storage,
    Workflow,
    Ai,
}
