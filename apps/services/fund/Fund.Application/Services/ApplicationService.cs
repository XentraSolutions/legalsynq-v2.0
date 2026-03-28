using BuildingBlocks.Exceptions;
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
        ValidateCreate(request);

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

    public async Task<ApplicationResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid updatedByUserId,
        UpdateApplicationRequest request,
        CancellationToken ct = default)
    {
        ValidateUpdate(request);

        var application = await _repository.GetByIdAsync(tenantId, id, ct);
        if (application is null)
            throw new NotFoundException($"Application '{id}' was not found.");

        application.Update(
            request.ApplicantFirstName,
            request.ApplicantLastName,
            request.Email,
            request.Phone,
            request.Status,
            updatedByUserId);

        await _repository.UpdateAsync(application, ct);
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

    private static void ValidateCreate(CreateApplicationRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateName(r.ApplicantFirstName, "applicantFirstName", "Applicant first name", errors);
        ValidateName(r.ApplicantLastName, "applicantLastName", "Applicant last name", errors);
        ValidateEmail(r.Email, errors);
        ValidatePhone(r.Phone, errors);

        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }

    private static void ValidateUpdate(UpdateApplicationRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateName(r.ApplicantFirstName, "applicantFirstName", "Applicant first name", errors);
        ValidateName(r.ApplicantLastName, "applicantLastName", "Applicant last name", errors);
        ValidateEmail(r.Email, errors);
        ValidatePhone(r.Phone, errors);
        ValidateStatus(r.Status, errors);

        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }

    private static void ValidateName(string? value, string field, string label, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = [$"{label} is required."];
        else if (value.Length > 100)
            errors[field] = [$"{label} must not exceed 100 characters."];
    }

    private static void ValidateEmail(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors["email"] = ["Email is required."];
            return;
        }

        if (value.Length > 320)
        {
            errors["email"] = ["Email must not exceed 320 characters."];
            return;
        }

        try { _ = new System.Net.Mail.MailAddress(value); }
        catch { errors["email"] = ["Email is not a valid email address."]; }
    }

    private static void ValidatePhone(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors["phone"] = ["Phone is required."];
        else if (value.Length > 50)
            errors["phone"] = ["Phone must not exceed 50 characters."];
    }

    private static void ValidateStatus(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors["status"] = ["Status is required."];
            return;
        }

        if (!Domain.Application.ValidStatuses.Contains(value))
            errors["status"] = [$"Status must be one of: {string.Join(", ", Domain.Application.ValidStatuses)}."];
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
