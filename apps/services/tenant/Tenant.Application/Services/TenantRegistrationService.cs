using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Text;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public sealed class TenantRegistrationService(
    ITenantRegistrationRepository registrations,
    ITenantRepository tenants,
    ITenantAdminService tenantAdmin,
    IIdentityProvisioningAdapter identityProvisioning,
    ITenantRegistrationNotificationClient notifications,
    ILogger<TenantRegistrationService> logger) : ITenantRegistrationService
{
    public async Task<SubmitTenantRegistrationResponse> SubmitAsync(SubmitTenantRegistrationRequest request, CancellationToken ct = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0) throw new BuildingBlocks.Exceptions.ValidationException("Registration validation failed.", errors);
        var code = NormalizeCode(request.TenantCode);
        var email = request.AdminEmail.Trim().ToLowerInvariant();
        if (await tenants.ExistsByCodeAsync(code, ct)) throw new ConflictException($"Tenant code '{code}' is already in use.");
        if (await registrations.HasPendingConflictAsync(code, email, ct))
            throw new ConflictException("A pending registration already exists for this tenant code or administrator email.");
        var emailAvailability = await identityProvisioning.CheckAdminEmailAsync(email, ct);
        if (!emailAvailability.Success)
            throw new InvalidOperationException("Administrator email availability could not be verified.");
        if (emailAvailability.Exists)
            throw new ConflictException("An account with this administrator email already exists.");

        var entity = TenantRegistration.Create(request.TenantName.Trim(), code, request.OrganizationType.Trim(),
            NullIfBlank(request.StreetAddress), request.AdminFirstName.Trim(), request.AdminLastName.Trim(), email,
            NullIfBlank(request.AddressLine1), NullIfBlank(request.AddressCity), NullIfBlank(request.AddressState), NullIfBlank(request.AddressPostalCode));
        await registrations.AddAsync(entity, ct);
        logger.LogInformation("Tenant registration submitted RegistrationId={RegistrationId} TenantCode={TenantCode}", entity.Id, code);
        var submittedEmail = await notifications.SendSubmittedAsync(
            entity.Id, entity.AdminEmail, $"{entity.AdminFirstName} {entity.AdminLastName}".Trim(), entity.TenantName, ct);
        if (!submittedEmail.Success)
            logger.LogWarning("Registration submitted email failed RegistrationId={RegistrationId}: {Error}", entity.Id, submittedEmail.Error);
        return new(entity.Id, entity.RegistrationStatus.ToString(), entity.ProvisioningStatus.ToString(),
            "Your registration has been submitted for review.");
    }

    public async Task<TenantRegistrationResponse?> GetAsync(Guid id, CancellationToken ct = default) =>
        Map(await registrations.GetAsync(id, ct));

    public async Task<TenantRegistrationListResponse> ListAsync(string? registrationStatus, string? provisioningStatus,
        string? search, DateTime? submittedFrom, DateTime? submittedTo, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await registrations.ListAsync(registrationStatus, provisioningStatus, search,
            submittedFrom, submittedTo, page, pageSize, ct);
        return new(items.Select(x => Map(x)!).ToList(), total, page, pageSize);
    }

    public async Task<TenantRegistrationDecisionResponse> ApproveAsync(Guid id, Guid reviewerId, CancellationToken ct = default)
    {
        var registration = await registrations.GetAsync(id, ct) ?? throw new NotFoundException($"Registration '{id}' was not found.");
        if (registration.RegistrationStatus == RegistrationStatus.Approved) return Decision(registration, [], []);
        if (registration.ProvisioningStatus == RegistrationProvisioningStatus.InProgress)
            throw new ConflictException("Approval is already in progress.");
        try
        {
            registration.BeginApproval(reviewerId);
            await registrations.SaveAsync(ct); // optimistic concurrency reserves the transition
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This registration is already being reviewed. Refresh and try again.");
        }

        try
        {
            var result = await tenantAdmin.CreateTenantAsync(new AdminCreateTenantRequest(
                registration.TenantName, registration.TenantCode,
                registration.AdminEmail, registration.AdminFirstName, registration.AdminLastName, registration.OrganizationType,
                registration.StreetAddress, null, null, null, null, null, null),
                ct, tenantRegistrationApproval: true);
            var success = result.IdentityProvisioned && result.ProvisioningStatus is "Active" or "Provisioned";
            registration.CompleteApproval(Guid.Parse(result.TenantId), result.Hostname, success,
                result.ProvisioningErrors.Count > 0 ? string.Join("; ", result.ProvisioningErrors) : null,
                success ? null : InferFailureStage(result));
            await registrations.SaveAsync(ct);
            return Decision(registration, result.ProvisioningWarnings, result.ProvisioningErrors);
        }
        catch
        {
            registration.ResetApprovalReservation();
            await registrations.SaveAsync(ct);
            throw;
        }
    }

    public async Task<TenantRegistrationResponse> DeclineAsync(Guid id, Guid reviewerId, string reason, CancellationToken ct = default)
    {
        var registration = await registrations.GetAsync(id, ct) ?? throw new NotFoundException($"Registration '{id}' was not found.");
        registration.Decline(reviewerId, reason);
        await registrations.SaveAsync(ct);
        var declinedEmail = await notifications.SendDeclinedAsync(
            registration.Id, registration.AdminEmail,
            $"{registration.AdminFirstName} {registration.AdminLastName}".Trim(),
            registration.TenantName, reason, ct);
        if (!declinedEmail.Success)
            logger.LogWarning("Registration declined email failed RegistrationId={RegistrationId}: {Error}", registration.Id, declinedEmail.Error);
        return Map(registration)!;
    }

    public async Task<TenantRegistrationDecisionResponse> RetryProvisioningAsync(Guid id, CancellationToken ct = default)
    {
        var registration = await registrations.GetAsync(id, ct) ?? throw new NotFoundException($"Registration '{id}' was not found.");
        registration.BeginProvisioningRetry();
        await registrations.SaveAsync(ct);
        var result = await identityProvisioning.RetryProvisioningAsync(registration.TenantId!.Value, ct);
        registration.CompleteProvisioningRetry(result.Success, result.Hostname, result.Error, result.FailureStage);
        await registrations.SaveAsync(ct);
        return Decision(registration, [], result.Error is null ? [] : [result.Error]);
    }

    public static string NormalizeCode(string value)
    {
        var input = value.Trim().ToLowerInvariant();
        var output = new StringBuilder(input.Length);
        var hyphen = false;
        foreach (var c in input)
        {
            if (char.IsAsciiLetterOrDigit(c)) { output.Append(c); hyphen = false; }
            else if (!hyphen && output.Length > 0) { output.Append('-'); hyphen = true; }
        }
        var slug = output.ToString().Trim('-');
        if (slug.Length is < 1 or > 63) throw new BuildingBlocks.Exceptions.ValidationException("Tenant code is invalid.",
            new Dictionary<string, string[]> { ["tenantCode"] = ["Tenant code must normalize to a DNS label between 1 and 63 characters."] });
        return slug;
    }

    private static Dictionary<string, string[]> Validate(SubmitTenantRegistrationRequest r)
    {
        var e = new Dictionary<string, string[]>();
        void Required(string key, string? value) { if (string.IsNullOrWhiteSpace(value)) e[key] = [$"{key} is required."]; }
        Required("tenantName", r.TenantName); Required("tenantCode", r.TenantCode); Required("organizationType", r.OrganizationType);
        Required("adminFirstName", r.AdminFirstName); Required("adminLastName", r.AdminLastName); Required("adminEmail", r.AdminEmail);
        if (!string.IsNullOrWhiteSpace(r.AdminEmail)) try { _ = new MailAddress(r.AdminEmail); } catch { e["adminEmail"] = ["A valid email address is required."]; }
        return e;
    }

    private static TenantRegistrationResponse? Map(TenantRegistration? x) => x is null ? null : new(x.Id, x.TenantName,
        x.TenantCode, x.OrganizationType, x.StreetAddress, x.AddressLine1, x.AddressCity, x.AddressState, x.AddressPostalCode, x.AdminFirstName, x.AdminLastName, x.AdminEmail,
        x.RegistrationStatus.ToString(), x.ProvisioningStatus.ToString(), x.TenantId, x.ProvisioningHostname,
        x.ProvisioningError, x.ProvisioningFailureStage, x.DecisionReason, x.ReviewedByUserId, x.ReviewedAtUtc,
        x.ProvisioningStartedAtUtc, x.ProvisionedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc);

    private static TenantRegistrationDecisionResponse Decision(TenantRegistration x, IReadOnlyList<string> warnings, IReadOnlyList<string> errors) =>
        new(x.RegistrationStatus.ToString(), x.TenantId, x.TenantId is null ? null : "Active", x.AdminEmail,
            x.ProvisioningStatus.ToString(), x.ProvisioningHostname, warnings, errors,
            x.ProvisioningStatus == RegistrationProvisioningStatus.Failed ? "RetryProvisioning" : "None", x.ProvisioningFailureStage);
    private static string InferFailureStage(AdminCreateTenantResponse r) => r.IdentityProvisioned ? "DnsRecord" : "IdentityTenant";
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
