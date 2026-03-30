using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Helpers;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CareConnect.Application.Services;

public class ProviderService : IProviderService
{
    private readonly IProviderRepository _providers;
    private readonly ILogger<ProviderService> _logger;

    public ProviderService(IProviderRepository providers, ILogger<ProviderService> logger)
    {
        _providers = providers;
        _logger    = logger;
    }

    public async Task<PagedResponse<ProviderResponse>> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        ValidatePaging(query.Page, query.PageSize);
        ValidateSearchGeo(query);

        var (items, totalCount) = await _providers.SearchAsync(tenantId, query, ct);

        return new PagedResponse<ProviderResponse>
        {
            Items      = items.Select(ToResponse).ToList(),
            Page       = query.Page,
            PageSize   = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<ProviderMarkerResponse>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        ValidateSearchGeo(query);

        var items = await _providers.GetMarkersAsync(tenantId, query, ct);
        return items.Select(ToMarker).ToList();
    }

    public async Task<ProviderResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Provider '{id}' was not found.");
        return ToResponse(provider);
    }

    public async Task<ProviderResponse> CreateAsync(Guid tenantId, Guid? userId, CreateProviderRequest request, CancellationToken ct = default)
    {
        ValidateFields(request.Name, request.Email, request.Phone, request.AddressLine1, request.City, request.State, request.PostalCode);
        ValidateGeoFields(request.Latitude, request.Longitude, request.GeoPointSource);

        var provider = Provider.Create(
            tenantId,
            request.Name,
            request.OrganizationName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.IsActive,
            request.AcceptingReferrals,
            userId,
            request.Latitude,
            request.Longitude,
            request.GeoPointSource);

        // Phase D / Step 6: link to Identity Organization before persisting so that
        // the OrganizationId is captured in the initial INSERT, eliminating the
        // redundant UPDATE that previously followed AddAsync.
        if (request.OrganizationId.HasValue)
        {
            provider.LinkOrganization(request.OrganizationId.Value);
            _logger.LogDebug(
                "Provider {ProviderId} linking to Identity Organization {OrganizationId}.",
                provider.Id, request.OrganizationId.Value);
        }

        await _providers.AddAsync(provider, ct);

        if (request.CategoryIds.Count > 0)
            await _providers.SyncCategoriesAsync(provider.Id, request.CategoryIds, ct);

        var loaded = await _providers.GetByIdAsync(tenantId, provider.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ProviderResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateProviderRequest request, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Provider '{id}' was not found.");

        ValidateFields(request.Name, request.Email, request.Phone, request.AddressLine1, request.City, request.State, request.PostalCode);
        ValidateGeoFields(request.Latitude, request.Longitude, request.GeoPointSource);

        provider.Update(
            request.Name,
            request.OrganizationName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.IsActive,
            request.AcceptingReferrals,
            userId,
            request.Latitude,
            request.Longitude,
            request.GeoPointSource);

        // Phase D: link to Identity Organization if supplied.
        if (request.OrganizationId.HasValue)
        {
            provider.LinkOrganization(request.OrganizationId.Value);
            _logger.LogDebug(
                "Provider {ProviderId} org linkage updated to Identity Organization {OrganizationId}.",
                provider.Id, request.OrganizationId.Value);
        }

        await _providers.UpdateAsync(provider, ct);
        await _providers.SyncCategoriesAsync(provider.Id, request.CategoryIds, ct);

        var loaded = await _providers.GetByIdAsync(tenantId, provider.Id, ct);
        return ToResponse(loaded!);
    }

    private static void ValidateSearchGeo(GetProvidersQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        bool hasRadius   = query.Latitude.HasValue || query.Longitude.HasValue || query.RadiusMiles.HasValue;
        bool hasViewport = query.NorthLat.HasValue  || query.SouthLat.HasValue  || query.EastLng.HasValue || query.WestLng.HasValue;

        ProviderGeoHelper.ValidateNoConflict(hasRadius, hasViewport, errors);

        if (!errors.ContainsKey("search"))
        {
            ProviderGeoHelper.ValidateGeoSearch(query.Latitude, query.Longitude, query.RadiusMiles, errors);
            ProviderGeoHelper.ValidateViewport(query.NorthLat, query.SouthLat, query.EastLng, query.WestLng, errors);
        }

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
            errors["page"] = new[] { "Page must be >= 1." };

        if (pageSize < 1)
            errors["pageSize"] = new[] { "PageSize must be >= 1." };
        else if (pageSize > 100)
            errors["pageSize"] = new[] { "PageSize must not exceed 100." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateGeoFields(double? latitude, double? longitude, string? geoPointSource)
    {
        var errors = new Dictionary<string, string[]>();
        ProviderGeoHelper.ValidateGeoFields(latitude, longitude, geoPointSource, errors);
        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateFields(string name, string email, string phone, string addressLine1, string city, string state, string postalCode)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "Name is required." };
        else if (name.Trim().Length > 200)
            errors["name"] = new[] { "Name must not exceed 200 characters." };

        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = new[] { "Email is required." };
        else if (email.Trim().Length > 320)
            errors["email"] = new[] { "Email must not exceed 320 characters." };
        else if (!Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors["email"] = new[] { "Email format is invalid." };

        if (string.IsNullOrWhiteSpace(phone))
            errors["phone"] = new[] { "Phone is required." };
        else if (phone.Trim().Length > 50)
            errors["phone"] = new[] { "Phone must not exceed 50 characters." };

        if (string.IsNullOrWhiteSpace(addressLine1))
            errors["addressLine1"] = new[] { "AddressLine1 is required." };

        if (string.IsNullOrWhiteSpace(city))
            errors["city"] = new[] { "City is required." };

        if (string.IsNullOrWhiteSpace(state))
            errors["state"] = new[] { "State is required." };

        if (string.IsNullOrWhiteSpace(postalCode))
            errors["postalCode"] = new[] { "PostalCode is required." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static ProviderResponse ToResponse(Provider p)
    {
        var categories = p.ProviderCategories
            .Where(pc => pc.Category != null)
            .Select(pc => pc.Category!.Name)
            .OrderBy(n => n)
            .ToList();

        var primary  = categories.FirstOrDefault();
        var label    = p.OrganizationName ?? p.Name;
        var subtitle = BuildSubtitle(p.City, p.State, primary);

        return new ProviderResponse
        {
            Id               = p.Id,
            TenantId         = p.TenantId,
            Name             = p.Name,
            OrganizationName = p.OrganizationName,
            OrganizationId   = p.OrganizationId,
            Email            = p.Email,
            Phone            = p.Phone,
            AddressLine1     = p.AddressLine1,
            City             = p.City,
            State            = p.State,
            PostalCode       = p.PostalCode,
            IsActive         = p.IsActive,
            AcceptingReferrals = p.AcceptingReferrals,
            Categories       = categories,
            Latitude         = p.Latitude,
            Longitude        = p.Longitude,
            GeoPointSource   = p.GeoPointSource,
            GeoUpdatedAtUtc  = p.GeoUpdatedAtUtc,
            HasGeoLocation   = p.Latitude.HasValue && p.Longitude.HasValue,
            PrimaryCategory  = primary,
            DisplayLabel     = label,
            MarkerSubtitle   = subtitle
        };
    }

    private static ProviderMarkerResponse ToMarker(Provider p)
    {
        var categories = p.ProviderCategories
            .Where(pc => pc.Category != null)
            .Select(pc => pc.Category!.Name)
            .OrderBy(n => n)
            .ToList();

        var primary  = categories.FirstOrDefault();
        var label    = p.OrganizationName ?? p.Name;
        var subtitle = BuildSubtitle(p.City, p.State, primary);

        return new ProviderMarkerResponse
        {
            Id               = p.Id,
            Name             = p.Name,
            OrganizationName = p.OrganizationName,
            DisplayLabel     = label,
            MarkerSubtitle   = subtitle,
            City             = p.City,
            State            = p.State,
            AddressLine1     = p.AddressLine1,
            PostalCode       = p.PostalCode,
            Email            = p.Email,
            Phone            = p.Phone,
            AcceptingReferrals = p.AcceptingReferrals,
            IsActive         = p.IsActive,
            Latitude         = p.Latitude!.Value,
            Longitude        = p.Longitude!.Value,
            GeoPointSource   = p.GeoPointSource,
            PrimaryCategory  = primary,
            Categories       = categories
        };
    }

    private static string BuildSubtitle(string city, string state, string? primaryCategory)
    {
        var location = $"{city}, {state}";
        return primaryCategory is null ? location : $"{location} · {primaryCategory}";
    }
}
