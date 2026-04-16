using Microsoft.EntityFrameworkCore;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Domain.Enums;
using SynqComm.Infrastructure.Persistence;

namespace SynqComm.Infrastructure.Repositories;

public class EmailTemplateConfigRepository : IEmailTemplateConfigRepository
{
    private readonly SynqCommDbContext _db;

    public EmailTemplateConfigRepository(SynqCommDbContext db)
    {
        _db = db;
    }

    public async Task<EmailTemplateConfig?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.EmailTemplateConfigs.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<EmailTemplateConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.EmailTemplateConfigs
            .Where(t => t.TenantId == tenantId || t.TemplateScope == TemplateScope.Global)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.TemplateKey)
            .ToListAsync(ct);

    public async Task<EmailTemplateConfig?> GetByKeyAsync(Guid tenantId, string templateKey, CancellationToken ct = default)
    {
        var normalized = templateKey.Trim().ToLowerInvariant();
        return await _db.EmailTemplateConfigs
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId &&
                t.TemplateKey == normalized &&
                t.IsActive, ct);
    }

    public async Task<EmailTemplateConfig?> GetGlobalByKeyAsync(string templateKey, CancellationToken ct = default)
    {
        var normalized = templateKey.Trim().ToLowerInvariant();
        return await _db.EmailTemplateConfigs
            .FirstOrDefaultAsync(t =>
                t.TemplateScope == TemplateScope.Global &&
                t.TemplateKey == normalized &&
                t.IsActive, ct);
    }

    public async Task<EmailTemplateConfig?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.EmailTemplateConfigs
            .FirstOrDefaultAsync(t =>
                (t.TenantId == tenantId || t.TemplateScope == TemplateScope.Global) &&
                t.IsDefault && t.IsActive, ct);

    public async Task AddAsync(EmailTemplateConfig config, CancellationToken ct = default) =>
        await _db.EmailTemplateConfigs.AddAsync(config, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
