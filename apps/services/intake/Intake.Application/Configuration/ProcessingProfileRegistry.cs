using System.Text.Json;
using System.Text.Json.Serialization;
using Intake.Contracts.Configuration;

namespace Intake.Application.Configuration;

public sealed class ProcessingProfileRegistry : IProcessingProfileRegistry
{
    private static readonly ProcessingProfileDescriptor LienIntakeV1Descriptor = new(
        ProcessingProfileCodes.LienIntakeV1,
        "Lien Intake V1",
        "Conservative configuration contract for future lien-intake processing.",
        1,
        IsActive: true,
        IsSystemDefined: true);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    public IReadOnlyList<ProcessingProfileDescriptor> SupportedProfiles { get; } =
        [LienIntakeV1Descriptor];

    public ProcessingProfileDescriptor GetRequired(string code)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length < 3 ||
            normalized.Length > 64 ||
            !char.IsLetter(normalized[0]) ||
            normalized.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_PROFILE_CODE",
                "Processing profile codes must match ^[A-Z][A-Z0-9_]{2,63}$.");
        }

        var descriptor = SupportedProfiles.FirstOrDefault(
            candidate => string.Equals(candidate.Code, normalized, StringComparison.Ordinal));

        return descriptor ?? throw IntakeConfigurationException.BadRequest(
            "UNKNOWN_PROFILE",
            $"Unsupported processing profile '{code}'.");
    }

    public LienIntakeV1Configuration ValidateAndDeserialize(
        string code,
        string? configurationJson)
    {
        var descriptor = GetRequired(code);
        if (!string.Equals(descriptor.Code, ProcessingProfileCodes.LienIntakeV1, StringComparison.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                "UNSUPPORTED_PROFILE_CONFIGURATION",
                $"No configuration contract is registered for '{descriptor.Code}'.");

        LienIntakeV1Configuration configuration;
        try
        {
            configuration = string.IsNullOrWhiteSpace(configurationJson)
                ? new LienIntakeV1Configuration()
                : JsonSerializer.Deserialize<LienIntakeV1Configuration>(
                    configurationJson,
                    SerializerOptions)
                  ?? throw new JsonException("Configuration must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_PROFILE_CONFIGURATION",
                $"Configuration for '{descriptor.Code}' is invalid: {ex.Message}");
        }

        ValidateLienIntakeV1(configuration);
        return configuration;
    }

    public string Serialize(string code, LienIntakeV1Configuration configuration)
    {
        var descriptor = GetRequired(code);
        ValidateLienIntakeV1(configuration);
        return JsonSerializer.Serialize(configuration, SerializerOptions);
    }

    private static string NormalizeCode(string code) =>
        code?.Trim().ToUpperInvariant() ?? string.Empty;

    private static void ValidateLienIntakeV1(LienIntakeV1Configuration configuration)
    {
        if (configuration.AutoApproveThreshold is < 0 or > 1 ||
            configuration.ReviewThreshold is < 0 or > 1 ||
            configuration.RejectThreshold is < 0 or > 1)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_THRESHOLDS",
                "Confidence thresholds must be between 0.00 and 1.00 inclusive.");
        }

        if (configuration.AutoApproveThreshold <= configuration.ReviewThreshold ||
            configuration.ReviewThreshold < configuration.RejectThreshold)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_THRESHOLD_ORDER",
                "Thresholds must satisfy AutoApproveThreshold > ReviewThreshold >= RejectThreshold.");
        }

        if (configuration.DestinationAdapterCode is { Length: > 0 } adapterCode &&
            (adapterCode.Length > 64 ||
             !adapterCode.All(character => char.IsLetterOrDigit(character) || character == '_') ||
             !char.IsLetter(adapterCode[0])))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_DESTINATION_ADAPTER_CODE",
                "DestinationAdapterCode must start with a letter and contain only letters, digits, or underscores.");
        }
    }
}