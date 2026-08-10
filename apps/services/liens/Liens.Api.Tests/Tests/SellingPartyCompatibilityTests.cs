using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Infrastructure.Compatibility;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Liens.Api.Tests.Tests;

public sealed class SellingPartyCompatibilityTests
{
    [Fact]
    public async Task Alias_resolution_is_tenant_namespace_scope_and_workflow_specific()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-party-alias-{Guid.CreateVersion7()}").Options;
        await using var db = new LiensDbContext(options);
        var tenantId = Guid.CreateVersion7();
        var orgId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var company = Company.Create(tenantId, orgId, CompanyDirectoryReferenceData.FundingCompanyId,
            "Compatibility Capital", actorId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var service = new SellingPartyCompatibilityService(db,
            Options.Create(new SellingPartyCompatibilityOptions()));
        var legacyId = Guid.CreateVersion7();
        await service.EnsureCompanyAliasAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.LegacyContact,
            SellingPartyWorkflows.SellingCaseInformation, legacyId, company.Id, true, actorId);

        var resolved = await service.ResolveAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.LegacyContact,
            SellingPartyWorkflows.SellingCaseInformation, legacyId);
        resolved.Should().Be(new Liens.Application.Interfaces.SellingPartyCanonicalReference(company.Id, null));

        var wrongWorkflow = await service.ResolveAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.LegacyContact,
            SellingPartyWorkflows.SellingPreparation, legacyId);
        wrongWorkflow.Should().BeNull();
    }

    [Fact]
    public async Task Existing_alias_cannot_be_reassigned_or_promoted()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-party-immutable-{Guid.CreateVersion7()}").Options;
        await using var db = new LiensDbContext(options);
        var tenantId = Guid.CreateVersion7();
        var orgId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var first = Company.Create(tenantId, orgId, CompanyDirectoryReferenceData.LawFirmId, "First Law", actorId);
        var second = Company.Create(tenantId, orgId, CompanyDirectoryReferenceData.LawFirmId, "Second Law", actorId);
        db.Companies.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new SellingPartyCompatibilityService(db,
            Options.Create(new SellingPartyCompatibilityOptions()));
        var legacyId = Guid.CreateVersion7();

        await service.EnsureCompanyAliasAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.LegacyContact, SellingPartyWorkflows.LegacyContact,
            legacyId, first.Id, false, actorId);

        var action = () => service.EnsureCompanyAliasAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.LegacyContact, SellingPartyWorkflows.LegacyContact,
            legacyId, second.Id, false, actorId);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Preferred_alias_is_unique_for_canonical_target_and_scope()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-party-preferred-{Guid.CreateVersion7()}").Options;
        await using var db = new LiensDbContext(options);
        var tenantId = Guid.CreateVersion7();
        var orgId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var company = Company.Create(tenantId, orgId, CompanyDirectoryReferenceData.LawFirmId,
            "Preferred Law", actorId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var service = new SellingPartyCompatibilityService(db,
            Options.Create(new SellingPartyCompatibilityOptions()));

        await service.EnsureCompanyAliasAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.LegacyContact, SellingPartyWorkflows.LegacyContact,
            Guid.CreateVersion7(), company.Id, true, actorId);
        var duplicatePreferred = () => service.EnsureCompanyAliasAsync(
            tenantId, SellingPartyAliasScopes.Organization, orgId,
            SellingPartyAliasNamespaces.LegacyContact, SellingPartyWorkflows.LegacyContact,
            Guid.CreateVersion7(), company.Id, true, actorId);

        await duplicatePreferred.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Alias_rejects_cross_tenant_and_cross_organization_targets()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-party-owner-{Guid.CreateVersion7()}").Options;
        await using var db = new LiensDbContext(options);
        var owningTenantId = Guid.CreateVersion7();
        var owningOrgId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var company = Company.Create(owningTenantId, owningOrgId,
            CompanyDirectoryReferenceData.MedicalProviderId, "Owner Medical", actorId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var service = new SellingPartyCompatibilityService(db,
            Options.Create(new SellingPartyCompatibilityOptions()));

        var crossTenant = () => service.EnsureCompanyAliasAsync(Guid.CreateVersion7(),
            SellingPartyAliasScopes.Organization, owningOrgId,
            SellingPartyAliasNamespaces.LegacyContact, SellingPartyWorkflows.LegacyContact,
            Guid.CreateVersion7(), company.Id, true, actorId);
        var crossOrg = () => service.EnsureCompanyAliasAsync(owningTenantId,
            SellingPartyAliasScopes.Organization, Guid.CreateVersion7(),
            SellingPartyAliasNamespaces.LegacyContact, SellingPartyWorkflows.LegacyContact,
            Guid.CreateVersion7(), company.Id, true, actorId);

        await crossTenant.Should().ThrowAsync<InvalidOperationException>();
        await crossOrg.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Backfill_failure_is_recorded_on_the_resumable_checkpoint()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"selling-party-backfill-failure-{Guid.CreateVersion7()}").Options;
        await using var db = new LiensDbContext(options);
        var tenantId = Guid.CreateVersion7();
        var orgId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var first = Company.Create(tenantId, orgId, CompanyDirectoryReferenceData.LawFirmId,
            "Backfill First", actorId);
        var second = Company.Create(tenantId, orgId, CompanyDirectoryReferenceData.LawFirmId,
            "Backfill Second", actorId);
        db.Companies.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new SellingPartyCompatibilityService(db,
            Options.Create(new SellingPartyCompatibilityOptions { BackfillEnabled = true }));
        await service.EnsureCompanyAliasAsync(tenantId, SellingPartyAliasScopes.Organization,
            orgId, SellingPartyAliasNamespaces.IdentityOrganization,
            SellingPartyWorkflows.CompanyDirectory, second.Id, first.Id, true, actorId);

        var action = () => service.RunBackfillBatchAsync(tenantId, actorId);

        await action.Should().ThrowAsync<InvalidOperationException>();
        var checkpoint = await db.SellingPartyBackfillCheckpoints.AsNoTracking().SingleAsync();
        checkpoint.Status.Should().Be(SellingPartyBackfillStatuses.Failed);
        checkpoint.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void All_rollout_flags_default_off()
    {
        var options = new SellingPartyCompatibilityOptions();
        options.BackfillEnabled.Should().BeFalse();
        options.DualWriteEnabled.Should().BeFalse();
        options.ShadowReadEnabled.Should().BeFalse();
        options.CanonicalReadEnabled.Should().BeFalse();
    }
}
