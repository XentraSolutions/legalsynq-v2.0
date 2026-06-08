namespace Tenant.Application.DTOs;

public record CareConnectAccessCodeMetadataResponse(
    bool Configured,
    int Version,
    DateTime? UpdatedAtUtc);

public record SetCareConnectAccessCodeRequest(string Code);

public record SetCareConnectAccessCodeResponse(
    bool Configured,
    int Version,
    DateTime? UpdatedAtUtc,
    string RevealedCode);

public record CareConnectAccessCodeStatusResponse(
    bool Configured,
    int Version);

public record VerifyCareConnectAccessCodeRequest(string Code);

public record VerifyCareConnectAccessCodeResponse(
    bool Ok,
    bool Configured,
    int Version);
