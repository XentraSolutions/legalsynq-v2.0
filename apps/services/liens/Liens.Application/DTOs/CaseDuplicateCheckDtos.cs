namespace Liens.Application.DTOs;

public sealed record class CaseDuplicateCheckRequest
{
    public string ClientFirstName { get; init; } = string.Empty;
    public string ClientLastName { get; init; } = string.Empty;
    public DateOnly? ClientDob { get; init; }
    public DateOnly? DateOfIncident { get; init; }
}

public sealed class CaseDuplicateCheckResponse
{
    public bool IsDuplicate { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<CaseDuplicateMatchResponse> Matches { get; init; } = [];
}

public sealed class CaseDuplicateMatchResponse
{
    public Guid Id { get; init; }
    public string CaseNumber { get; init; } = string.Empty;
    public string ClientFirstName { get; init; } = string.Empty;
    public string ClientLastName { get; init; } = string.Empty;
    public string ClientDisplayName { get; init; } = string.Empty;
    public DateOnly? ClientDob { get; init; }
    public DateOnly? DateOfIncident { get; init; }
    public string Status { get; init; } = string.Empty;
}
