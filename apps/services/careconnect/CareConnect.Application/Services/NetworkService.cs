using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Helpers;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

// CC2-INT-B06 / CC2-INT-B06-01 — provider network management with shared provider registry
public class NetworkService : INetworkService
{
    private readonly INetworkRepository _networks;
    private readonly ICategoryRepository _categories;
    private readonly ISpecialtyRepository _specialties;
    private readonly IProviderImportParser _providerImportParser;
    private readonly ILogger<NetworkService> _logger;

    public NetworkService(
        INetworkRepository networks,
        ICategoryRepository categories,
        ISpecialtyRepository specialties,
        IProviderImportParser providerImportParser,
        ILogger<NetworkService> logger)
    {
        _networks = networks;
        _categories = categories;
        _specialties = specialties;
        _providerImportParser = providerImportParser;
        _logger = logger;
    }

    // ── Network CRUD ─────────────────────────────────────────────────────────

    public async Task<List<NetworkSummaryResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var networks = await _networks.GetAllByTenantAsync(tenantId, ct);

        var tasks = networks.Select(async n =>
        {
            var detail = await _networks.GetWithProvidersAsync(tenantId, n.Id, ct);
            return ToSummary(n, detail?.NetworkProviders.Count ?? 0);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<NetworkDetailResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var network = await _networks.GetWithProvidersAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        return ToDetail(network);
    }

    public async Task<NetworkSummaryResponse> CreateAsync(
        Guid tenantId, Guid? userId, CreateNetworkRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        if (await _networks.NameExistsAsync(tenantId, request.Name.Trim(), ct: ct))
            throw new ValidationException("Duplicate network name.",
                new() { ["name"] = [$"A network named '{request.Name.Trim()}' already exists."] });

        var network = ProviderNetwork.Create(tenantId, request.Name, request.Description ?? string.Empty);
        await _networks.AddAsync(network, ct);
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation("Network {NetworkId} created for tenant {TenantId}.", network.Id, tenantId);

        return ToSummary(network, 0);
    }

    public async Task<NetworkSummaryResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid? userId, UpdateNetworkRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        var network = await _networks.GetWithProvidersAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        if (await _networks.NameExistsAsync(tenantId, request.Name.Trim(), excludeId: id, ct: ct))
            throw new ValidationException("Duplicate network name.",
                new() { ["name"] = [$"A network named '{request.Name.Trim()}' already exists."] });

        network.Update(request.Name, request.Description ?? string.Empty);
        await _networks.SaveChangesAsync(ct);

        return ToSummary(network, network.NetworkProviders.Count);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var network = await _networks.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        network.Delete();
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation("Network {NetworkId} soft-deleted for tenant {TenantId}.", id, tenantId);
    }

    // ── Shared provider registry — search/import/match-or-create ────────────

    public async Task<List<ProviderSearchResult>> SearchProvidersAsync(
        string? name, string? phone, string? npi, string? city, CancellationToken ct = default)
    {
        var providers = await _networks.SearchProvidersGlobalAsync(name, phone, npi, city, limit: 20, ct: ct);
        return providers.Select(ToSearchResult).ToList();
    }

