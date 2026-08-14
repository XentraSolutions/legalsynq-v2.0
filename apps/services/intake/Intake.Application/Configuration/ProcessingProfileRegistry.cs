using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
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

        if (configuration.ClassificationProfileCode is null ||
            configuration.ClassificationProfileCode.Length is < 3 or > 64 ||
            !char.IsLetter(configuration.ClassificationProfileCode[0]) ||
            !configuration.ClassificationProfileCode.All(
                character => char.IsLetterOrDigit(character) || character == '_'))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CLASSIFICATION_PROFILE_CODE",
                "ClassificationProfileCode must start with a letter and contain only letters, digits, or underscores.");
        }

        if (configuration.MaxClassificationInputCharacters is < 256 or > 1_000_000)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CLASSIFICATION_INPUT_LIMIT",
                "MaxClassificationInputCharacters must be between 256 and 1000000.");
        }

        if (configuration.MinimumClassificationConfidence is < 0 or > 1 ||
            configuration.MaxClassificationInputTokens is < 1 or > 128_000 ||
            configuration.MaxClassificationOutputTokens is < 1 or > 8_000 ||
            configuration.ClassificationTimeoutSeconds is < 1 or > 300 ||
            configuration.ClassificationMaxAttempts is < 1 or > 10)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CLASSIFICATION_GUARDRAILS",
                "Classification confidence, token, timeout, and retry settings are outside the supported bounds.");
        }

        if (configuration.ExtractionProfileCode is null ||
            configuration.ExtractionProfileCode.Length is < 3 or > 64 ||
            !char.IsLetter(configuration.ExtractionProfileCode[0]) ||
            !configuration.ExtractionProfileCode.All(
                character => char.IsLetterOrDigit(character) || character == '_'))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EXTRACTION_PROFILE_CODE",
                "ExtractionProfileCode must start with a letter and contain only letters, digits, or underscores.");
        }

        if (configuration.MinimumFactConfidence is < 0 or > 1 ||
            configuration.MaxExtractionInputCharacters is < 256 or > 1_000_000 ||
            configuration.MaxExtractionOutputTokens is < 1 or > 16_000 ||
            configuration.ExtractionTimeoutSeconds is < 1 or > 300 ||
            configuration.ExtractionMaxAttempts is < 1 or > 10 ||
            configuration.MaxExtractedFacts is < 1 or > 500 ||
            configuration.MaxFactValueCharacters is < 16 or > 4_000 ||
            configuration.MaxFactEvidenceCharacters is < 16 or > 2_000)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EXTRACTION_GUARDRAILS",
                "Extraction confidence, input, output, timeout, retry, fact, or evidence settings are outside the supported bounds.");
        }

        if (configuration.NormalizationProfileCode is null ||
            configuration.NormalizationProfileCode.Length is < 3 or > 64 ||
            !char.IsLetter(configuration.NormalizationProfileCode[0]) ||
            !configuration.NormalizationProfileCode.All(
                character => char.IsLetterOrDigit(character) || character == '_'))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_NORMALIZATION_PROFILE_CODE",
                "NormalizationProfileCode must start with a letter and contain only letters, digits, or underscores.");
        }

        if (configuration.DefaultCountryCode is null ||
            configuration.DefaultCountryCode.Length != 2 ||
            !configuration.DefaultCountryCode.All(char.IsLetter))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_NORMALIZATION_COUNTRY_CODE",
                "DefaultCountryCode must be a two-letter country code.");
        }

        if (configuration.DefaultCurrencyCode is null ||
            configuration.DefaultCurrencyCode.Length != 3 ||
            !configuration.DefaultCurrencyCode.All(char.IsLetter))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_NORMALIZATION_CURRENCY_CODE",
                "DefaultCurrencyCode must be a three-letter currency code.");
        }

        if (string.IsNullOrWhiteSpace(configuration.DateCulture))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_NORMALIZATION_DATE_CULTURE",
                "DateCulture must identify a supported .NET culture.");
        }
        try
        {
            _ = CultureInfo.GetCultureInfo(configuration.DateCulture);
        }
        catch (CultureNotFoundException)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_NORMALIZATION_DATE_CULTURE",
                "DateCulture must identify a supported .NET culture.");
        }

        if (configuration.MatchingProfileCode is null ||
            configuration.MatchingProfileCode.Length is < 3 or > 64 ||
            !char.IsLetter(configuration.MatchingProfileCode[0]) ||
            !configuration.MatchingProfileCode.All(
                character => char.IsLetterOrDigit(character) || character == '_'))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_MATCHING_PROFILE_CODE",
                "MatchingProfileCode must start with a letter and contain only letters, digits, or underscores.");
        }

        if (configuration.MaxCandidatesPerEntityType is < 1 or > 100 ||
            configuration.MaxCandidateSearchPool is < 1 or > 1_000 ||
            configuration.MaxCandidateSearchPool < configuration.MaxCandidatesPerEntityType ||
            configuration.MinimumCandidateScore is < 0 or > 1)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_MATCHING_GUARDRAILS",
                "Matching candidate limits and score threshold are outside the supported bounds.");
        }

        if (configuration.PolicyProfileCode is null ||
            configuration.PolicyProfileCode.Length is < 3 or > 64 ||
            !char.IsLetter(configuration.PolicyProfileCode[0]) ||
            !configuration.PolicyProfileCode.All(
                character => char.IsLetterOrDigit(character) || character == '_'))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_POLICY_PROFILE_CODE",
                "PolicyProfileCode must start with a letter and contain only letters, digits, or underscores.");
        }

        if (configuration.MinimumRequiredFactConfidence is < 0 or > 1 ||
            configuration.MinimumPatientMatchScore is < 0 or > 1 ||
            configuration.MinimumProviderFacilityMatchScore is < 0 or > 1 ||
            configuration.MinimumCaseMatchScore is < 0 or > 1 ||
            configuration.MinimumPatientMatchMargin is < 0 or > 1 ||
            configuration.MinimumProviderFacilityMatchMargin is < 0 or > 1 ||
            configuration.MinimumCaseMatchMargin is < 0 or > 1)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_POLICY_GUARDRAILS",
                "Policy confidence thresholds and candidate margins must be between 0.00 and 1.00.");
        }
    }
}