using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Services;

public sealed class AuditEventService : IAuditEventService
{
    private readonly IAuditEventRepository    _repository;
    private readonly byte[]                   _hmacSecret;
    private readonly ILogger<AuditEventService> _logger;

    public AuditEventService(
        IAuditEventRepository    repository,
        IOptions<AuditServiceOptions> options,
        ILogger<AuditEventService>    logger)
    {
        _repository = repository;
        _logger     = logger;
        _hmacSecret = Convert.FromBase64String(options.Value.IntegrityHmacKeyBase64
            ?? throw new InvalidOperationException("AuditService:IntegrityHmacKeyBase64 is not configured."));
    }

    public async Task<AuditEventResponse> IngestAsync(IngestAuditEventRequest request, CancellationToken ct = default)
    {
        var model    = AuditEventMapper.ToModel(request, _hmacSecret);
        var persisted = await _repository.AppendAsync(model, ct);

        _logger.LogInformation(
            "AuditEvent ingested: Id={Id} Source={Source} EventType={EventType} TenantId={TenantId}",
            persisted.Id, persisted.Source, persisted.EventType, persisted.TenantId);

        return AuditEventMapper.ToResponse(persisted);
    }

    public async Task<AuditEventResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var evt = await _repository.GetByIdAsync(id, ct);
        return evt is null ? null : AuditEventMapper.ToResponse(evt);
    }

    public async Task<PagedResult<AuditEventResponse>> QueryAsync(AuditEventQueryRequest query, CancellationToken ct = default)
    {
        var result = await _repository.QueryAsync(query, ct);
        return new PagedResult<AuditEventResponse>
        {
            Items      = result.Items.Select(AuditEventMapper.ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
        };
    }

    public Task<long> CountAsync(CancellationToken ct = default) =>
        _repository.CountAsync(ct);
}
