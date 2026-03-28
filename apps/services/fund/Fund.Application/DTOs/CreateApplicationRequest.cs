namespace Fund.Application.DTOs;

public record CreateApplicationRequest(
    string ApplicantFirstName,
    string ApplicantLastName,
    string Email,
    string Phone);
