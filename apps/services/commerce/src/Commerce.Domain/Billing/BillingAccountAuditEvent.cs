using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Billing;

public sealed class BillingAccountAuditEvent : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public BillingAccountAuditActorType ActorType { get; private set; }
    public string? ActorId { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private BillingAccountAuditEvent() { }

    public static BillingAccountAuditEvent Create(
        Guid billingAccountId,
        string eventType,
        string description,
        BillingAccountAuditActorType actorType,
        string? actorId,
        string? metadataJson,
        DateTime nowUtc)
    {
        return new BillingAccountAuditEvent
        {
            Id = Guid.CreateVersion7(),
            BillingAccountId = billingAccountId,
            EventType = eventType.Trim(),
            Description = description.Trim(),
            ActorType = actorType,
            ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson,
            CreatedAtUtc = nowUtc
        };
    }
}

public static class BillingAccountAuditEventTypes
{
    public const string AccountCreated = "AccountCreated";
    public const string AccountUpdated = "AccountUpdated";
    public const string AccountActivated = "AccountActivated";
    public const string AccountSuspended = "AccountSuspended";
    public const string AccountClosed = "AccountClosed";
    public const string ExternalRefAdded = "ExternalRefAdded";
    public const string ExternalRefUpdated = "ExternalRefUpdated";
    public const string ExternalRefMadePrimary = "ExternalRefMadePrimary";
    public const string BillingContactAdded = "BillingContactAdded";
    public const string BillingContactUpdated = "BillingContactUpdated";
    public const string BillingContactMadePrimary = "BillingContactMadePrimary";
    public const string BillingProfileUpdated = "BillingProfileUpdated";
}
