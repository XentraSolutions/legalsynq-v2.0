using Commerce.Application.Common.Time;
using Commerce.Domain.Billing;
using Commerce.Domain.Billing.Enums;
using Commerce.Infrastructure.Persistence;

namespace Commerce.Infrastructure.Billing.Services;

/// <summary>
/// Internal helper used by every mutating billing service to append an
/// audit row inside the same DbContext SaveChanges as the mutation.
/// </summary>
public sealed class BillingAuditWriter
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;

    public BillingAuditWriter(CommerceDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public BillingAccountAuditEvent Append(
        Guid accountId,
        string eventType,
        string description,
        string? metadataJson = null,
        BillingAccountAuditActorType actorType = BillingAccountAuditActorType.System,
        string? actorId = null)
    {
        var evt = BillingAccountAuditEvent.Create(
            accountId, eventType, description, actorType, actorId, metadataJson, _clock.UtcNow);
        _db.BillingAccountAuditEvents.Add(evt);
        return evt;
    }
}