    public async Task<ProviderImportSummaryResponse> ImportProvidersAsync(
        Guid networkId,
        Stream fileStream,
        string fileName,
        bool dryRun,
        Guid? userId,
        CancellationToken ct = default)
    {
        var network = await _networks.GetByIdGlobalAsync(networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");
        var networkTenantId = network.TenantId;

        _logger.LogInformation(
            "Provider import started: TenantId={TenantId} NetworkId={NetworkId} FileName={FileName} DryRun={DryRun}",
            networkTenantId, networkId, fileName, dryRun);

        var parsed = await _providerImportParser.ParseAsync(fileStream, fileName, ct);
        var npis = parsed.Rows
            .Select(r => NormalizeOptional(r.Npi))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var emails = parsed.Rows
            .Select(r => NormalizeEmail(r.Email))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var providersByNpi = await _networks.GetProvidersByNpisAsync(npis, ct);
        var providersByEmail = await _networks.GetProvidersByTenantEmailsAsync(networkTenantId, emails, ct);
        var providerIdsInNetwork = await _networks.GetNetworkProviderIdsAsync(networkTenantId, networkId, ct);

        var rows = new List<ProviderImportRowResult>(parsed.Rows.Count);
        var createdProviders = 0;
        var reusedByNpi = 0;
        var reusedByEmail = 0;
        var alreadyInNetwork = 0;
        var failedRows = 0;
        var validRows = 0;
        var processedRows = 0;

        foreach (var parsedRow in parsed.Rows)
        {
            if (!TryNormalizeImportRow(parsedRow, out var normalized, out var errors))
            {
                failedRows++;
                rows.Add(new ProviderImportRowResult(
                    parsedRow.RowNumber,
                    parsedRow.SourceKey,
                    "failed",
                    null,
                    "Row validation failed.",
                    null,
                    errors));
                continue;
            }

            validRows++;

            try
            {
                if (normalized.TenantId != networkTenantId)
                {
                    failedRows++;
                    rows.Add(new ProviderImportRowResult(
                        parsedRow.RowNumber,
                        parsedRow.SourceKey,
                        "failed",
                        null,
                        "Row tenant does not match the target network tenant.",
                        normalized,
                        [$"tenantId {normalized.TenantId} does not match network tenant {networkTenantId}."]));
                    continue;
                }

                var resolution = ResolveImportProvider(networkTenantId, userId, normalized, providersByNpi, providersByEmail);
                var provider = resolution.Provider;
                var status = resolution.Status;
                var message = resolution.Message;

                if (providerIdsInNetwork.Contains(provider.Id))
                {
                    alreadyInNetwork++;
                    processedRows++;
                    rows.Add(new ProviderImportRowResult(
                        parsedRow.RowNumber,
                        parsedRow.SourceKey,
                        "already_in_network",
                        provider.Id,
                        "Provider is already linked to this network.",
                        normalized,
                        []));
                    continue;
                }

                if (!dryRun)
                {
                    if (status == "created")
                        await _networks.AddProviderToRegistryAsync(provider, ct);

                    await _networks.AddProviderAsync(NetworkProvider.Create(networkTenantId, networkId, provider.Id), ct);
                    await _networks.SaveChangesAsync(ct);
                }

                providerIdsInNetwork.Add(provider.Id);
                CacheResolvedProvider(provider, providersByNpi, providersByEmail);

                processedRows++;
                switch (status)
                {
                    case "created":
                        createdProviders++;
                        break;
                    case "reused_npi":
                        reusedByNpi++;
                        break;
                    case "reused_email":
                        reusedByEmail++;
                        break;
                }

                rows.Add(new ProviderImportRowResult(
                    parsedRow.RowNumber,
                    parsedRow.SourceKey,
                    status,
                    provider.Id,
                    message,
                    normalized,
                    []));
            }
            catch (Exception ex)
            {
                _networks.ClearTracking();
                failedRows++;
                _logger.LogWarning(
                    ex,
                    "Provider import row failed: TenantId={TenantId} NetworkId={NetworkId} FileName={FileName} RowNumber={RowNumber}",
                    networkTenantId, networkId, fileName, parsedRow.RowNumber);

                rows.Add(new ProviderImportRowResult(
                    parsedRow.RowNumber,
                    parsedRow.SourceKey,
                    "failed",
                    null,
                    "Row import failed.",
                    normalized,
                    [ex.Message]));
            }
        }

        _logger.LogInformation(
            "Provider import completed: TenantId={TenantId} NetworkId={NetworkId} FileName={FileName} DryRun={DryRun} TotalRows={TotalRows} Created={CreatedProviders} ReusedByNpi={ReusedByNpi} ReusedByEmail={ReusedByEmail} AlreadyInNetwork={AlreadyInNetwork} FailedRows={FailedRows}",
            networkTenantId, networkId, fileName, dryRun, parsed.TotalRows, createdProviders, reusedByNpi, reusedByEmail, alreadyInNetwork, failedRows);

        return new ProviderImportSummaryResponse(
            TenantId: networkTenantId,
            NetworkId: networkId,
            FileName: fileName,
            DryRun: dryRun,
            TotalRows: parsed.TotalRows,
            ValidRows: validRows,
            ProcessedRows: processedRows,
            CreatedProviders: createdProviders,
            ReusedByNpi: reusedByNpi,
            ReusedByEmail: reusedByEmail,
            AlreadyInNetwork: alreadyInNetwork,
            FailedRows: failedRows,
            Rows: rows);
    }

    public async Task<NetworkProviderItem> AddProviderAsync(
        Guid tenantId,
        Guid networkId,
        AddProviderToNetworkRequest request,
        Guid? userId,
        CancellationToken ct = default)
    {
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        Provider provider;

        if (request.ExistingProviderId.HasValue)
        {
            provider = await _networks.GetProviderByIdGlobalAsync(request.ExistingProviderId.Value, ct)
                ?? throw new NotFoundException($"Provider {request.ExistingProviderId.Value} not found in the shared registry.");
        }
        else if (request.NewProvider is { } np)
        {
            ValidateNewProvider(np);
            ValidateGeoFields(np.Latitude, np.Longitude, np.GeoPointSource);
            provider = await ResolveProviderForAddAsync(tenantId, np, userId, ct);
        }
        else
        {
            throw new ValidationException("Validation failed.",
                new() { ["request"] = ["Either ExistingProviderId or NewProvider must be provided."] });
        }

        var existing = await _networks.GetMembershipAsync(networkId, provider.Id, ct);
        if (existing is null)
            await _networks.AddProviderAsync(NetworkProvider.Create(tenantId, networkId, provider.Id), ct);
        else
            _logger.LogDebug("Provider {ProviderId} already in network {NetworkId} — no-op.", provider.Id, networkId);

        await _networks.SaveChangesAsync(ct);

        return ToProviderItem(provider);
    }

    public async Task RemoveProviderAsync(
        Guid tenantId, Guid networkId, Guid providerId, CancellationToken ct = default)
    {
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var entry = await _networks.GetMembershipAsync(networkId, providerId, ct)
            ?? throw new NotFoundException($"Provider {providerId} is not a member of network {networkId}.");

        await _networks.RemoveProviderAsync(entry, ct);
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Provider {ProviderId} removed from network {NetworkId} (association only; shared record preserved).",
            providerId, networkId);
    }

