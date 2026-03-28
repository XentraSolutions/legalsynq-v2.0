using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilities;

    public FacilityService(IFacilityRepository facilities)
    {
        _facilities = facilities;
    }

    public async Task<List<FacilityResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var facilities = await _facilities.GetAllByTenantAsync(tenantId, ct);
        return facilities.Select(ToResponse).ToList();
    }

    public async Task<FacilityResponse> CreateAsync(Guid tenantId, Guid? userId, CreateFacilityRequest request, CancellationToken ct = default)
    {
        Validate(request.Name, request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone);

        var facility = Facility.Create(
            tenantId,
            request.Name,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.Phone,
            request.IsActive,
            userId);

        await _facilities.AddAsync(facility, ct);
        return ToResponse(facility);
    }

    public async Task<FacilityResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateFacilityRequest request, CancellationToken ct = default)
    {
        var facility = await _facilities.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Facility '{id}' was not found.");

        Validate(request.Name, request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone);

        facility.Update(request.Name, request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone, request.IsActive, userId);
        await _facilities.UpdateAsync(facility, ct);
        return ToResponse(facility);
    }

    private static void Validate(string name, string addressLine1, string city, string state, string postalCode, string? phone)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "Name is required." };
        else if (name.Trim().Length > 200)
            errors["name"] = new[] { "Name must not exceed 200 characters." };

        if (string.IsNullOrWhiteSpace(addressLine1))
            errors["addressLine1"] = new[] { "AddressLine1 is required." };

        if (string.IsNullOrWhiteSpace(city))
            errors["city"] = new[] { "City is required." };

        if (string.IsNullOrWhiteSpace(state))
            errors["state"] = new[] { "State is required." };

        if (string.IsNullOrWhiteSpace(postalCode))
            errors["postalCode"] = new[] { "PostalCode is required." };

        if (phone is not null && phone.Trim().Length > 50)
            errors["phone"] = new[] { "Phone must not exceed 50 characters." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static FacilityResponse ToResponse(Facility f) => new()
    {
        Id = f.Id,
        TenantId = f.TenantId,
        Name = f.Name,
        AddressLine1 = f.AddressLine1,
        City = f.City,
        State = f.State,
        PostalCode = f.PostalCode,
        Phone = f.Phone,
        IsActive = f.IsActive
    };
}
