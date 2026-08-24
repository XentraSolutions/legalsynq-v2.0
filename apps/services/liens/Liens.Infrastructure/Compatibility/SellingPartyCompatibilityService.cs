using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Liens.Infrastructure.Compatibility;

public sealed class SellingPartyCompatibilityService : ISellingPartyCompatibilityService
{
    private readonly LiensDbContext _db;
    private readonly SellingPartyCompatibilityOptions _options;

    public SellingPartyCompatibilityService(
        LiensDbContext db,
        IOptions<SellingPartyCompatibilityOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public bool DualWriteEnabled => _options.DualWriteEnabled;
    public bool ShadowReadEnabled => _options.ShadowReadEnabled;
    public bool CanonicalReadEnabled => _options.CanonicalReadEnabled;

    public async Task<SellingPartyCanonicalReference?> ResolveAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, CancellationToken ct = default)
    {
        var alias = await _db.SellingPartyAliases.AsNoTracking().SingleOrDefaultAsync(a =>
            a.TenantId == tenantId && a.ScopeKind == scopeKind && a.ScopeId == scopeId &&
            a.Namespace == aliasNamespace && a.WorkflowProvenance == workflowProvenance &&
            a.ExternalId == externalId, ct);
        return alias is null
            ? null
            : new SellingPartyCanonicalReference(alias.CompanyId, alias.CompanyContactPersonId);
    }

