using Intake.Contracts.Configuration;
using Intake.Contracts.Sources;
using Intake.Application.Configuration;

namespace Intake.Application.Sources;

public sealed record IntakeSourceCodeDescriptor(string Code, string DisplayName);

public interface IIntakeSourceTypeRegistry
{
    IReadOnlyList<IntakeSourceCodeDescriptor> Supported { get; }
    string GetRequired(string code);
}

public interface IIntakeSourcePurposeRegistry
{
    IReadOnlyList<IntakeSourceCodeDescriptor> Supported { get; }
    string GetRequired(string code);
}

public interface IIntakeSourceProfileCompatibilityRegistry
{
    bool IsCompatible(string purpose, string processingProfileCode);
    void EnsureCompatible(string purpose, string processingProfileCode);
}

public sealed class IntakeSourceTypeRegistry : IIntakeSourceTypeRegistry
{
    public IReadOnlyList<IntakeSourceCodeDescriptor> Supported { get; } =
        [
            new(IntakeSourceTypes.Email, "Email"),
            new(IntakeSourceTypes.Manual, "Manual"),
        ];

    public string GetRequired(string code)
    {
        var normalized = Normalize(code);
        return Supported.Any(item => item.Code == normalized)
            ? normalized
            : throw ConfigurationError(
                "UNSUPPORTED_SOURCE_TYPE",
                $"Unsupported Intake source type '{code}'.");
    }

    private static string Normalize(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;

    private static IntakeConfigurationException ConfigurationError(string code, string message) =>
        IntakeConfigurationException.BadRequest(code, message);
}

public sealed class IntakeSourcePurposeRegistry : IIntakeSourcePurposeRegistry
{
    public IReadOnlyList<IntakeSourceCodeDescriptor> Supported { get; } =
        [new(IntakeSourcePurposes.LienIntake, "Lien Intake")];

    public string GetRequired(string code)
    {
        var normalized = Normalize(code);
        return Supported.Any(item => item.Code == normalized)
            ? normalized
            : throw IntakeConfigurationException.BadRequest(
                "UNSUPPORTED_SOURCE_PURPOSE",
                $"Unsupported Intake source purpose '{code}'.");
    }

    private static string Normalize(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
}

public sealed class IntakeSourceProfileCompatibilityRegistry
    : IIntakeSourceProfileCompatibilityRegistry
{
    public bool IsCompatible(string purpose, string processingProfileCode) =>
        string.Equals(purpose, IntakeSourcePurposes.LienIntake, StringComparison.Ordinal) &&
        string.Equals(processingProfileCode, ProcessingProfileCodes.LienIntakeV1, StringComparison.Ordinal);

    public void EnsureCompatible(string purpose, string processingProfileCode)
    {
        if (!IsCompatible(purpose, processingProfileCode))
        {
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_SOURCE_PROFILE_INCOMPATIBLE",
                $"Purpose '{purpose}' cannot use processing profile '{processingProfileCode}'.");
        }
    }
}