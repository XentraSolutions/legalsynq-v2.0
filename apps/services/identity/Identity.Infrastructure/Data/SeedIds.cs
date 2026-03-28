namespace Identity.Infrastructure.Data;

internal static class SeedIds
{
    public static readonly DateTime SeededAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Products
    public static readonly Guid ProductSynqFund        = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ProductSynqLiens       = new("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ProductSynqCareConnect = new("10000000-0000-0000-0000-000000000003");
    public static readonly Guid ProductSynqPay         = new("10000000-0000-0000-0000-000000000004");
    public static readonly Guid ProductSynqAI          = new("10000000-0000-0000-0000-000000000005");

    // Tenant
    public static readonly Guid TenantLegalSynq = new("20000000-0000-0000-0000-000000000001");

    // Roles
    public static readonly Guid RolePlatformAdmin = new("30000000-0000-0000-0000-000000000001");
    public static readonly Guid RoleTenantAdmin   = new("30000000-0000-0000-0000-000000000002");
    public static readonly Guid RoleStandardUser  = new("30000000-0000-0000-0000-000000000003");
}
