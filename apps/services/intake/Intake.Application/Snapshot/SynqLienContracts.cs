using Intake.Contracts.Snapshot;

namespace Intake.Application.Snapshot;

public sealed class SynqLienDestinationOptions
{
    public const string SectionName = "Intake:SynqLien";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
    public string ServiceTokenAudience { get; set; } = "liens-service";
    public Guid? OrganizationId { get; set; }
}

public static class SynqLienFailureCodes
{
    public const string Disabled = "SYNQLIEN_DISABLED";
    public const string ConfigurationInvalid = "SYNQLIEN_CONFIGURATION_INVALID";
    public const string RoutingInvalid = "SYNQLIEN_ROUTING_INVALID";
    public const string DestinationUnavailable = "SYNQLIEN_DESTINATION_UNAVAILABLE";
    public const string DestinationRejected = "SYNQLIEN_DESTINATION_REJECTED";
    public const string PartialSuccess = "SYNQLIEN_PARTIAL_SUCCESS";
    public const string ReconciliationRequired = "SYNQLIEN_RECONCILIATION_REQUIRED";
}

public sealed record SynqLienCaseRequest(
    string CaseNumber,
    string ClientFirstName,
    string ClientLastName,
    string? ExternalReference,
    string? Title,
    DateOnly? ClientDob,
    string? ClientPhone,
    string? ClientEmail,
    string? ClientAddress,
    DateOnly? DateOfIncident,
    string? InsuranceCarrier,
    string? PolicyNumber,
    string? ClaimNumber,
    string? Description);

public sealed record SynqLienLienRequest(
    string LienNumber,
    string? ExternalReference,
    string LienType,
    Guid CaseId,
    Guid? FacilityId,
    decimal OriginalAmount,
    string? Jurisdiction,
    bool IsConfidential,
    string? SubjectFirstName,
    string? SubjectLastName,
    DateOnly? IncidentDate,
    DateOnly? InitialServiceDate,
    DateOnly? EndServiceDate,
    string? Description);

public sealed record SynqLienCaseResponse(Guid Id, string CaseNumber);
public sealed record SynqLienLienResponse(Guid Id, string LienNumber, Guid? CaseId);

public sealed record SynqLienCallResult<T>(
    bool Success,
    bool Retryable,
    int StatusCode,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage);

public interface ISynqLienClient
{
    Task<SynqLienCallResult<SynqLienCaseResponse>> GetCaseAsync(
        Guid tenantId, Guid caseId, string correlationId, CancellationToken cancellationToken);

    Task<SynqLienCallResult<SynqLienCaseResponse>> CreateCaseAsync(
        Guid tenantId, Guid actingUserId, string idempotencyKey, string correlationId,
        SynqLienCaseRequest request, CancellationToken cancellationToken);

    Task<SynqLienCallResult<SynqLienLienResponse>> CreateLienAsync(
        Guid tenantId, Guid actingUserId, string idempotencyKey, string correlationId,
        SynqLienLienRequest request, CancellationToken cancellationToken);
}

public sealed record SynqLienRouting(
    ApprovedSnapshotEntityDecision? CaseDecision,
    ApprovedSnapshotEntityDecision? FacilityDecision,
    Guid? ExistingCaseId,
    Guid? FacilityId,
    bool CreateCase);