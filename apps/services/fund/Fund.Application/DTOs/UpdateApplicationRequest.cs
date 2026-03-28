namespace Fund.Application.DTOs;

public record UpdateApplicationRequest(
    string ApplicantFirstName,
    string ApplicantLastName,
    string Email,
    string Phone,
    string Status);