    public async Task<List<NetworkProviderMarker>> GetMarkersAsync(
        Guid tenantId, Guid networkId, CancellationToken ct = default)
    {
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var providers = await _networks.GetNetworkProvidersAsync(tenantId, networkId, ct);

        return providers
            .Select(p =>
            {
                var specialties = MapSpecialties(p.ProviderSpecialties);
                var primarySpecialty = specialties.FirstOrDefault();
                return new NetworkProviderMarker(
                    p.Id,
                    p.Name,
                    p.Title,
                    p.OrganizationName,
                    p.City,
                    p.State,
                    p.AddressLine1,
                    p.PostalCode,
                    p.Email,
                    p.Phone,
                    p.AcceptingReferrals,
                    p.IsActive,
                    p.Latitude ?? 0.0,
                    p.Longitude ?? 0.0,
                    p.GeoPointSource,
                    specialties,
                    primarySpecialty?.Id,
                    primarySpecialty?.Name);
            })
            .ToList();
    }

    public async Task<NetworkProviderItem> UpdateProviderAsync(
        Guid tenantId,
        Guid networkId,
        Guid providerId,
        UpdateNetworkProviderRequest request,
        Guid? userId,
        CancellationToken ct = default)
    {
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        _ = await _networks.GetMembershipAsync(networkId, providerId, ct)
            ?? throw new NotFoundException($"Provider {providerId} is not a member of network {networkId}.");

        ValidateNetworkProviderFields(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.Title);
        ValidateGeoFields(request.Latitude, request.Longitude, request.GeoPointSource);
        var specialtyIds = await ValidateSpecialtyIdsAsync(request.SpecialtyIds, ct);

        var provider = await _networks.GetProviderByIdGlobalAsync(providerId, ct)
            ?? throw new NotFoundException($"Provider {providerId} not found in the shared registry.");

        provider.Update(
            name: BuildProviderDisplayName(request.Title, request.FirstName, request.LastName),
            organizationName: request.OrganizationName,
            email: request.Email.Trim().ToLowerInvariant(),
            phone: request.Phone,
            addressLine1: request.AddressLine1,
            city: request.City,
            state: request.State.Trim().ToUpperInvariant(),
            postalCode: request.PostalCode,
            isActive: request.IsActive,
            acceptingReferrals: request.AcceptingReferrals,
            updatedByUserId: userId,
            latitude: request.Latitude ?? provider.Latitude,
            longitude: request.Longitude ?? provider.Longitude,
            geoPointSource: request.Latitude.HasValue ? request.GeoPointSource : provider.GeoPointSource,
            firstName: request.FirstName,
            lastName: request.LastName,
            title: request.Title);

        await _networks.UpdateProviderInRegistryAsync(provider, ct);
        await _networks.SyncProviderSpecialtiesAsync(provider.Id, specialtyIds, ct);
        await _networks.SaveChangesAsync(ct);

        var loaded = await _networks.GetProviderByIdGlobalAsync(providerId, ct);
        return ToProviderItem(loaded ?? provider);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static NetworkSummaryResponse ToSummary(ProviderNetwork n, int providerCount) =>
        new(n.Id, n.Name, n.Description, providerCount, n.CreatedAtUtc, n.UpdatedAtUtc);

    private static NetworkDetailResponse ToDetail(ProviderNetwork n) =>
        new(
            n.Id,
            n.Name,
            n.Description,
            n.NetworkProviders.Select(np => ToProviderItem(np.Provider)).ToList(),
            n.CreatedAtUtc,
            n.UpdatedAtUtc);

    private static NetworkProviderItem ToProviderItem(Provider p)
    {
        var specialties = MapSpecialties(p.ProviderSpecialties);
        var primarySpecialty = specialties.FirstOrDefault();
        return new(p.Id, p.Name, p.Title, p.OrganizationName, p.Email, p.Phone, p.City, p.State,
            p.AddressLine1, p.PostalCode, p.IsActive, p.AcceptingReferrals, p.AccessStage,
            specialties,
            primarySpecialty?.Id,
            primarySpecialty?.Name);
    }

    private static ProviderSearchResult ToSearchResult(Provider p)
    {
        var specialties = MapSpecialties(p.ProviderSpecialties);
        var primarySpecialty = specialties.FirstOrDefault();
        return new(p.Id, p.Name, p.Title, p.OrganizationName, p.Email, p.Phone, p.City, p.State,
            p.AddressLine1, p.PostalCode, p.Npi, p.IsActive, p.AcceptingReferrals, p.AccessStage,
            specialties,
            primarySpecialty?.Id,
            primarySpecialty?.Name);
    }

    private async Task<Provider> ResolveProviderForAddAsync(
        Guid tenantId,
        NewProviderData np,
        Guid? userId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(np.Npi))
        {
            var byNpi = await _networks.GetProviderByNpiAsync(np.Npi, ct);
            if (byNpi is not null)
            {
                _logger.LogInformation(
                    "Provider NPI {Npi} already exists (Id={ProviderId}); reusing instead of creating duplicate.",
                    np.Npi, byNpi.Id);
                return byNpi;
            }
        }

        var byEmail = await _networks.GetProviderByTenantEmailAsync(tenantId, np.Email, ct);
        if (byEmail is not null)
        {
            _logger.LogInformation(
                "Provider email {Email} already exists for tenant {TenantId} (Id={ProviderId}); reusing existing provider.",
                np.Email, tenantId, byEmail.Id);
            return byEmail;
        }

        var provider = Provider.Create(
            tenantId: tenantId,
            name: BuildProviderDisplayName(np.Title, np.FirstName, np.LastName),
            firstName: np.FirstName,
            lastName: np.LastName,
            title: np.Title,
            organizationName: np.OrganizationName,
            email: np.Email.Trim().ToLowerInvariant(),
            phone: np.Phone,
            addressLine1: np.AddressLine1,
            city: np.City,
            state: np.State,
            postalCode: np.PostalCode,
            isActive: np.IsActive,
            acceptingReferrals: np.AcceptingReferrals,
            createdByUserId: userId,
            latitude: np.Latitude,
            longitude: np.Longitude,
            geoPointSource: np.GeoPointSource,
            npi: NormalizeOptional(np.Npi));

        await _networks.AddProviderToRegistryAsync(provider, ct);

        if (np.CategoryCodes is { Count: > 0 } codes)
        {
            var categoryEntities = await _categories.GetByCodesAsync(codes, ct);
            var orderedIds = new List<Guid>();

            if (!string.IsNullOrWhiteSpace(np.PrimaryCategoryCode))
            {
                var primary = categoryEntities.FirstOrDefault(
                    c => string.Equals(c.Code, np.PrimaryCategoryCode, StringComparison.OrdinalIgnoreCase));
                if (primary is not null) orderedIds.Add(primary.Id);
            }

            orderedIds.AddRange(categoryEntities
                .Where(c => !string.Equals(c.Code, np.PrimaryCategoryCode, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id));

            if (orderedIds.Count > 0)
                await _networks.SyncProviderCategoriesAsync(provider.Id, orderedIds, ct);
        }

        var specialtyIds = await ResolveSpecialtyIdsByCodesAsync(
            np.SpecialtyCodes,
            np.PrimarySpecialtyCode,
            requireAtLeastOne: true,
            ct);
        await _networks.SyncProviderSpecialtiesAsync(provider.Id, specialtyIds, ct);

        _logger.LogInformation(
            "New provider {ProviderId} ({Name}) registered in shared registry by tenant {TenantId}.",
            provider.Id, provider.Name, tenantId);

        return provider;
    }

    private ImportProviderResolution ResolveImportProvider(
        Guid tenantId,
        Guid? userId,
        ProviderImportNormalizedRow normalized,
        Dictionary<string, Provider> providersByNpi,
        Dictionary<string, Provider> providersByEmail)
    {
        if (!string.IsNullOrWhiteSpace(normalized.Npi) &&
            providersByNpi.TryGetValue(normalized.Npi, out var byNpi))
        {
            return new ImportProviderResolution(
                byNpi,
                "reused_npi",
                "Matched existing provider by NPI.");
        }

        if (providersByEmail.TryGetValue(normalized.Email, out var byEmail))
        {
            return new ImportProviderResolution(
                byEmail,
                "reused_email",
                "Matched existing provider by tenant email.");
        }

        var provider = Provider.Create(
            tenantId: tenantId,
            name: $"{normalized.FirstName} {normalized.LastName}".Trim(),
            firstName: normalized.FirstName,
            lastName: normalized.LastName,
            organizationName: normalized.OrganizationName,
            email: normalized.Email,
            phone: normalized.Phone,
            addressLine1: normalized.AddressLine1,
            city: normalized.City,
            state: normalized.State,
            postalCode: normalized.PostalCode,
            isActive: normalized.IsActive,
            acceptingReferrals: normalized.AcceptingReferrals,
            createdByUserId: userId,
            npi: normalized.Npi);

        return new ImportProviderResolution(
            provider,
            "created",
            "Provider will be created and linked to the network.");
    }

    private static void CacheResolvedProvider(
        Provider provider,
        Dictionary<string, Provider> providersByNpi,
        Dictionary<string, Provider> providersByEmail)
    {
        if (!string.IsNullOrWhiteSpace(provider.Npi))
            providersByNpi[provider.Npi] = provider;

        providersByEmail[provider.Email] = provider;
    }

    private static bool TryNormalizeImportRow(
        ProviderImportParsedRow parsedRow,
        out ProviderImportNormalizedRow normalized,
        out List<string> errors)
    {
        errors = [];

        var tenantId = ParseRequiredGuid(parsedRow.TenantId, "tenantId is required and must be a valid GUID.", errors);
        var firstName = NormalizeRequired(parsedRow.FirstName, "Provider first name is required.", errors);
        var lastName = NormalizeRequired(parsedRow.LastName, "Provider last name is required.", errors);
        var email = NormalizeRequired(parsedRow.Email, "Provider email is required.", errors, NormalizeEmail);
        var phone = NormalizeRequired(parsedRow.Phone, "Provider phone is required.", errors);
        var addressLine1 = NormalizeRequired(parsedRow.AddressLine1, "Address is required.", errors);
        var city = NormalizeRequired(parsedRow.City, "City is required.", errors);
        var state = NormalizeRequired(parsedRow.State, "State is required.", errors, v => v.Trim().ToUpperInvariant());
        var postalCode = NormalizeRequired(parsedRow.PostalCode, "Postal code is required.", errors);

        if (!TryParseOptionalBoolean(parsedRow.IsActiveRaw, defaultValue: true, out var isActive, out var isActiveError))
            errors.Add(isActiveError);

        if (!TryParseOptionalBoolean(parsedRow.AcceptingReferralsRaw, defaultValue: true, out var acceptingReferrals, out var acceptingError))
            errors.Add(acceptingError);

        if (errors.Count > 0)
        {
            normalized = default!;
            return false;
        }

        normalized = new ProviderImportNormalizedRow(
            TenantId: tenantId!.Value,
            FirstName: firstName!,
            LastName: lastName!,
            OrganizationName: NormalizeOptional(parsedRow.OrganizationName),
            Npi: NormalizeOptional(parsedRow.Npi),
            Email: email!,
            Phone: phone!,
            AddressLine1: addressLine1!,
            City: city!,
            State: state!,
            PostalCode: postalCode!,
            IsActive: isActive,
            AcceptingReferrals: acceptingReferrals);

        return true;
    }

    private static Guid? ParseRequiredGuid(string? value, string errorMessage, List<string> errors)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null || !Guid.TryParse(normalized, out var parsed))
        {
            errors.Add(errorMessage);
            return null;
        }

