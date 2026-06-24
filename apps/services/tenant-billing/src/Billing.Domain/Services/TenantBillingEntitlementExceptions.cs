namespace Billing.Domain.Services;

/// <summary>
/// Thrown when an apply request's BillingAccountId does not match the profile
/// found for the tenant (i.e. the snapshot is for a different account).
/// </summary>
public sealed class TenantBillingEntitlementProfileMismatchException : Exception
{
    public TenantBillingEntitlementProfileMismatchException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the supplied raw snapshot JSON is not well-formed JSON.
/// Surfaced as 400 by the controller.
/// </summary>
public sealed class TenantBillingEntitlementInvalidJsonException : Exception
{
    public TenantBillingEntitlementInvalidJsonException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a snapshot is applied against a Closed profile. Closed
/// profiles are immutable per TB-DATA-01.
/// </summary>
public sealed class TenantBillingEntitlementClosedProfileException : Exception
{
    public TenantBillingEntitlementClosedProfileException(string message) : base(message) { }
}
