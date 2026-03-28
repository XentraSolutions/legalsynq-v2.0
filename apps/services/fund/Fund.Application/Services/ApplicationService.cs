using Fund.Application.DTOs;
using Fund.Application.Interfaces;

namespace Fund.Application.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _repository;

    public ApplicationService(IApplicationRepository repository) => _repository = repository;

    public async Task<ApplicationResponse> CreateAsync(
        Guid tenantId,
        Guid userId,
        CreateApplicationRequest request,
        CancellationToken ct = default)
    {
        var applicationNumber = GenerateApplicationNumber();

        var application = Domain.Application.Create(
            tenantId,
            applicationNumber,
            request.ApplicantFirstName,
            request.ApplicantLastName,
            request.Email,
            request.Phone,
            userId);

        await _repository.AddAsync(application, ct);
        return ToResponse(application);
    }

    public async Task<List<ApplicationResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var apps = await _repository.GetAllByTenantAsync(tenantId, ct);
        return apps.Select(ToResponse).ToList();
    }

    public async Task<ApplicationResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var app = await _repository.GetByIdAsync(tenantId, id, ct);
        return app is null ? null : ToResponse(app);
    }

    private static string GenerateApplicationNumber()
    {
        var year = DateTime.UtcNow.Year;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"FUND-{year}-{suffix}";
    }

    private static ApplicationResponse ToResponse(Domain.Application a) => new(
        a.Id,
        a.TenantId,
        a.ApplicationNumber,
        a.ApplicantFirstName,
        a.ApplicantLastName,
        a.Email,
        a.Phone,
        a.Status,
        a.CreatedByUserId,
        a.UpdatedByUserId,
        a.CreatedAtUtc,
        a.UpdatedAtUtc);
}
