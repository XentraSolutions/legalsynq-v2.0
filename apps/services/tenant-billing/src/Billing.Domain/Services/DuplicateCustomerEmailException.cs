namespace Billing.Domain.Services;

/// <summary>
/// Thrown when a customer create/update would result in two active customers
/// in the same tenant sharing the same email. Same email across tenants is
/// allowed and does NOT trigger this exception.
/// </summary>
public sealed class DuplicateCustomerEmailException : Exception
{
    public Guid TenantId { get; }
    public string Email { get; }

    public DuplicateCustomerEmailException(Guid tenantId, string email)
        : base($"A customer with email '{email}' already exists for tenant {tenantId}.")
    {
        TenantId = tenantId;
        Email = email;
    }
}
