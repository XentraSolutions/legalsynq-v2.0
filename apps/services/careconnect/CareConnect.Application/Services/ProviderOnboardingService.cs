// CC2-INT-B09: Provider tenant self-onboarding service.
// Orchestrates COMMON_PORTAL → TENANT stage transition.
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

/// <summary>
/// Orchestrates provider self-onboarding:
///  1. Validates the provider exists and is at COMMON_PORTAL stage.
///  2. Calls Identity to create a new tenant for the existing user.
///  3. Calls provider.MarkTenantProvisioned(newTenantId).
///  4. Persists the change.
///  5. Returns tenant details (including portal URL).
/// </summary>
public sealed class ProviderOnboardingService : IProviderOnboardingService
{
    private readonly IProviderRepository           _providerRepo;
    private readonly IIdentityOrganizationService  _identityService;
    private readonly ILogger<ProviderOnboardingService> _logger;

    public ProviderOnboardingService(
        IProviderRepository          providerRepo,
        IIdentityOrganizationService identityService,
        ILogger<ProviderOnboardingService> logger)
    {
        _providerRepo    = providerRepo;
        _identityService = identityService;
        _logger          = logger;
    }

    /// <inheritdoc />
    public async Task<ProviderOnboardingCodeCheckResult?> CheckCodeAvailableAsync(
        string            code,
        CancellationToken ct = default)
    {
        var result = await _identityService.CheckTenantCodeAvailableAsync(code, ct);
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
                "CC2-INT-B09 OnboardingFailed: no provider found for IdentityUserId={UserId}.",
                identityUserId);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.ProviderNotFound,
                "No provider record is linked to your account. Contact platform support.");
        }

        // ── 2. Guard: must be COMMON_PORTAL ──────────────────────────────────
        if (!ProviderAccessStage.IsAtLeast(provider.AccessStage, ProviderAccessStage.CommonPortal) ||
            provider.AccessStage == ProviderAccessStage.Tenant)
        {
            _logger.LogWarning(
                "CC2-INT-B09 OnboardingFailed: provider {ProviderId} is at stage '{Stage}', expected COMMON_PORTAL.",
                provider.Id, provider.AccessStage);

            var msg = provider.AccessStage == ProviderAccessStage.Tenant
                ? "Your account has already been provisioned to a tenant workspace."
                : "Your account must be at the COMMON_PORTAL stage before setting up a workspace. Complete your portal activation first.";

            throw new ProviderOnboardingException(ProviderOnboardingErrorCode.WrongAccessStage, msg);
        }

        // ── 3. Call Identity: create new tenant for existing user ─────────────
        var provision = await _identityService.SelfProvisionProviderTenantAsync(
            identityUserId, tenantName, tenantCode, ct);

        if (provision is null)
        {
            _logger.LogError(
                "CC2-INT-B09 OnboardingFailed: Identity SelfProvision returned null for provider {ProviderId}.",
                provider.Id);
            throw new ProviderOnboardingException(
                ProviderOnboardingErrorCode.IdentityServiceFailed,
                "Unable to create your workspace at this time. Please try again or contact support.");
        }

        // ── 4. Transition provider to TENANT stage ───────────────────────────
        provider.MarkTenantProvisioned(provision.TenantId);
        await _providerRepo.UpdateAsync(provider, ct);

        _logger.LogInformation(
            "CC2-INT-B09 OnboardingSucceeded: Provider {ProviderId} transitioned to TENANT stage. " +
            "TenantId={TenantId} TenantCode={TenantCode} Subdomain={Subdomain}.",
            provider.Id, provision.TenantId, provision.TenantCode, provision.Subdomain);

        // ── 5. Build the portal URL ──────────────────────────────────────────
        var portalUrl = provision.Hostname is not null
            ? $"https://{provision.Hostname}"
            : null;

        return new ProviderOnboardingResult
        {
            ProviderId         = provider.Id,
            TenantId           = provision.TenantId,
            TenantCode         = provision.TenantCode,
            Subdomain          = provision.Subdomain,
            ProvisioningStatus = provision.ProvisioningStatus,
            PortalUrl          = portalUrl,
        };
    }
}
