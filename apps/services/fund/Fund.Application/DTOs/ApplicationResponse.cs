namespace Fund.Application.DTOs;

public record ApplicationResponse(
    Guid Id,
    Guid TenantId,
    string ApplicationNumber,
    string ApplicantFirstName,
    string ApplicantLastName,
    string Email,
    string Phone,
    string Status,
    Guid? CreatedByUserId,
    Guid? UpdatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