    public Task EnsureCompanyAliasAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid companyId,
        bool isPreferred, Guid actorUserId, CancellationToken ct = default)
        => EnsureAliasAsync(tenantId, scopeKind, scopeId, aliasNamespace, workflowProvenance,
            externalId, companyId, null, isPreferred, actorUserId, ct);

    public Task EnsureContactPersonAliasAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid companyContactPersonId,
        bool isPreferred, Guid actorUserId, CancellationToken ct = default)
        => EnsureAliasAsync(tenantId, scopeKind, scopeId, aliasNamespace, workflowProvenance,
            externalId, null, companyContactPersonId, isPreferred, actorUserId, ct);

    private async Task EnsureAliasAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid? companyId,
        Guid? companyContactPersonId, bool isPreferred, Guid actorUserId, CancellationToken ct)
    {
        ValidateScope(tenantId, scopeKind, scopeId);
        if (companyId.HasValue)
        {
            var company = await _db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId.Value, ct)
                ?? throw new InvalidOperationException("The canonical company target does not exist.");
            ValidateCompanyScope(company, tenantId, scopeKind, scopeId);
        }
        else
        {
            var contact = await _db.CompanyContactPersons.AsNoTracking()
                .Include(c => c.Company)
                .SingleOrDefaultAsync(c => c.Id == companyContactPersonId!.Value, ct)
                ?? throw new InvalidOperationException("The canonical contact-person target does not exist.");
            if (contact.Company is null || contact.TenantId != contact.Company.TenantId)
                throw new InvalidOperationException("The canonical contact person is not consistent with its parent company.");
            ValidateCompanyScope(contact.Company, tenantId, scopeKind, scopeId);
        }

        var existing = await _db.SellingPartyAliases.SingleOrDefaultAsync(a =>
            a.TenantId == tenantId && a.ScopeKind == scopeKind && a.ScopeId == scopeId &&
            a.Namespace == aliasNamespace && a.WorkflowProvenance == workflowProvenance &&
            a.ExternalId == externalId, ct);
        if (existing is not null)
        {
            if (existing.CompanyId != companyId || existing.CompanyContactPersonId != companyContactPersonId ||
                (isPreferred && !existing.IsPreferred))
            {
                throw new InvalidOperationException("The immutable selling-party alias is already assigned to a different canonical target or preference.");
            }
            return;
        }

        if (isPreferred)
        {
            var preferredExists = await _db.SellingPartyAliases.AsNoTracking().AnyAsync(a =>
                a.TenantId == tenantId && a.ScopeKind == scopeKind && a.ScopeId == scopeId &&
                a.Namespace == aliasNamespace && a.WorkflowProvenance == workflowProvenance &&
                a.IsPreferred && a.CompanyId == companyId &&
                a.CompanyContactPersonId == companyContactPersonId, ct);
            if (preferredExists)
                throw new InvalidOperationException("A preferred alias already exists for this canonical target and workflow scope.");
        }

        var alias = companyId.HasValue
            ? SellingPartyAlias.CreateForCompany(tenantId, scopeKind, scopeId, aliasNamespace,
                workflowProvenance, externalId, companyId.Value, isPreferred, actorUserId)
            : SellingPartyAlias.CreateForContactPerson(tenantId, scopeKind, scopeId, aliasNamespace,
                workflowProvenance, externalId, companyContactPersonId!.Value, isPreferred, actorUserId);
        _db.SellingPartyAliases.Add(alias);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            _db.Entry(alias).State = EntityState.Detached;
            var winner = await _db.SellingPartyAliases.AsNoTracking().SingleOrDefaultAsync(a =>
                a.TenantId == tenantId && a.ScopeKind == scopeKind && a.ScopeId == scopeId &&
                a.Namespace == aliasNamespace && a.WorkflowProvenance == workflowProvenance &&
                a.ExternalId == externalId, ct);
            if (winner is not null && winner.CompanyId == companyId && winner.CompanyContactPersonId == companyContactPersonId &&
                (!isPreferred || winner.IsPreferred))
                return;
            throw new InvalidOperationException("The alias or preferred canonical target was claimed concurrently.", ex);
        }
    }

    public async Task<int> RunBackfillBatchAsync(Guid tenantId, Guid actorUserId, CancellationToken ct = default)
    {
        if (!_options.BackfillEnabled) return 0;

        try
        {
            return await RunBackfillBatchCoreAsync(tenantId, actorUserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordBackfillFailureAsync(tenantId, actorUserId, ex, ct);
            throw;
        }
    }

    private async Task<int> RunBackfillBatchCoreAsync(Guid tenantId, Guid actorUserId, CancellationToken ct)
    {

        var checkpoint = await _db.SellingPartyBackfillCheckpoints.SingleOrDefaultAsync(
            c => c.TenantId == tenantId && c.Workflow == SellingPartyWorkflows.CompanyDirectory, ct);
        if (checkpoint is null)
        {
            checkpoint = SellingPartyBackfillCheckpoint.Create(
                tenantId, SellingPartyWorkflows.CompanyDirectory, actorUserId);
            _db.SellingPartyBackfillCheckpoints.Add(checkpoint);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                _db.Entry(checkpoint).State = EntityState.Detached;
                checkpoint = await _db.SellingPartyBackfillCheckpoints.SingleAsync(
                    c => c.TenantId == tenantId && c.Workflow == SellingPartyWorkflows.CompanyDirectory, ct);
            }
        }

        var batchSize = Math.Clamp(_options.BackfillBatchSize, 1, 1000);
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id.CompareTo(checkpoint.LastExternalId) > 0)
            .OrderBy(c => c.Id)
            .Take(batchSize)
            .Select(c => new { c.Id, c.OrgId, c.CreatedByUserId })
            .ToListAsync(ct);

        foreach (var company in companies)
        {
            await EnsureCompanyAliasAsync(tenantId, SellingPartyAliasScopes.Organization,
                company.OrgId, SellingPartyAliasNamespaces.IdentityOrganization,
                SellingPartyWorkflows.CompanyDirectory, company.Id, company.Id,
                true, company.CreatedByUserId ?? actorUserId, ct);
        }

        var lastId = companies.Count == 0 ? checkpoint.LastExternalId : companies[^1].Id;
        checkpoint.Advance(lastId, companies.Count, 0, companies.Count < batchSize, actorUserId);
        await _db.SaveChangesAsync(ct);
        return companies.Count;
    }

    private async Task RecordBackfillFailureAsync(
        Guid tenantId, Guid actorUserId, Exception exception, CancellationToken ct)
    {
        try
        {
            _db.ChangeTracker.Clear();
            var checkpoint = await _db.SellingPartyBackfillCheckpoints.SingleOrDefaultAsync(
                c => c.TenantId == tenantId && c.Workflow == SellingPartyWorkflows.CompanyDirectory, ct);
            if (checkpoint is null) return;
            checkpoint.Fail(exception.Message, actorUserId);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Preserve the original backfill failure when the database is also unavailable for checkpointing.
        }
    }

    private static void ValidateScope(Guid tenantId, string scopeKind, Guid scopeId)
    {
        if (tenantId == Guid.Empty || scopeId == Guid.Empty)
            throw new ArgumentException("Tenant and scope IDs are required.");
        if (scopeKind == SellingPartyAliasScopes.Tenant && scopeId != tenantId)
            throw new InvalidOperationException("Tenant-scoped aliases must use the owning tenant as ScopeId.");
        if (scopeKind is not (SellingPartyAliasScopes.Tenant or SellingPartyAliasScopes.Organization))
            throw new InvalidOperationException($"Unsupported selling-party alias scope '{scopeKind}'.");
    }

    private static void ValidateCompanyScope(Company company, Guid tenantId, string scopeKind, Guid scopeId)
    {
        if (company.TenantId != tenantId)
            throw new InvalidOperationException("The canonical target belongs to a different tenant.");
        if (scopeKind == SellingPartyAliasScopes.Organization && company.OrgId != scopeId)
            throw new InvalidOperationException("The canonical target belongs to a different organization scope.");
    }

    internal static bool IsDuplicateKey(DbUpdateException exception)
        => exception.InnerException is MySqlException { Number: 1062 };
}
