using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Liens.Infrastructure.Identity;

public sealed class SellerOrganizationDisplayResolver : ISellerOrganizationDisplayResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityServiceOptions _options;
    private readonly ILogger<SellerOrganizationDisplayResolver> _logger;

    public SellerOrganizationDisplayResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<IdentityServiceOptions> options,
        ILogger<SellerOrganizationDisplayResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SellerOrganizationDisplay> ResolveAsync(
        Guid tenantId,
        Guid sellerOrgId,
        IReadOnlyList<Contact> sellerContacts,
        string? fallbackEmail = null,
        CancellationToken ct = default)
    {
        var identityOwnerDisplay = await ResolveIdentityTenantOwnerDisplayAsync(tenantId, sellerOrgId, ct);
        var identityOrganizationName = FirstNonEmpty(
            identityOwnerDisplay?.OrganizationDisplayName,
            identityOwnerDisplay?.OrganizationName)
            ?? await ResolveIdentityOrganizationNameAsync(sellerOrgId, ct);
        var localOrganizationName = ResolveLocalSellerOrganizationName(sellerContacts);
        var company = FirstNonEmpty(
            identityOrganizationName,
            localOrganizationName,
            "Seller company unavailable")!;
        var name = FirstNonEmpty(
            ResolveDisplayName(identityOwnerDisplay?.DisplayName, identityOwnerDisplay?.FirstName, identityOwnerDisplay?.LastName),
            identityOrganizationName,
            localOrganizationName,
            "Seller unavailable")!;
        var email = FirstNonEmpty(fallbackEmail);

        return new SellerOrganizationDisplay(name, company, email);
    }

    private async Task<IdentityTenantOwnerDisplayResponse?> ResolveIdentityTenantOwnerDisplayAsync(
        Guid tenantId,
        Guid sellerOrgId,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty || sellerOrgId == Guid.Empty)
            return null;

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return null;

        try
        {
            using var client = BuildIdentityClient();
            using var response = await client.GetAsync(
                $"api/internal/users/tenant-owner/display?organizationId={sellerOrgId:D}&tenantId={tenantId:D}",
                ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity tenant owner lookup returned HTTP {StatusCode} for seller org {SellerOrgId}.",
                    (int)response.StatusCode,
                    sellerOrgId);
                return null;
            }

            var owner = await response.Content.ReadFromJsonAsync<IdentityTenantOwnerDisplayResponse>(
                cancellationToken: ct);
            return owner?.Found == true ? owner : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Identity tenant owner lookup timed out for seller org {SellerOrgId}.",
                sellerOrgId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Identity tenant owner lookup failed for seller org {SellerOrgId}.",
                sellerOrgId);
            return null;
        }
    }

    private async Task<string?> ResolveIdentityOrganizationNameAsync(Guid sellerOrgId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return null;

        try
        {
            using var client = BuildIdentityClient();
            using var response = await client.GetAsync($"api/admin/organizations/{sellerOrgId:D}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity organization lookup returned HTTP {StatusCode} for seller org {SellerOrgId}.",
                    (int)response.StatusCode,
                    sellerOrgId);
                return null;
            }

            var organization = await response.Content.ReadFromJsonAsync<IdentityOrganizationResponse>(
                cancellationToken: ct);
            return FirstNonEmpty(organization?.DisplayName, organization?.Name);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Identity organization lookup timed out for seller org {SellerOrgId}.",
                sellerOrgId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Identity organization lookup failed for seller org {SellerOrgId}.",
                sellerOrgId);
            return null;
        }
    }

    private HttpClient BuildIdentityClient()
    {
        var client = _httpClientFactory.CreateClient("IdentityService");
        client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_options.ProvisioningToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Provisioning-Token",
                _options.ProvisioningToken);
        }
        else if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                 !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                _options.AuthHeaderName,
                _options.AuthHeaderValue);
        }

        return client;
    }

    private static string? ResolveLocalSellerOrganizationName(IReadOnlyList<Contact> contacts)
    {
        var orderedContacts = OrderSellerContacts(contacts);
        var organizationContact =
            orderedContacts.FirstOrDefault(IsPreferredSellerOrganizationContact) ??
            orderedContacts.FirstOrDefault(contact =>
                !IsExcludedSellerDisplayContact(contact) &&
                !string.IsNullOrWhiteSpace(contact.Organization)) ??
            orderedContacts.FirstOrDefault(contact =>
                !IsExcludedSellerDisplayContact(contact) &&
                !string.IsNullOrWhiteSpace(contact.DisplayName));

        return FirstNonEmpty(
            organizationContact?.Organization,
            organizationContact?.DisplayName,
            orderedContacts.FirstOrDefault(contact =>
                !string.IsNullOrWhiteSpace(contact.Organization))?.Organization,
            orderedContacts.FirstOrDefault(contact =>
                !string.IsNullOrWhiteSpace(contact.DisplayName))?.DisplayName);
    }

    private static bool IsPreferredSellerOrganizationContact(Contact contact)
        => !IsExcludedSellerDisplayContact(contact) &&
           !string.IsNullOrWhiteSpace(contact.Organization) &&
           (string.Equals(contact.ContactType, ContactType.InternalUser, StringComparison.Ordinal) ||
            string.Equals(contact.ContactType, ContactType.LienHolder, StringComparison.Ordinal) ||
            string.Equals(contact.ContactType, ContactType.Provider, StringComparison.Ordinal) ||
            string.Equals(contact.ContactType, ContactType.Facility, StringComparison.Ordinal) ||
            string.Equals(contact.ContactType, ContactType.MedicalFacility, StringComparison.Ordinal));

    private static bool IsExcludedSellerDisplayContact(Contact contact)
        => string.Equals(contact.ContactType, ContactType.LawFirm, StringComparison.Ordinal) ||
           string.Equals(contact.ContactType, ContactType.CaseManager, StringComparison.Ordinal);

    private static IReadOnlyList<Contact> OrderSellerContacts(IReadOnlyList<Contact> contacts)
        => contacts
            .OrderBy(contact => SellerOrganizationContactRank(contact))
            .ThenBy(contact => contact.Organization ?? string.Empty)
            .ThenBy(contact => contact.DisplayName)
            .ThenBy(contact => contact.Email ?? string.Empty)
            .ThenBy(contact => contact.Id)
            .ToList();

    private static int SellerOrganizationContactRank(Contact contact)
    {
        if (string.Equals(contact.ContactType, ContactType.InternalUser, StringComparison.Ordinal))
            return 0;
        if (string.Equals(contact.ContactType, ContactType.LienHolder, StringComparison.Ordinal))
            return 1;
        if (string.Equals(contact.ContactType, ContactType.Provider, StringComparison.Ordinal) ||
            string.Equals(contact.ContactType, ContactType.Facility, StringComparison.Ordinal) ||
            string.Equals(contact.ContactType, ContactType.MedicalFacility, StringComparison.Ordinal))
            return 2;
        if (!IsExcludedSellerDisplayContact(contact))
            return 3;

        return 4;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ResolveDisplayName(string? displayName, string? firstName, string? lastName)
        => FirstNonEmpty(
            displayName,
            string.Join(' ', new[] { firstName, lastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))));

    private sealed class IdentityOrganizationResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }
    }

    private sealed class IdentityTenantOwnerDisplayResponse
    {
        [JsonPropertyName("found")]
        public bool Found { get; init; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; init; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("organizationName")]
        public string? OrganizationName { get; init; }

        [JsonPropertyName("organizationDisplayName")]
        public string? OrganizationDisplayName { get; init; }
    }
}
