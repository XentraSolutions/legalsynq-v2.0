namespace Support.Api.Domain;

public class ExternalCustomer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TenantId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public ExternalCustomerStatus Status { get; set; } = ExternalCustomerStatus.Active;
    public DateTime CreatedAt { get; set; }
}
