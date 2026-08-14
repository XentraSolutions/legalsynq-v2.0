using Intake.Contracts.Configuration;

namespace Intake.Application.Configuration;

public sealed record ProcessingProfileDescriptor(
    string Code,
    string DisplayName,
    string Description,
    int Version,
    bool IsActive,
    bool IsSystemDefined);

public interface IProcessingProfileRegistry
{
    IReadOnlyList<ProcessingProfileDescriptor> SupportedProfiles { get; }
    ProcessingProfileDescriptor GetRequired(string code);
    LienIntakeV1Configuration ValidateAndDeserialize(string code, string? configurationJson);
    string Serialize(string code, LienIntakeV1Configuration configuration);
}