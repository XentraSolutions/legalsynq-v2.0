namespace Liens.Application.Interfaces;

public interface IAuditPublisher
{
    IAuditPublicationBuffer BeginBuffer();

    void Publish(
        string eventType,
        string action,
        string description,
        Guid tenantId,
        Guid? actorUserId = null,
        string? entityType = null,
        string? entityId = null,
        string? before = null,
        string? after = null,
        string? metadata = null);
}

public interface IAuditPublicationBuffer : IDisposable
{
    void Commit();
}
