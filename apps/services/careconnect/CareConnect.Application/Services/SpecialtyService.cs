using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class SpecialtyService : ISpecialtyService
{
    private readonly ISpecialtyRepository _specialties;

    public SpecialtyService(ISpecialtyRepository specialties)
    {
        _specialties = specialties;
    }

    public async Task<List<SpecialtyResponse>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var specialties = await _specialties.GetAllAsync(includeInactive, ct);
        return specialties.Select(ToResponse).ToList();
    }

    public async Task<SpecialtyResponse> CreateAsync(CreateSpecialtyRequest request, CancellationToken ct = default)
    {
        Validate(request.Name, request.Code);

        if (await _specialties.CodeExistsAsync(request.Code, ct: ct))
            throw new ValidationException("Duplicate specialty code.",
                new() { ["code"] = [$"A specialty with code '{Specialty.NormalizeCode(request.Code)}' already exists."] });

        var specialty = Specialty.Create(request.Name, request.Code, request.Description);
        await _specialties.AddAsync(specialty, ct);
        await _specialties.SaveChangesAsync(ct);
        return ToResponse(specialty);
    }

    public async Task<SpecialtyResponse> UpdateAsync(Guid id, UpdateSpecialtyRequest request, CancellationToken ct = default)
    {
        Validate(request.Name, request.Code);

        var specialty = await _specialties.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Specialty '{id}' was not found.");

        if (await _specialties.CodeExistsAsync(request.Code, excludeId: id, ct: ct))
            throw new ValidationException("Duplicate specialty code.",
                new() { ["code"] = [$"A specialty with code '{Specialty.NormalizeCode(request.Code)}' already exists."] });

        specialty.Update(request.Name, request.Code, request.Description, request.IsActive);
        await _specialties.SaveChangesAsync(ct);
        return ToResponse(specialty);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var specialty = await _specialties.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Specialty '{id}' was not found.");

        specialty.Deactivate();
        await _specialties.SaveChangesAsync(ct);
    }

    internal static SpecialtyResponse ToResponse(Specialty s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Code = s.Code,
        Description = s.Description,
        IsActive = s.IsActive
    };

    private static void Validate(string name, string code)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = ["Name is required."];
        else if (name.Trim().Length > 200)
            errors["name"] = ["Name must not exceed 200 characters."];

        if (string.IsNullOrWhiteSpace(code))
            errors["code"] = ["Code is required."];
        else if (Specialty.NormalizeCode(code).Length > 50)
            errors["code"] = ["Code must not exceed 50 characters."];

        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }
}
