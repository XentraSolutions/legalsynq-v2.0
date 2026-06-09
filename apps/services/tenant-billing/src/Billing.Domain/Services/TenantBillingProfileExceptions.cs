namespace Billing.Domain.Services;

/// <summary>
/// Thrown when a profile cannot be created because the uniqueness invariants
/// would be violated (the tenant already has an open profile, or the target
/// BillingAccountId is already claimed by another open profile — possibly in
/// a different tenant).
/// </summary>
public sealed class TenantBillingProfileConflictException : Exception
{
    public TenantBillingProfileConflictException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a tenant-scoped lookup cannot find the profile id (either the
/// id is unknown or it belongs to a different tenant — both surface as 404).
/// </summary>
public sealed class TenantBillingProfileNotFoundException : Exception
{
    public TenantBillingProfileNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a lifecycle transition is not allowed from the profile's
/// current status (e.g. Suspend on a Closed profile).
/// </summary>
public sealed class InvalidTenantBillingProfileTransitionException : Exception
{
    public InvalidTenantBillingProfileTransitionException(string message) : base(message) { }
}
