namespace Tenant.Domain;

public enum RegistrationStatus { PendingReview, Approved, Declined }
public enum RegistrationProvisioningStatus { NotStarted, InProgress, Provisioned, Failed }

public sealed class TenantRegistration
{
    private TenantRegistration() { }

    public Guid Id { get; private set; }
    public string TenantName { get; private set; } = null!;
    public string TenantCode { get; private set; } = null!;
    public string OrganizationType { get; private set; } = null!;
    public string? StreetAddress { get; private set; }
    public string AdminFirstName { get; private set; } = null!;
    public string AdminLastName { get; private set; } = null!;
    public string AdminEmail { get; private set; } = null!;
    public RegistrationStatus RegistrationStatus { get; private set; }
    public RegistrationProvisioningStatus ProvisioningStatus { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? ProvisioningHostname { get; private set; }
    public string? ProvisioningError { get; private set; }
    public string? ProvisioningFailureStage { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime? ProvisioningStartedAtUtc { get; private set; }
    public DateTime? ProvisionedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public static TenantRegistration Create(string tenantName, string tenantCode, string organizationType,
        string? streetAddress, string adminFirstName, string adminLastName, string adminEmail)
    {
        var now = DateTime.UtcNow;
        return new TenantRegistration
        {
            Id = Guid.CreateVersion7(), TenantName = tenantName, TenantCode = tenantCode,
            OrganizationType = organizationType, StreetAddress = streetAddress,
            AdminFirstName = adminFirstName, AdminLastName = adminLastName, AdminEmail = adminEmail,
            RegistrationStatus = RegistrationStatus.PendingReview,
            ProvisioningStatus = RegistrationProvisioningStatus.NotStarted,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
    }

    public void BeginApproval(Guid reviewerId)
    {
        if (RegistrationStatus == RegistrationStatus.Approved) return;
        if (RegistrationStatus != RegistrationStatus.PendingReview)
            throw new InvalidOperationException("Only pending registrations may be approved.");
        if (ProvisioningStatus == RegistrationProvisioningStatus.InProgress)
            throw new InvalidOperationException("Approval is already in progress.");
        ReviewedByUserId = reviewerId;
        ReviewedAtUtc = UpdatedAtUtc = DateTime.UtcNow;
        ProvisioningStatus = RegistrationProvisioningStatus.InProgress;
        ProvisioningStartedAtUtc = DateTime.UtcNow;
        ProvisioningError = null;
        ProvisioningFailureStage = null;
    }

    public void CompleteApproval(Guid tenantId, string? hostname, bool provisioned, string? error, string? failureStage)
    {
        TenantId = tenantId;
        RegistrationStatus = RegistrationStatus.Approved;
        ProvisioningHostname = hostname;
        ProvisioningStatus = provisioned ? RegistrationProvisioningStatus.Provisioned : RegistrationProvisioningStatus.Failed;
        ProvisioningError = error;
        ProvisioningFailureStage = failureStage;
        ProvisionedAtUtc = provisioned ? DateTime.UtcNow : null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ResetApprovalReservation()
    {
        if (RegistrationStatus != RegistrationStatus.PendingReview) return;
        ProvisioningStatus = RegistrationProvisioningStatus.NotStarted;
        ProvisioningStartedAtUtc = null;
        ReviewedByUserId = null;
        ReviewedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Decline(Guid reviewerId, string reason)
    {
        if (RegistrationStatus != RegistrationStatus.PendingReview)
            throw new InvalidOperationException("Only pending registrations may be declined.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A decline reason is required.", nameof(reason));
        RegistrationStatus = RegistrationStatus.Declined;
        DecisionReason = reason.Trim();
        ReviewedByUserId = reviewerId;
        ReviewedAtUtc = UpdatedAtUtc = DateTime.UtcNow;
    }

    public void BeginProvisioningRetry()
    {
        if (RegistrationStatus != RegistrationStatus.Approved || TenantId is null ||
            ProvisioningStatus == RegistrationProvisioningStatus.Provisioned)
            throw new InvalidOperationException("Provisioning can only be retried for an approved, incomplete registration.");
        ProvisioningStatus = RegistrationProvisioningStatus.InProgress;
        ProvisioningStartedAtUtc = UpdatedAtUtc = DateTime.UtcNow;
        ProvisioningError = null;
        ProvisioningFailureStage = null;
    }

    public void CompleteProvisioningRetry(bool success, string? hostname, string? error, string? failureStage)
    {
        ProvisioningStatus = success ? RegistrationProvisioningStatus.Provisioned : RegistrationProvisioningStatus.Failed;
        ProvisioningHostname = hostname ?? ProvisioningHostname;
        ProvisioningError = error;
        ProvisioningFailureStage = failureStage;
        ProvisionedAtUtc = success ? DateTime.UtcNow : null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
