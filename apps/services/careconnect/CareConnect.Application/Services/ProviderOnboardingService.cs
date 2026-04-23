// BLK-CC-01: Provider tenant self-onboarding service — rewired to new architecture.
//
// Old flow: CareConnect → Identity (retired endpoints, BLK-ID-01)
// New flow: CareConnect → Tenant service (provision) → Identity membership (assign-tenant)
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

/// <summary>
/// Orchestrates provider self-onboarding:
///  1. Validates the provider exists and is at COMMON_PORTAL stage.
///  2. Calls Tenant service to provision a new tenant (BLK-CC-01).
///  3. Calls Identity to assign the existing user to the new tenant (BLK-ID-02).
///  4. Calls provider.MarkTenantProvisioned(newTenantId).
///  5. Persists the change.
///  6. Returns tenant details (including portal URL).
///
/// Provider is only transitioned to TENANT stage after BOTH Tenant provisioning
/// AND Identity membership assignment succeed.
/// </summary>
public sealed class ProviderOnboardingService : IProviderOnboardingService
{
    private readonly IProviderRepository          _providerRepo;
    private readonly ITenantServiceClient         _tenantClient;
    private readonly IIdentityMembershipClient    _identityMembership;
    private readonly ILogger<ProviderOnboardingService> _logger;

    public ProviderOnboardingService(
        IProviderRepository               providerRepo,
        ITenantServiceClient              tenantClient,
        IIdentityMembershipClient         identityMembership,
        ILogger<ProviderOnboardingService> logger)
    {
        _providerRepo       = providerRepo;
        _tenantClient       = tenantClient;
        _identityMembership = identityMembership;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<ProviderOnboardingCodeCheckResult?> CheckCodeAvailableAsync(
        string            code,
        CancellationToken ct = default)
    {
        var result = await _tenantClient.CheckCodeAsync(code, ct);
        if (result is null) return null;

        return new ProviderOnboardingCodeCheckResult
        {
            Available      = result.Available,
            NormalizedCode = result.NormalizedCode,
            Message        = result.Message,
        };
    }

    /// <inheritdoc />
    public async Task<ProviderOnboardingResult> ProvisionToTenantAsync(
        Guid              identityUserId,
        string            tenantName,
        string            tenantCode,
        CancellationToken ct = default)
    {
        // ── 1. Find the provider by Identity user ID ──────────────────────────
        var provider = await _providerRepo.GetByIdentityUserIdAsync(identityUserId, ct);
        if (provider is null)
        {
            _logger.LogWarning(
                "BLK-CC-01 OnboardingFailed: no provider found for IdentityUserId={UserId}.",
                identityUserId);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.ProviderNotFound,
                "No provider record is linked to your account. Contact platform support.");
        }

        // ── 2. Guard: must be COMMON_PORTAL ───────────────────────────────────
        if (!ProviderAccessStage.IsAtLeast(provider.AccessStage, ProviderAccessStage.CommonPortal) ||
            provider.AccessStage == ProviderAccessStage.Tenant)
        {
            _logger.LogWarning(
                "BLK-CC-01 OnboardingFailed: provider {ProviderId} is at stage '{Stage}', expected COMMON_PORTAL.",
                provider.Id, provider.AccessStage);

            var msg = provider.AccessStage == ProviderAccessStage.Tenant
                ? "Your account has already been provisioned to a tenant workspace."
                : "Your account must be at the COMMON_PORTAL stage before setting up a workspace. Complete your portal activation first.";

            throw new ProviderOnboardingException(ProviderOnboardingErrorCode.WrongAccessStage, msg);
        }

        // ── 3. Tenant service: create new tenant ──────────────────────────────
        var provision = await _tenantClient.ProvisionAsync(tenantName, tenantCode, ct);

        if (provision is null)
        {
            _logger.LogError(
                "BLK-CC-01 OnboardingFailed: Tenant service ProvisionAsync returned null " +
                "(unexpected failure) for provider {ProviderId}.", provider.Id);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.IdentityServiceFailed,
                "Unable to create your workspace at this time. Please try again or contact support.");
        }

        if (!provision.IsSuccess)
        {
            if (provision.FailureCode == "CODE_TAKEN")
            {
                _logger.LogWarning(
                    "BLK-CC-01 OnboardingFailed: tenant code '{TenantCode}' already taken " +
                    "for provider {ProviderId}.", tenantCode, provider.Id);
                throw new ProviderOnboardingException(
                    ProviderOnboardingErrorCode.TenantCodeUnavailable,
                    $"The subdomain '{tenantCode}' is already taken. Please choose a different code.");
            }

            _logger.LogError(
                "BLK-CC-01 OnboardingFailed: Tenant service ProvisionAsync returned failure " +
                "code '{FailureCode}' for provider {ProviderId}.", provision.FailureCode, provider.Id);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.IdentityServiceFailed,
                "Unable to create your workspace at this time. Please try again or contact support.");
        }

        // ── 4. Identity: assign existing user to new tenant ───────────────────
        // DO NOT create a new Identity user. Reuse the existing provider.IdentityUserId.
        // Provider only advances to TENANT after this step succeeds.
        var membership = await _identityMembership.AssignTenantAsync(
            identityUserId,
            provision.TenantId,
            ["TenantAdmin"],
            ct);

        if (membership is null)
        {
            _logger.LogError(
                "BLK-CC-01 OnboardingFailed: Identity membership AssignTenant returned null " +
                "for provider {ProviderId} (TenantId={TenantId}). " +
                "WARNING: Tenant is provisioned but provider remains at COMMON_PORTAL — " +
                "partial failure state. Retry will get CODE_TAKEN. Phase 2 recovery needed.",
                provider.Id, provision.TenantId);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.IdentityServiceFailed,
                "Your workspace was created but account assignment failed. Please contact support to complete setup.");
        }

        // ── 5. Transition provider to TENANT stage ────────────────────────────
        // Both Tenant service and Identity assignment succeeded.
        provider.MarkTenantProvisioned(provision.TenantId);
        await _providerRepo.UpdateAsync(provider, ct);

        _logger.LogInformation(
            "BLK-CC-01 OnboardingSucceeded: Provider {ProviderId} transitioned to TENANT stage. " +
            "TenantId={TenantId} TenantCode={TenantCode} Subdomain={Subdomain}.",
            provider.Id, provision.TenantId, provision.TenantCode, provision.Subdomain);

        // ── 6. Build the portal URL ───────────────────────────────────────────
        var portalUrl = string.IsNullOrWhiteSpace(provision.Subdomain)
            ? null
            : $"https://{provision.Subdomain}.legalsynq.com";

        return new ProviderOnboardingResult
        {
            ProviderId         = provider.Id,
            TenantId           = provision.TenantId,
            TenantCode         = provision.TenantCode,
            Subdomain          = provision.Subdomain,
            ProvisioningStatus = "Provisioning",
            PortalUrl          = portalUrl,
        };
    }
}
