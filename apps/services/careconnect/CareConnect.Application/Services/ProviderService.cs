using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using System.Text.RegularExpressions;

namespace CareConnect.Application.Services;

public class ProviderService : IProviderService
{
    private readonly IProviderRepository _providers;

    public ProviderService(IProviderRepository providers)
    {
        _providers = providers;
    }

    public async Task<List<ProviderResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var providers = await _providers.GetAllByTenantAsync(tenantId, ct);
        return providers.Select(ToResponse).ToList();
    }

    public async Task<ProviderResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Provider '{id}' was not found.");
        return ToResponse(provider);
    }

    public async Task<ProviderResponse> CreateAsync(Guid tenantId, Guid? userId, CreateProviderRequest request, CancellationToken ct = default)
    {
        Validate(request.Name, request.Email, request.Phone, request.AddressLine1, request.City, request.State, request.PostalCode);

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
            userId);

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

        Validate(request.Name, request.Email, request.Phone, request.AddressLine1, request.City, request.State, request.PostalCode);

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
            userId);

        await _providers.UpdateAsync(provider, ct);
        await _providers.SyncCategoriesAsync(provider.Id, request.CategoryIds, ct);

        var loaded = await _providers.GetByIdAsync(tenantId, provider.Id, ct);
        return ToResponse(loaded!);
    }

    private static void Validate(string name, string email, string phone, string addressLine1, string city, string state, string postalCode)
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

    private static ProviderResponse ToResponse(Provider p) => new()
    {
        Id = p.Id,
        TenantId = p.TenantId,
        Name = p.Name,
        OrganizationName = p.OrganizationName,
        Email = p.Email,
        Phone = p.Phone,
        AddressLine1 = p.AddressLine1,
        City = p.City,
        State = p.State,
        PostalCode = p.PostalCode,
        IsActive = p.IsActive,
        AcceptingReferrals = p.AcceptingReferrals,
        Categories = p.ProviderCategories
            .Where(pc => pc.Category != null)
            .Select(pc => pc.Category!.Name)
            .OrderBy(n => n)
            .ToList()
    };
}
