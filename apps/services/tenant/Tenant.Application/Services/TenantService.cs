using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _repository;

    public TenantService(ITenantRepository repository) => _repository = repository;

    public async Task<TenantResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(id, ct);
        return tenant is null ? null : ToResponse(tenant);
    }

    public async Task<TenantResponse?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var tenant = await _repository.GetByCodeAsync(code.ToLowerInvariant(), ct);
        return tenant is null ? null : ToResponse(tenant);
    }

    public async Task<(List<TenantResponse> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)    page     = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (items, total) = await _repository.ListAsync(page, pageSize, ct);
        return (items.Select(ToResponse).ToList(), total);
    }

    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code,        nameof(request.Code));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        var code = request.Code.Trim().ToLowerInvariant();

        if (await _repository.ExistsByCodeAsync(code, ct))
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        var tenant = Domain.Tenant.Create(
            code,
            request.DisplayName,
            request.LegalName,
            request.Subdomain);

        await _repository.AddAsync(tenant, ct);
        return ToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        var tenant = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        tenant.UpdateProfile(request.DisplayName, request.LegalName, request.TimeZone);

        if (request.Subdomain is not null)
            tenant.SetSubdomain(request.Subdomain);

        if (request.Status is not null)
        {
            if (!Enum.TryParse<TenantStatus>(request.Status, ignoreCase: true, out var status))
                throw new ValidationException($"Invalid status '{request.Status}'.",
                    new Dictionary<string, string[]> { ["status"] = [$"'{request.Status}' is not a valid status value."] });
            tenant.SetStatus(status);
        }

        if (request.LogoDocumentId is not null)
            tenant.SetLogo(request.LogoDocumentId);

        if (request.LogoWhiteDocumentId is not null)
            tenant.SetLogoWhite(request.LogoWhiteDocumentId);

        await _repository.UpdateAsync(tenant, ct);
        return ToResponse(tenant);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        tenant.SetStatus(TenantStatus.Inactive);
        await _repository.UpdateAsync(tenant, ct);
    }

    private static TenantResponse ToResponse(Domain.Tenant t) => new(
        t.Id,
        t.Code,
        t.DisplayName,
        t.LegalName,
        t.Status.ToString(),
        t.Subdomain,
        t.LogoDocumentId,
        t.LogoWhiteDocumentId,
        t.TimeZone,
        t.CreatedAtUtc,
        t.UpdatedAtUtc);
}
