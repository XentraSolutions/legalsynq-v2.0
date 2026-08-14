using System.Text.Json;
using Intake.Contracts.Configuration;
using Intake.Domain.Configuration;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Configuration;

public sealed class IntakeConfigurationService(
    IIntakeConfigurationRepository repository,
    IProcessingProfileRegistry registry,
    IIntakeConfigurationAuditSink auditSink,
    ILogger<IntakeConfigurationService> logger) : IIntakeConfigurationService
{
    public async Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var configuration = await repository.FindTenantConfigurationAsync(tenantId, cancellationToken);
        return configuration is null ? null : Map(configuration);
    }

    public async Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(
        Guid tenantId,
        UpsertTenantIntakeConfigurationRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var configuration = await repository.FindTenantConfigurationAsync(tenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (configuration is null)
        {
            if (request.ConfigurationVersion.HasValue)
                throw IntakeConfigurationException.Conflict(
                    "CONFIGURATION_NOT_FOUND",
                    "A configuration version cannot be supplied before the tenant configuration exists.");

            if (request.DefaultProcessingProfileCode is not null)
            {
                await EnsureValidDefaultAsync(
                    tenantId,
                    request.DefaultProcessingProfileCode,
                    cancellationToken);
                await SetProfileAsDefaultAsync(
                    tenantId,
                    NormalizeRequiredCode(request.DefaultProcessingProfileCode),
                    actorId,
                    now,
                    cancellationToken);
            }

            configuration = new TenantIntakeConfiguration
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                IsEnabled = request.IsEnabled ?? true,
                DefaultProcessingProfileCode = NormalizeOptionalCode(request.DefaultProcessingProfileCode),
                RequireHumanReviewByDefault = request.RequireHumanReviewByDefault ?? true,
                AutoProcessingEnabled = request.AutoProcessingEnabled ?? false,
                ConfigurationVersion = 1,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            };

            repository.AddTenantConfiguration(configuration);
            await SaveAsync(cancellationToken);
            RecordAudit(
                tenantId,
                "TenantIntakeConfiguration",
                tenantId.ToString(),
                "CREATE",
                null,
                configuration.ConfigurationVersion,
                actorId,
                correlationId,
                new { configuration.IsEnabled, configuration.DefaultProcessingProfileCode });

            return Map(configuration);
        }

        RequireExpectedVersion(request.ConfigurationVersion, configuration.ConfigurationVersion);

        var previousEnabled = configuration.IsEnabled;
        var previousDefault = configuration.DefaultProcessingProfileCode;
        var hasDefaultInput = request.DefaultProcessingProfileCode is not null;
        var requestedDefault = !hasDefaultInput
            ? previousDefault
            : NormalizeOptionalCode(request.DefaultProcessingProfileCode);

        if (hasDefaultInput &&
            !string.Equals(previousDefault, requestedDefault, StringComparison.Ordinal))
        {
            if (requestedDefault is null)
            {
                if (previousDefault is not null)
                {
                    await ClearProfileDefaultAsync(
                        tenantId,
                        previousDefault,
                        actorId,
                        now,
                        cancellationToken);
                }
            }
            else
            {
                await EnsureValidDefaultAsync(tenantId, requestedDefault, cancellationToken);
                await SetProfileAsDefaultAsync(
                    tenantId,
                    requestedDefault,
                    actorId,
                    now,
                    cancellationToken);
            }
        }

        if (request.IsEnabled.HasValue)
            configuration.IsEnabled = request.IsEnabled.Value;
        if (hasDefaultInput)
            configuration.DefaultProcessingProfileCode = requestedDefault;
        if (request.RequireHumanReviewByDefault.HasValue)
            configuration.RequireHumanReviewByDefault = request.RequireHumanReviewByDefault.Value;
        if (request.AutoProcessingEnabled.HasValue)
            configuration.AutoProcessingEnabled = request.AutoProcessingEnabled.Value;

        var changed = previousEnabled != configuration.IsEnabled ||
                      !string.Equals(previousDefault, configuration.DefaultProcessingProfileCode, StringComparison.Ordinal) ||
                      request.RequireHumanReviewByDefault.HasValue ||
                      request.AutoProcessingEnabled.HasValue;

        if (!changed)
            return Map(configuration);

        var previousVersion = configuration.ConfigurationVersion;
        Touch(configuration, actorId, now);
        await SaveAsync(cancellationToken);

        var operation = previousEnabled != configuration.IsEnabled
            ? configuration.IsEnabled ? "ENABLE" : "DISABLE"
            : previousDefault is null && configuration.DefaultProcessingProfileCode is not null
                ? "SET_DEFAULT"
                : previousDefault is not null && configuration.DefaultProcessingProfileCode is null
                    ? "REMOVE_DEFAULT"
                    : "UPDATE";

        RecordAudit(
            tenantId,
            "TenantIntakeConfiguration",
            tenantId.ToString(),
            operation,
            previousVersion,
            configuration.ConfigurationVersion,
            actorId,
            correlationId,
            new { configuration.IsEnabled, configuration.DefaultProcessingProfileCode });
        LogMutation(tenantId, null, operation, configuration.ConfigurationVersion, correlationId, "succeeded");

        return Map(configuration);
    }

    public async Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(
        CancellationToken cancellationToken)
    {
        var definitions = await repository.ListActiveDefinitionsAsync(cancellationToken);
        return definitions
            .OrderBy(definition => definition.Code, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListTenantProfilesAsync(tenantId, cancellationToken);
        return profiles
            .OrderBy(profile => profile.ProcessingProfileDefinition?.Code, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task<TenantProcessingProfileResponse> AssignProfileAsync(
        Guid tenantId,
        AssignTenantProcessingProfileRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequiredCode(request.ProcessingProfileCode);
        registry.GetRequired(code);

        var definition = await repository.FindDefinitionByCodeAsync(code, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "PROFILE_NOT_REGISTERED",
                $"Processing profile '{code}' is not registered.");

        EnsureDefinitionCanBeAssigned(definition);

        var existing = await repository.FindTenantProfileAsync(tenantId, code, cancellationToken);
        if (existing is not null)
            throw IntakeConfigurationException.Conflict(
                "DUPLICATE_PROFILE_ASSIGNMENT",
                $"Tenant already has an assignment for '{code}'.");

        var typedConfiguration = registry.ValidateAndDeserialize(
            code,
            request.Configuration?.GetRawText());
        var now = DateTimeOffset.UtcNow;

        var profile = new TenantProcessingProfile
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProcessingProfileDefinitionId = definition.Id,
            ProcessingProfileDefinition = definition,
            IsEnabled = true,
            IsDefault = request.IsDefault,
            DefaultTenantKey = request.IsDefault ? TenantKey(tenantId) : null,
            ConfigurationJson = registry.Serialize(code, typedConfiguration),
            ConfigurationVersion = 1,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };

        TenantIntakeConfiguration? configurationBeforeDefault = null;
        if (profile.IsDefault)
        {
            configurationBeforeDefault = await repository.FindTenantConfigurationAsync(
                tenantId,
                cancellationToken);
            await ClearOtherDefaultsAsync(tenantId, null, actorId, now, cancellationToken);
            await SetTenantConfigurationDefaultAsync(
                tenantId,
                code,
                actorId,
                now,
                cancellationToken);
        }

        repository.AddTenantProfile(profile);
        await SaveAsync(cancellationToken);
        if (profile.IsDefault)
        {
            var configurationAfterDefault = await repository.FindTenantConfigurationAsync(
                tenantId,
                cancellationToken);
            if (configurationAfterDefault is not null &&
                (configurationBeforeDefault is null ||
                 !string.Equals(
                     configurationBeforeDefault.DefaultProcessingProfileCode,
                     code,
                     StringComparison.Ordinal)))
            {
                RecordAudit(
                    tenantId,
                    "TenantIntakeConfiguration",
                    tenantId.ToString(),
                    configurationBeforeDefault is null ? "CREATE" : "SET_DEFAULT",
                    configurationBeforeDefault?.ConfigurationVersion,
                    configurationAfterDefault.ConfigurationVersion,
                    actorId,
                    correlationId,
                    new { configurationAfterDefault.DefaultProcessingProfileCode });
            }
        }
        RecordAudit(
            tenantId,
            "TenantProcessingProfile",
            code,
            profile.IsDefault ? "SET_DEFAULT" : "CREATE",
            null,
            profile.ConfigurationVersion,
            actorId,
            correlationId,
            new { profile.IsEnabled, profile.IsDefault });
        LogMutation(tenantId, code, profile.IsDefault ? "SET_DEFAULT" : "CREATE",
            profile.ConfigurationVersion, correlationId, "succeeded");

        return Map(profile);
    }

    public async Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(
        Guid tenantId,
        string profileCode,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequiredCode(profileCode);
        var profile = await repository.FindTenantProfileAsync(tenantId, code, cancellationToken);
        return profile is null ? null : Map(profile);
    }

    public async Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(
        Guid tenantId,
        string profileCode,
        UpdateTenantProcessingProfileRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequiredCode(profileCode);
        var profile = await repository.FindTenantProfileAsync(tenantId, code, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "PROFILE_ASSIGNMENT_NOT_FOUND",
                $"Tenant has no assignment for '{code}'.");

        RequireExpectedVersion(request.ConfigurationVersion, profile.ConfigurationVersion);
        var now = DateTimeOffset.UtcNow;
        var previousVersion = profile.ConfigurationVersion;
        var previousDefault = profile.IsDefault;

        if (request.Configuration.HasValue)
        {
            var typedConfiguration = registry.ValidateAndDeserialize(
                code,
                request.Configuration.Value.GetRawText());
            profile.ConfigurationJson = registry.Serialize(code, typedConfiguration);
        }

        if (request.IsDefault.HasValue && request.IsDefault.Value != profile.IsDefault)
        {
            if (request.IsDefault.Value)
            {
                EnsureDefinitionCanBeAssigned(profile.ProcessingProfileDefinition);
                if (!profile.IsEnabled)
                    throw IntakeConfigurationException.BadRequest(
                        "DISABLED_PROFILE_CANNOT_BE_DEFAULT",
                        "A disabled tenant profile cannot become the default.");

                await ClearOtherDefaultsAsync(tenantId, profile.Id, actorId, now, cancellationToken);
                profile.IsDefault = true;
                profile.DefaultTenantKey = TenantKey(tenantId);
                await SetTenantConfigurationDefaultAsync(
                    tenantId,
                    code,
                    actorId,
                    now,
                    cancellationToken);
            }
            else
            {
                profile.IsDefault = false;
                profile.DefaultTenantKey = null;
                await ClearTenantConfigurationDefaultAsync(
                    tenantId,
                    code,
                    actorId,
                    now,
                    cancellationToken);
            }
        }

        if (request.Configuration.HasValue || request.IsDefault.HasValue)
        {
            Touch(profile, actorId, now);
            await SaveAsync(cancellationToken);

            var operation = previousDefault != profile.IsDefault
                ? profile.IsDefault ? "SET_DEFAULT" : "REMOVE_DEFAULT"
                : "UPDATE";
            RecordAudit(
                tenantId,
                "TenantProcessingProfile",
                code,
                operation,
                previousVersion,
                profile.ConfigurationVersion,
                actorId,
                correlationId,
                new { profile.IsEnabled, profile.IsDefault });
            LogMutation(tenantId, code, operation, profile.ConfigurationVersion, correlationId, "succeeded");
        }

        return Map(profile);
    }

    public async Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(
        Guid tenantId,
        string profileCode,
        UpdateTenantProcessingProfileStatusRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequiredCode(profileCode);
        var profile = await repository.FindTenantProfileAsync(tenantId, code, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "PROFILE_ASSIGNMENT_NOT_FOUND",
                $"Tenant has no assignment for '{code}'.");

        RequireExpectedVersion(request.ConfigurationVersion, profile.ConfigurationVersion);
        if (profile.IsEnabled == request.IsEnabled)
            return Map(profile);

        if (!request.IsEnabled && profile.IsDefault)
            throw IntakeConfigurationException.BadRequest(
                "DEFAULT_PROFILE_MUST_BE_CHANGED_FIRST",
                "The current default profile cannot be disabled until another default is selected or the default is removed.");

        var previousVersion = profile.ConfigurationVersion;
        var now = DateTimeOffset.UtcNow;
        profile.IsEnabled = request.IsEnabled;
        Touch(profile, actorId, now);
        await SaveAsync(cancellationToken);

        var operation = request.IsEnabled ? "ENABLE" : "DISABLE";
        RecordAudit(
            tenantId,
            "TenantProcessingProfile",
            code,
            operation,
            previousVersion,
            profile.ConfigurationVersion,
            actorId,
            correlationId,
            new { profile.IsEnabled, profile.IsDefault });
        LogMutation(tenantId, code, operation, profile.ConfigurationVersion, correlationId, "succeeded");

        return Map(profile);
    }

    public async Task<ResolvedProcessingConfiguration> ResolveAsync(
        Guid tenantId,
        string? profileCode,
        CancellationToken cancellationToken)
    {
        var tenantConfiguration = await repository.FindTenantConfigurationAsync(tenantId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "TENANT_CONFIGURATION_NOT_FOUND",
                "No Intake configuration exists for the tenant.");

        if (!tenantConfiguration.IsEnabled)
            throw IntakeConfigurationException.BadRequest(
                "TENANT_INTAKE_DISABLED",
                "Intake is disabled for the tenant.");

        var code = string.IsNullOrWhiteSpace(profileCode)
            ? tenantConfiguration.DefaultProcessingProfileCode
            : NormalizeRequiredCode(profileCode);

        if (string.IsNullOrWhiteSpace(code))
            throw IntakeConfigurationException.BadRequest(
                "DEFAULT_PROFILE_NOT_CONFIGURED",
                "No default processing profile is configured; callers must provide a profile code.");

        registry.GetRequired(code);
        var profile = await repository.FindTenantProfileAsync(tenantId, code, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "PROFILE_ASSIGNMENT_NOT_FOUND",
                $"Tenant has no assignment for '{code}'.");

        EnsureDefinitionCanBeResolved(profile.ProcessingProfileDefinition);
        if (!profile.IsEnabled)
            throw IntakeConfigurationException.BadRequest(
                "PROFILE_DISABLED",
                $"Processing profile '{code}' is disabled for the tenant.");

        var typedConfiguration = registry.ValidateAndDeserialize(code, profile.ConfigurationJson);
        return new ResolvedProcessingConfiguration(
            tenantId,
            code,
            profile.ProcessingProfileDefinition!.Version,
            tenantConfiguration.ConfigurationVersion,
            profile.ConfigurationVersion,
            typedConfiguration,
            DateTimeOffset.UtcNow);
    }

    private async Task EnsureValidDefaultAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequiredCode(code);
        registry.GetRequired(normalized);
        var profile = await repository.FindTenantProfileAsync(tenantId, normalized, cancellationToken)
            ?? throw IntakeConfigurationException.BadRequest(
                "DEFAULT_PROFILE_NOT_ASSIGNED",
                $"Default profile '{normalized}' must be assigned to the tenant first.");

        if (!profile.IsEnabled)
            throw IntakeConfigurationException.BadRequest(
                "DEFAULT_PROFILE_DISABLED",
                $"Default profile '{normalized}' must be enabled.");
        EnsureDefinitionCanBeResolved(profile.ProcessingProfileDefinition);
    }

    private async Task SetTenantConfigurationDefaultAsync(
        Guid tenantId,
        string code,
        Guid? actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var configuration = await repository.FindTenantConfigurationAsync(tenantId, cancellationToken);
        if (configuration is null)
        {
            configuration = new TenantIntakeConfiguration
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                IsEnabled = true,
                DefaultProcessingProfileCode = code,
                RequireHumanReviewByDefault = true,
                AutoProcessingEnabled = false,
                ConfigurationVersion = 1,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            };
            repository.AddTenantConfiguration(configuration);
            return;
        }

        if (string.Equals(configuration.DefaultProcessingProfileCode, code, StringComparison.Ordinal))
            return;

        configuration.DefaultProcessingProfileCode = code;
        Touch(configuration, actorId, now);
    }

    private async Task SetProfileAsDefaultAsync(
        Guid tenantId,
        string code,
        Guid? actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await repository.FindTenantProfileAsync(
                tenantId,
                code,
                cancellationToken)
            ?? throw IntakeConfigurationException.BadRequest(
                "DEFAULT_PROFILE_NOT_ASSIGNED",
                $"Default profile '{code}' must be assigned to the tenant first.");

        if (!profile.IsEnabled)
            throw IntakeConfigurationException.BadRequest(
                "DEFAULT_PROFILE_DISABLED",
                $"Default profile '{code}' must be enabled.");
        EnsureDefinitionCanBeResolved(profile.ProcessingProfileDefinition);

        await ClearOtherDefaultsAsync(
            tenantId,
            profile.Id,
            actorId,
            now,
            cancellationToken);

        if (!profile.IsDefault)
        {
            profile.IsDefault = true;
            profile.DefaultTenantKey = TenantKey(tenantId);
            Touch(profile, actorId, now);
        }
    }

    private async Task ClearProfileDefaultAsync(
        Guid tenantId,
        string code,
        Guid? actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await repository.FindTenantProfileAsync(
            tenantId,
            code,
            cancellationToken);
        if (profile is null || !profile.IsDefault)
            return;

        profile.IsDefault = false;
        profile.DefaultTenantKey = null;
        Touch(profile, actorId, now);
    }

    private async Task ClearTenantConfigurationDefaultAsync(
        Guid tenantId,
        string code,
        Guid? actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var configuration = await repository.FindTenantConfigurationAsync(tenantId, cancellationToken);
        if (configuration is null ||
            !string.Equals(configuration.DefaultProcessingProfileCode, code, StringComparison.Ordinal))
            return;

        configuration.DefaultProcessingProfileCode = null;
        Touch(configuration, actorId, now);
    }

    private async Task ClearOtherDefaultsAsync(
        Guid tenantId,
        Guid? exceptProfileId,
        Guid? actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListTenantProfilesAsync(tenantId, cancellationToken);
        foreach (var profile in profiles.Where(
                     profile => profile.IsDefault && profile.Id != exceptProfileId))
        {
            profile.IsDefault = false;
            profile.DefaultTenantKey = null;
            Touch(profile, actorId, now);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await repository.SaveChangesAsync(cancellationToken);
    }

    private void RecordAudit(
        Guid tenantId,
        string resourceType,
        string resourceIdentifier,
        string operation,
        int? previousVersion,
        int newVersion,
        Guid? actorId,
        string? correlationId,
        object? metadata) =>
        _ = auditSink.RecordAsync(
            new ConfigurationAuditEntry(
                tenantId,
                resourceType,
                resourceIdentifier,
                operation,
                previousVersion,
                newVersion,
                actorId,
                correlationId,
                metadata),
            CancellationToken.None);

    private void LogMutation(
        Guid tenantId,
        string? profileCode,
        string operation,
        int version,
        string? correlationId,
        string result) =>
        logger.LogInformation(
            "Intake configuration mutation {Result} CorrelationId={CorrelationId} TenantId={TenantId} ProfileCode={ProfileCode} ConfigurationVersion={ConfigurationVersion} Operation={Operation}",
            result,
            correlationId,
            tenantId,
            profileCode,
            version,
            operation);

    private static void RequireExpectedVersion(int? expected, int actual)
    {
        if (!expected.HasValue)
            throw IntakeConfigurationException.BadRequest(
                "CONFIGURATION_VERSION_REQUIRED",
                "configurationVersion is required for mutation of an existing resource.");
        if (expected.Value != actual)
            throw IntakeConfigurationException.Conflict(
                "STALE_CONFIGURATION_VERSION",
                $"The supplied configurationVersion {expected.Value} is stale; current version is {actual}.");
    }

    private static void EnsureDefinitionCanBeAssigned(ProcessingProfileDefinition? definition)
    {
        if (definition is null)
            throw IntakeConfigurationException.NotFound(
                "PROFILE_DEFINITION_NOT_FOUND",
                "The processing profile definition was not found.");
        if (!definition.IsActive)
            throw IntakeConfigurationException.BadRequest(
                "INACTIVE_PROFILE",
                $"Inactive processing profile '{definition.Code}' cannot be newly assigned or selected.");
    }

    private static void EnsureDefinitionCanBeResolved(ProcessingProfileDefinition? definition)
    {
        if (definition is null)
            throw IntakeConfigurationException.NotFound(
                "PROFILE_DEFINITION_NOT_FOUND",
                "The processing profile definition was not found.");
        if (!definition.IsActive)
            throw IntakeConfigurationException.BadRequest(
                "INACTIVE_PROFILE",
                $"Inactive processing profile '{definition.Code}' cannot be selected for new processing.");
    }

    private static void Touch(TenantIntakeConfiguration configuration, Guid? actorId, DateTimeOffset now)
    {
        configuration.ConfigurationVersion++;
        configuration.UpdatedAt = now;
        configuration.UpdatedBy = actorId;
    }

    private static void Touch(TenantProcessingProfile profile, Guid? actorId, DateTimeOffset now)
    {
        profile.ConfigurationVersion++;
        profile.UpdatedAt = now;
        profile.UpdatedBy = actorId;
    }

    private static string NormalizeRequiredCode(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0)
            throw IntakeConfigurationException.BadRequest(
                "PROFILE_CODE_REQUIRED",
                "processingProfileCode is required.");
        return normalized;
    }

    private static string? NormalizeOptionalCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static string TenantKey(Guid tenantId) => tenantId.ToString("N");

    private static TenantIntakeConfigurationResponse Map(TenantIntakeConfiguration configuration) =>
        new(
            configuration.TenantId,
            configuration.OrgId,
            configuration.IsEnabled,
            configuration.DefaultProcessingProfileCode,
            configuration.RequireHumanReviewByDefault,
            configuration.AutoProcessingEnabled,
            configuration.ConfigurationVersion,
            configuration.CreatedAt,
            configuration.CreatedBy,
            configuration.UpdatedAt,
            configuration.UpdatedBy);

    private static ProcessingProfileDefinitionResponse Map(ProcessingProfileDefinition definition) =>
        new(
            definition.Code,
            definition.DisplayName,
            definition.Description,
            definition.Version,
            definition.IsActive,
            definition.IsSystemDefined);

    private static TenantProcessingProfileResponse Map(TenantProcessingProfile profile)
    {
        using var document = JsonDocument.Parse(profile.ConfigurationJson);
        return new(
            profile.TenantId,
            profile.ProcessingProfileDefinition?.Code ?? string.Empty,
            profile.ProcessingProfileDefinition?.DisplayName ?? string.Empty,
            profile.ProcessingProfileDefinition?.Version ?? 0,
            profile.IsEnabled,
            profile.IsDefault,
            document.RootElement.Clone(),
            profile.ConfigurationVersion,
            profile.CreatedAt,
            profile.CreatedBy,
            profile.UpdatedAt,
            profile.UpdatedBy);
    }
}