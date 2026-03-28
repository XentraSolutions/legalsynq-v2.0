using BuildingBlocks.Domain;

namespace Fund.Domain;

public class Application : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ApplicationNumber { get; private set; } = string.Empty;
    public string ApplicantFirstName { get; private set; } = string.Empty;
    public string ApplicantLastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;

    private Application() { }

    public static Application Create(
        Guid tenantId,
        string applicationNumber,
        string applicantFirstName,
        string applicantLastName,
        string email,
        string phone,
        Guid createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantLastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        var now = DateTime.UtcNow;
        return new Application
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationNumber = applicationNumber,
            ApplicantFirstName = applicantFirstName.Trim(),
            ApplicantLastName = applicantLastName.Trim(),
            Email = email.ToLowerInvariant().Trim(),
            Phone = phone.Trim(),
            Status = "New",
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