        return parsed;
    }

    private static string? NormalizeRequired(
        string? value,
        string errorMessage,
        List<string> errors,
        Func<string, string?>? normalize = null)
    {
        var normalized = normalize is null ? NormalizeOptional(value) : NormalizeOptional(value) is { } text ? normalize(text) : null;
        if (string.IsNullOrWhiteSpace(normalized))
            errors.Add(errorMessage);
        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized?.ToLowerInvariant();
    }

    private static string BuildProviderDisplayName(string? title, string firstName, string lastName)
    {
        return string.Join(" ", new[] { NormalizeOptional(title), NormalizeOptional(firstName), NormalizeOptional(lastName) }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static bool TryParseOptionalBoolean(
        string? raw,
        bool defaultValue,
        out bool value,
        out string error)
    {
        error = string.Empty;
        var normalized = NormalizeOptional(raw);
        if (normalized is null)
        {
            value = defaultValue;
            return true;
        }

        switch (normalized.ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "y":
                value = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "n":
                value = false;
                return true;
            default:
                value = defaultValue;
                error = $"Boolean value '{raw}' is invalid. Use true/false, 1/0, yes/no, or y/n.";
                return false;
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Validation failed.",
                new() { ["name"] = ["Network name is required."] });
        if (name.Trim().Length > 200)
            throw new ValidationException("Validation failed.",
                new() { ["name"] = ["Network name must be 200 characters or fewer."] });
    }

    private static void ValidateNewProvider(NewProviderData np)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(np.FirstName))
            errors["firstName"] = ["Provider first name is required."];
        if (string.IsNullOrWhiteSpace(np.LastName))
            errors["lastName"] = ["Provider last name is required."];
        if (string.IsNullOrWhiteSpace(np.Email))
            errors["email"] = ["Provider email is required."];
        if (string.IsNullOrWhiteSpace(np.Phone))
            errors["phone"] = ["Provider phone is required."];
        if (string.IsNullOrWhiteSpace(np.AddressLine1))
            errors["addressLine1"] = ["Address is required."];
        if (string.IsNullOrWhiteSpace(np.City))
            errors["city"] = ["City is required."];
        if (string.IsNullOrWhiteSpace(np.State))
            errors["state"] = ["State is required."];
        if (string.IsNullOrWhiteSpace(np.PostalCode))
            errors["postalCode"] = ["Postal code is required."];
        if (np.Title?.Trim().Length > 50)
            errors["title"] = ["Title must be 50 characters or fewer."];
        if (np.SpecialtyCodes is null || np.SpecialtyCodes.Count == 0)
            errors["specialtyCodes"] = ["Select at least one specialty."];
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }

    private static void ValidateNetworkProviderFields(
        string firstName,
        string lastName,
        string email,
        string phone,
        string addressLine1,
        string city,
        string state,
        string postalCode,
        string? title)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(firstName))
            errors["firstName"] = ["Provider first name is required."];
        if (string.IsNullOrWhiteSpace(lastName))
            errors["lastName"] = ["Provider last name is required."];
        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = ["Provider email is required."];
        if (string.IsNullOrWhiteSpace(phone))
            errors["phone"] = ["Provider phone is required."];
        if (string.IsNullOrWhiteSpace(addressLine1))
            errors["addressLine1"] = ["Address is required."];
        if (string.IsNullOrWhiteSpace(city))
            errors["city"] = ["City is required."];
        if (string.IsNullOrWhiteSpace(state))
            errors["state"] = ["State is required."];
        if (string.IsNullOrWhiteSpace(postalCode))
            errors["postalCode"] = ["Postal code is required."];
        if (title?.Trim().Length > 50)
            errors["title"] = ["Title must be 50 characters or fewer."];
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }

    private static void ValidateGeoFields(double? latitude, double? longitude, string? geoPointSource)
    {
        var errors = new Dictionary<string, string[]>();
        ProviderGeoHelper.ValidateGeoFields(latitude, longitude, geoPointSource, errors);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }

    private async Task<List<Guid>> ValidateSpecialtyIdsAsync(List<Guid> specialtyIds, CancellationToken ct)
    {
        var distinct = specialtyIds.Distinct().ToList();
        if (distinct.Count == 0)
            throw new ValidationException("Validation failed.",
                new() { ["specialtyIds"] = ["Select at least one specialty."] });

        var active = await _specialties.GetActiveByIdsAsync(distinct, ct);
        if (active.Count != distinct.Count)
            throw new ValidationException("Validation failed.",
                new() { ["specialtyIds"] = ["One or more selected specialties are inactive or invalid."] });

        return distinct;
    }

    private async Task<List<Guid>> ResolveSpecialtyIdsByCodesAsync(
        List<string>? specialtyCodes,
        string? primarySpecialtyCode,
        bool requireAtLeastOne,
        CancellationToken ct)
    {
        if (specialtyCodes is null || specialtyCodes.Count == 0)
        {
            if (!requireAtLeastOne) return [];
            throw new ValidationException("Validation failed.",
                new() { ["specialtyCodes"] = ["Select at least one specialty."] });
        }

        var specialtyEntities = await _specialties.GetActiveByCodesAsync(specialtyCodes, ct);
        if (specialtyEntities.Count != specialtyCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(Specialty.NormalizeCode).Distinct().Count())
            throw new ValidationException("Validation failed.",
                new() { ["specialtyCodes"] = ["One or more selected specialties are inactive or invalid."] });

        var orderedIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(primarySpecialtyCode))
        {
            var primary = specialtyEntities.FirstOrDefault(
                s => string.Equals(s.Code, Specialty.NormalizeCode(primarySpecialtyCode), StringComparison.Ordinal));
            if (primary is not null) orderedIds.Add(primary.Id);
        }

        orderedIds.AddRange(specialtyEntities
            .Where(s => !orderedIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .Select(s => s.Id));

        return orderedIds;
    }

    private static List<SpecialtyResponse> MapSpecialties(IEnumerable<ProviderSpecialty> providerSpecialties)
    {
        return providerSpecialties
            .Where(ps => ps.Specialty != null)
            .OrderByDescending(ps => ps.IsPrimary)
            .ThenBy(ps => ps.Specialty!.Name)
            .Select(ps => SpecialtyService.ToResponse(ps.Specialty!))
            .ToList();
    }

    private sealed record ImportProviderResolution(
        Provider Provider,
        string Status,
        string Message);
}
