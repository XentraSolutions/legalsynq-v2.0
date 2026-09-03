using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Liens.Infrastructure.Identity;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

internal static partial class LienHistoryDescriptionEnricher
{
    private static readonly IReadOnlyDictionary<string, string> DisplayFields =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Organization ID"] = "Organization",
            ["Funding Company ID"] = "Funding Company",
            ["Case ID"] = "Case",
            ["Selling Case ID"] = "Selling Case",
            ["Facility ID"] = "Facility",
            ["Subject Party ID"] = "Subject Party",
            ["Selling Organization ID"] = "Selling Organization",
            ["Buying Organization ID"] = "Buying Organization",
            ["Holding Organization ID"] = "Holding Organization",
            ["Funding Company Contact ID"] = "Funding Company Contact",
            ["Medical Provider ID"] = "Medical Provider",
            ["Medical Facility ID"] = "Medical Facility",
        };

    private static readonly IReadOnlySet<string> OrganizationFields = new HashSet<string>(
        ["Organization ID", "Selling Organization ID", "Buying Organization ID", "Holding Organization ID"],
        StringComparer.Ordinal);

    internal static async Task<IReadOnlyDictionary<HistoryReference, string>> ResolveAsync(
        LiensDbContext db,
        Guid tenantId,
        IEnumerable<string> descriptions,
        IHttpClientFactory httpClientFactory,
        IdentityServiceOptions identityOptions,
        ILogger logger,
        CancellationToken ct)
    {
        var references = descriptions
            .SelectMany(ExtractReferences)
            .Distinct()
            .ToList();
        if (references.Count == 0)
            return new Dictionary<HistoryReference, string>();

        var facilityIds = IdsFor(references, "Facility ID", "Medical Facility ID");
        var contactIds = IdsFor(
            references,
            "Facility ID",
            "Subject Party ID",
            "Funding Company ID",
            "Funding Company Contact ID",
            "Medical Provider ID",
            "Medical Facility ID");
        var companyIds = IdsFor(
            references,
            "Facility ID",
            "Funding Company ID",
            "Medical Provider ID",
            "Medical Facility ID");
        var companyContactIds = IdsFor(references, "Subject Party ID", "Funding Company Contact ID");
        var caseIds = IdsFor(references, "Case ID", "Selling Case ID");
        var sellingCaseIds = IdsFor(references, "Selling Case ID");

        var facilities = await TryResolveAsync(
            facilityIds,
            () => db.Facilities
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && facilityIds.Contains(item.Id))
                .Select(item => new NamedReference(item.Id, item.Name))
                .ToDictionaryAsync(item => item.Id, ct),
            "facilities",
            logger,
            ct);
        var contacts = await TryResolveAsync(
            contactIds,
            () => db.Contacts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && contactIds.Contains(item.Id))
                .Select(item => new ContactReference(item.Id, item.DisplayName, item.Organization))
                .ToDictionaryAsync(item => item.Id, ct),
            "contacts",
            logger,
            ct);
        var companies = await TryResolveAsync(
            companyIds,
            () => db.Companies
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && companyIds.Contains(item.Id))
                .Select(item => new NamedReference(item.Id, item.Name))
                .ToDictionaryAsync(item => item.Id, ct),
            "companies",
            logger,
            ct);
        var companyContacts = await TryResolveAsync(
            companyContactIds,
            () => db.CompanyContactPersons
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && companyContactIds.Contains(item.Id))
                .Select(item => new PersonReference(item.Id, item.FirstName, item.LastName))
                .ToDictionaryAsync(item => item.Id, ct),
            "company contacts",
            logger,
            ct);
        var cases = await TryResolveAsync(
            caseIds,
            () => db.Cases
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && caseIds.Contains(item.Id))
                .Select(item => new CaseReference(item.Id, item.CaseNumber, item.ClientFirstName, item.ClientLastName))
                .ToDictionaryAsync(item => item.Id, ct),
            "cases",
            logger,
            ct);
        var sellingCases = await TryResolveAsync(
            sellingCaseIds,
            () => db.SellingCaseDrafts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && sellingCaseIds.Contains(item.Id))
                .Select(item => new SellingCaseReference(item.Id, item.CaseId, item.CaseStatus))
                .ToDictionaryAsync(item => item.Id, ct),
            "selling case drafts",
            logger,
            ct);

        var finalizedCaseIds = sellingCases.Values
            .Where(item => item.CaseId.HasValue)
            .Select(item => item.CaseId!.Value)
            .Where(id => !cases.ContainsKey(id))
            .Distinct()
            .ToList();
        if (finalizedCaseIds.Count > 0)
        {
            var finalizedCases = await TryResolveAsync(
                finalizedCaseIds,
                () => db.Cases
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId && finalizedCaseIds.Contains(item.Id))
                    .Select(item => new CaseReference(item.Id, item.CaseNumber, item.ClientFirstName, item.ClientLastName))
                    .ToDictionaryAsync(item => item.Id, ct),
                "finalized selling cases",
                logger,
                ct);
            foreach (var item in finalizedCases)
                cases[item.Key] = item.Value;
        }

        var organizationIds = references
            .Where(reference => OrganizationFields.Contains(reference.Field))
            .Select(reference => reference.Id)
            .Distinct()
            .ToList();
        var organizations = await ResolveOrganizationNamesAsync(
            organizationIds,
            httpClientFactory,
            identityOptions,
            ct);

        var resolved = new Dictionary<HistoryReference, string>();
        foreach (var reference in references)
        {
            var description = ResolveLocalDescription(
                reference,
                facilities,
                contacts,
                companies,
                companyContacts,
                cases,
                sellingCases,
                organizations);
            resolved[reference] = description ?? $"Unavailable {DisplayFields[reference.Field].ToLowerInvariant()}";
        }

        return resolved;
    }

    private static List<Guid> IdsFor(
        IReadOnlyCollection<HistoryReference> references,
        params string[] fields)
    {
        var selectedFields = fields.ToHashSet(StringComparer.Ordinal);
        return references
            .Where(reference => selectedFields.Contains(reference.Field))
            .Select(reference => reference.Id)
            .Distinct()
            .ToList();
    }

    private static async Task<Dictionary<Guid, T>> TryResolveAsync<T>(
        IReadOnlyCollection<Guid> ids,
        Func<Task<Dictionary<Guid, T>>> resolve,
        string source,
        ILogger logger,
        CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        try
        {
            return await resolve();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Could not resolve lien history {ReferenceSource}; descriptions will use unavailable labels.",
                source);
            return [];
        }
    }

    internal static string Enrich(
        string description,
        IReadOnlyDictionary<HistoryReference, string> references) =>
        ReferenceChangeRegex().Replace(description, match =>
        {
            var field = match.Groups["field"].Value;
            return $"{match.Groups["prefix"].Value}{DisplayFields[field]}: " +
                   $"{ResolveValue(field, match.Groups["previous"].Value, references)} → " +
                   ResolveValue(field, match.Groups["current"].Value, references);
        });

    private static IEnumerable<HistoryReference> ExtractReferences(string description)
    {
        foreach (Match match in ReferenceChangeRegex().Matches(description))
        {
            var field = match.Groups["field"].Value;
            if (Guid.TryParse(match.Groups["previous"].Value.Trim(), out var previousId))
                yield return new HistoryReference(field, previousId);
            if (Guid.TryParse(match.Groups["current"].Value.Trim(), out var currentId))
                yield return new HistoryReference(field, currentId);
        }
    }

    private static string ResolveValue(
        string field,
        string value,
        IReadOnlyDictionary<HistoryReference, string> references)
    {
        var normalized = value.Trim();
        if (!Guid.TryParse(normalized, out var id))
            return normalized;

        return references.GetValueOrDefault(
            new HistoryReference(field, id),
            $"Unavailable {DisplayFields[field].ToLowerInvariant()}");
    }

    private static string? ResolveLocalDescription(
        HistoryReference reference,
        IReadOnlyDictionary<Guid, NamedReference> facilities,
        IReadOnlyDictionary<Guid, ContactReference> contacts,
        IReadOnlyDictionary<Guid, NamedReference> companies,
        IReadOnlyDictionary<Guid, PersonReference> companyContacts,
        IReadOnlyDictionary<Guid, CaseReference> cases,
        IReadOnlyDictionary<Guid, SellingCaseReference> sellingCases,
        IReadOnlyDictionary<Guid, string> organizations)
    {
        facilities.TryGetValue(reference.Id, out var facility);
        contacts.TryGetValue(reference.Id, out var contact);
        companies.TryGetValue(reference.Id, out var company);
        companyContacts.TryGetValue(reference.Id, out var companyContact);
        cases.TryGetValue(reference.Id, out var caseItem);
        sellingCases.TryGetValue(reference.Id, out var sellingCase);
        organizations.TryGetValue(reference.Id, out var organization);

        return reference.Field switch
        {
            "Organization ID" or "Selling Organization ID" or "Buying Organization ID" or
                "Holding Organization ID" => organization,
            "Case ID" => DisplayCase(caseItem),
            "Selling Case ID" => DisplayCase(caseItem) ?? DisplaySellingCase(sellingCase, cases),
            "Facility ID" => FirstNonEmpty(facility?.Name, contact?.Organization, contact?.DisplayName, company?.Name),
            "Subject Party ID" => FirstNonEmpty(contact?.DisplayName, DisplayPerson(companyContact)),
            "Funding Company ID" => FirstNonEmpty(company?.Name, contact?.Organization, contact?.DisplayName),
            "Funding Company Contact ID" => FirstNonEmpty(DisplayPerson(companyContact), contact?.DisplayName),
            "Medical Provider ID" => FirstNonEmpty(company?.Name, contact?.Organization, contact?.DisplayName),
            "Medical Facility ID" => FirstNonEmpty(company?.Name, facility?.Name, contact?.Organization, contact?.DisplayName),
            _ => null,
        };
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ResolveOrganizationNamesAsync(
        IReadOnlyCollection<Guid> organizationIds,
        IHttpClientFactory httpClientFactory,
        IdentityServiceOptions options,
        CancellationToken ct)
    {
        var resolved = new Dictionary<Guid, string>();
        if (organizationIds.Count == 0 ||
            !Uri.TryCreate(options.BaseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out var identityBaseUri))
        {
            return resolved;
        }

        using var client = httpClientFactory.CreateClient("IdentityService");
        client.BaseAddress = identityBaseUri;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 5);
        if (!string.IsNullOrWhiteSpace(options.ProvisioningToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Provisioning-Token", options.ProvisioningToken);
        }
        else if (!string.IsNullOrWhiteSpace(options.AuthHeaderName) &&
                 !string.IsNullOrWhiteSpace(options.AuthHeaderValue))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(options.AuthHeaderName, options.AuthHeaderValue);
        }

        foreach (var organizationId in organizationIds)
        {
            try
            {
                using var response = await client.GetAsync($"api/admin/organizations/{organizationId:D}", ct);
                if (!response.IsSuccessStatusCode)
                    continue;

                var organization = await response.Content.ReadFromJsonAsync<IdentityOrganizationResponse>(
                    cancellationToken: ct);
                var name = FirstNonEmpty(organization?.DisplayName, organization?.Name);
                if (name is not null)
                    resolved[organizationId] = name;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException ||
                                       ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                // A failed display lookup must not make the history endpoint unavailable.
            }
        }

        return resolved;
    }

    private static string? DisplayCase(CaseReference? item)
    {
        if (item is null)
            return null;

        var clientName = FirstNonEmpty(string.Join(' ', new[] { item.ClientFirstName, item.ClientLastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))));
        return clientName is null ? item.CaseNumber : $"{item.CaseNumber} — {clientName}";
    }

    private static string? DisplaySellingCase(
        SellingCaseReference? item,
        IReadOnlyDictionary<Guid, CaseReference> cases)
    {
        if (item is null)
            return null;
        if (item.CaseId.HasValue && cases.TryGetValue(item.CaseId.Value, out var caseItem))
            return DisplayCase(caseItem);
        return $"Selling case draft — {item.CaseStatus}";
    }

    private static string? DisplayPerson(PersonReference? item) =>
        item is null ? null : FirstNonEmpty($"{item.FirstName} {item.LastName}");

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    internal readonly record struct HistoryReference(string Field, Guid Id);
    private sealed record NamedReference(Guid Id, string Name);
    private sealed record ContactReference(Guid Id, string DisplayName, string? Organization);
    private sealed record PersonReference(Guid Id, string FirstName, string LastName);
    private sealed record CaseReference(Guid Id, string CaseNumber, string ClientFirstName, string ClientLastName);
    private sealed record SellingCaseReference(Guid Id, Guid? CaseId, string CaseStatus);

    private sealed class IdentityOrganizationResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }
    }

    [GeneratedRegex(
        @"(?<prefix>Changes: |; )(?<field>Organization ID|Funding Company ID|Case ID|Selling Case ID|Facility ID|Subject Party ID|Selling Organization ID|Buying Organization ID|Holding Organization ID|Funding Company Contact ID|Medical Provider ID|Medical Facility ID): (?<previous>.*?) → (?<current>.*?)(?=; |\.$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceChangeRegex();
}
