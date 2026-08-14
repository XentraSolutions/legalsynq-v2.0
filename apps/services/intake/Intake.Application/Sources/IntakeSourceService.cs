using System.Text.Json;
using System.Text.RegularExpressions;
using Intake.Application.Configuration;
using Intake.Contracts.Configuration;
using Intake.Contracts.Sources;
using Intake.Domain.Sources;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Sources;

public sealed class IntakeSourceService(
    IIntakeSourceRepository repository,
    IIntakeConfigurationService configurationService,
    IProcessingProfileRegistry profileRegistry,
    IIntakeSourceTypeRegistry sourceTypeRegistry,
    IIntakeSourcePurposeRegistry purposeRegistry,
    IIntakeSourceProfileCompatibilityRegistry compatibilityRegistry,
    IEmailConnectorRegistry connectorRegistry,
    IIntakeConfigurationAuditSink auditSink,
    ILogger<IntakeSourceService> logger) : IIntakeSourceService
{
    private static readonly Regex CredentialReferencePattern = new(
        "^(secret|credential|connection)://[A-Za-z0-9][A-Za-z0-9._:/-]{0,255}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<IntakeSourceResponse>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var sources = await repository.ListTenantSourcesAsync(tenantId, cancellationToken);
        return sources
            .OrderByDescending(source => source.IsDefault)
            .ThenBy(source => source.NormalizedEmailAddress, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task<IntakeSourceResponse?> GetAsync(
        Guid tenantId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindTenantSourceAsync(tenantId, sourceId, cancellationToken);
        return source is null ? null : Map(source);
    }

    public Task<IntakeSourceResponse> CreateAsync(
        Guid tenantId,
        CreateIntakeSourceRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken) =>
        ExecuteTransactionalMutationAsync(
            pendingPublications => CreateCoreAsync(
                tenantId,
                request,
                actorId,
                correlationId,
                pendingPublications,
                cancellationToken),
            cancellationToken);

    private async Task<IntakeSourceResponse> CreateCoreAsync(
        Guid tenantId,
        CreateIntakeSourceRequest request,
        Guid? actorId,
        string? correlationId,
        List<Action> pendingPublications,
        CancellationToken cancellationToken)
    {
        await EnsureTenantIntakeEnabledAsync(tenantId, cancellationToken);
        var input = await ValidateInputAsync(
            tenantId,
            request.SourceType,
            request.EmailAddress,
            request.Provider,
            request.Purpose,
            request.ProcessingProfileCode,
            request.ConnectorConfiguration,
            request.CredentialReference,
            null,
            cancellationToken);

        if (await repository.FindByNormalizedEmailAddressAsync(
                input.NormalizedEmailAddress,
                cancellationToken) is not null)
        {
            throw IntakeConfigurationException.Conflict(
                "DUPLICATE_SOURCE_EMAIL",
                $"Email address '{input.NormalizedEmailAddress}' is already owned by an Intake source.");
        }

        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<TenantIntakeSource> clearedDefaults = [];
        if (request.IsDefault)
        {
            await EnsureDefaultCanBeSetAsync(tenantId, input.Purpose, cancellationToken);
            clearedDefaults = await ClearOtherDefaultsAsync(
                tenantId,
                input.Purpose,
                null,
                actorId,
                now,
                cancellationToken);
        }

        if (clearedDefaults.Count > 0)
            await SaveAsync(cancellationToken);

        var source = new TenantIntakeSource
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SourceType = input.SourceType,
            EmailAddress = input.EmailAddress,
            NormalizedEmailAddress = input.NormalizedEmailAddress,
            Provider = input.Provider,
            Purpose = input.Purpose,
            ProcessingProfileCode = input.ProcessingProfileCode,
            IsActive = true,
            IsDefault = request.IsDefault,
            DefaultTenantPurposeKey = request.IsDefault
                ? DefaultKey(tenantId, input.Purpose)
                : null,
            ConnectorConfigurationJson = input.ConnectorConfigurationJson,
            CredentialReference = input.CredentialReference,
            ValidationStatus = IntakeSourceValidationStatuses.NotValidated,
            ConfigurationVersion = 1,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };

        repository.Add(source);
        await SaveAsync(cancellationToken);

        QueueClearedDefaultAudits(
            pendingPublications,
            tenantId,
            clearedDefaults,
            actorId,
            correlationId);
        QueueAudit(
            pendingPublications,
            tenantId,
            source,
            request.IsDefault ? "SET_DEFAULT_SOURCE" : "CREATE_SOURCE",
            null,
            source.ConfigurationVersion,
            actorId,
            correlationId,
            new { source.Provider, source.Purpose, source.ProcessingProfileCode, Result = "succeeded" });
        QueueLog(
            pendingPublications,
            tenantId,
            source,
            request.IsDefault ? "SET_DEFAULT_SOURCE" : "CREATE_SOURCE",
            correlationId);

        return Map(source);
    }

    public Task<IntakeSourceResponse> UpdateAsync(
        Guid tenantId,
        Guid sourceId,
        UpdateIntakeSourceRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken) =>
        ExecuteTransactionalMutationAsync(
            pendingPublications => UpdateCoreAsync(
                tenantId,
                sourceId,
                request,
                actorId,
                correlationId,
                pendingPublications,
                cancellationToken),
            cancellationToken);

    private async Task<IntakeSourceResponse> UpdateCoreAsync(
        Guid tenantId,
        Guid sourceId,
        UpdateIntakeSourceRequest request,
        Guid? actorId,
        string? correlationId,
        List<Action> pendingPublications,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindTenantSourceAsync(tenantId, sourceId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "INTAKE_SOURCE_NOT_FOUND",
                $"Intake source '{sourceId}' was not found for the current tenant.");

        RequireExpectedVersion(request.ConfigurationVersion, source.ConfigurationVersion);
        await EnsureTenantIntakeEnabledAsync(tenantId, cancellationToken);
        var input = await ValidateInputAsync(
            tenantId,
            request.SourceType,
            request.EmailAddress,
            request.Provider,
            request.Purpose,
            request.ProcessingProfileCode,
            request.ConnectorConfiguration,
            request.CredentialReference,
            source.Id,
            cancellationToken);

        if (!string.Equals(source.NormalizedEmailAddress, input.NormalizedEmailAddress, StringComparison.Ordinal) &&
            await repository.FindByNormalizedEmailAddressAsync(
                input.NormalizedEmailAddress,
                cancellationToken) is { } duplicate &&
            duplicate.Id != source.Id)
        {
            throw IntakeConfigurationException.Conflict(
                "DUPLICATE_SOURCE_EMAIL",
                $"Email address '{input.NormalizedEmailAddress}' is already owned by an Intake source.");
        }

        if (request.IsDefault && !source.IsActive)
        {
            throw IntakeConfigurationException.BadRequest(
                "INACTIVE_SOURCE_CANNOT_BE_DEFAULT",
                "An inactive Intake source cannot become the default.");
        }

        var now = DateTimeOffset.UtcNow;
        var previousDefault = source.IsDefault;
        var previousVersion = source.ConfigurationVersion;
        IReadOnlyList<TenantIntakeSource> clearedDefaults = [];
        if (request.IsDefault)
        {
            await EnsureDefaultCanBeSetAsync(tenantId, input.Purpose, cancellationToken);
            clearedDefaults = await ClearOtherDefaultsAsync(
                tenantId,
                input.Purpose,
                source.Id,
                actorId,
                now,
                cancellationToken);
        }

        if (clearedDefaults.Count > 0)
            await SaveAsync(cancellationToken);

        source.SourceType = input.SourceType;
        source.EmailAddress = input.EmailAddress;
        source.NormalizedEmailAddress = input.NormalizedEmailAddress;
        source.Provider = input.Provider;
        source.Purpose = input.Purpose;
        source.ProcessingProfileCode = input.ProcessingProfileCode;
        source.IsDefault = request.IsDefault;
        source.DefaultTenantPurposeKey = request.IsDefault
            ? DefaultKey(tenantId, input.Purpose)
            : null;
        source.ConnectorConfigurationJson = input.ConnectorConfigurationJson;
        source.CredentialReference = input.CredentialReference;
        source.ValidationStatus = IntakeSourceValidationStatuses.NotValidated;
        source.LastValidatedAt = null;
        source.LastValidationMessage = null;
        Touch(source, actorId, now);

        await SaveAsync(cancellationToken);

        QueueClearedDefaultAudits(
            pendingPublications,
            tenantId,
            clearedDefaults,
            actorId,
            correlationId);
        var operation = previousDefault == request.IsDefault
            ? "UPDATE_SOURCE"
            : request.IsDefault ? "SET_DEFAULT_SOURCE" : "REMOVE_DEFAULT_SOURCE";
        QueueAudit(
            pendingPublications,
            tenantId,
            source,
            operation,
            previousVersion,
            source.ConfigurationVersion,
            actorId,
            correlationId,
            new { source.Provider, source.Purpose, source.ProcessingProfileCode, Result = "succeeded" });
        QueueLog(
            pendingPublications,
            tenantId,
            source,
            operation,
            correlationId);

        return Map(source);
    }

    public async Task<IntakeSourceResponse> UpdateStatusAsync(
        Guid tenantId,
        Guid sourceId,
        UpdateIntakeSourceStatusRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindTenantSourceAsync(tenantId, sourceId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "INTAKE_SOURCE_NOT_FOUND",
                $"Intake source '{sourceId}' was not found for the current tenant.");

        RequireExpectedVersion(request.ConfigurationVersion, source.ConfigurationVersion);
        if (source.IsActive == request.IsActive)
            return Map(source);

        if (!request.IsActive && source.IsDefault)
        {
            throw IntakeConfigurationException.BadRequest(
                "DEFAULT_SOURCE_MUST_BE_CHANGED_FIRST",
                "The current default source cannot be disabled until another source is selected or the default is removed.");
        }

        var previousVersion = source.ConfigurationVersion;
        var now = DateTimeOffset.UtcNow;
        source.IsActive = request.IsActive;
        Touch(source, actorId, now);
        await SaveAsync(cancellationToken);

        var operation = request.IsActive ? "ENABLE_SOURCE" : "DISABLE_SOURCE";
        RecordAudit(
            tenantId,
            source,
            operation,
            previousVersion,
            source.ConfigurationVersion,
            actorId,
            correlationId,
            new { source.IsActive, Result = "succeeded" });
        LogMutation(tenantId, source, operation, correlationId);

        return Map(source);
    }

    public async Task<SourceValidationResponse> ValidateAsync(
        Guid tenantId,
        Guid sourceId,
        int? configurationVersion,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindTenantSourceAsync(tenantId, sourceId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "INTAKE_SOURCE_NOT_FOUND",
                $"Intake source '{sourceId}' was not found for the current tenant.");

        RequireExpectedVersion(configurationVersion, source.ConfigurationVersion);
        await EnsureTenantIntakeEnabledAsync(tenantId, cancellationToken);
        await ValidateInputAsync(
            tenantId,
            source.SourceType,
            source.EmailAddress,
            source.Provider,
            source.Purpose,
            source.ProcessingProfileCode,
            ParseConfiguration(source.ConnectorConfigurationJson),
            source.CredentialReference,
            source.Id,
            cancellationToken);

        var previousVersion = source.ConfigurationVersion;
        var now = DateTimeOffset.UtcNow;
        source.ValidationStatus = IntakeSourceValidationStatuses.Valid;
        source.LastValidatedAt = now;
        source.LastValidationMessage = "Source configuration is valid. Live mailbox connectivity is not asserted.";
        Touch(source, actorId, now);
        await SaveAsync(cancellationToken);

        RecordAudit(
            tenantId,
            source,
            "VALIDATE_SOURCE",
            previousVersion,
            source.ConfigurationVersion,
            actorId,
            correlationId,
            new { source.ValidationStatus, Result = "succeeded" });
        LogMutation(tenantId, source, "VALIDATE_SOURCE", correlationId);

        return new SourceValidationResponse(
            Map(source),
            source.ValidationStatus,
            source.LastValidationMessage);
    }

    public async Task<ConnectorTestResponse> TestConnectorAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindTenantSourceAsync(tenantId, sourceId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "INTAKE_SOURCE_NOT_FOUND",
                $"Intake source '{sourceId}' was not found for the current tenant.");
        if (source.SourceType == IntakeSourceTypes.Manual)
        {
            return new ConnectorTestResponse(
                source.Id,
                source.Provider,
                "NOT_APPLICABLE",
                "Manual sources do not require live connector connectivity.",
                DateTimeOffset.UtcNow);
        }
        var connector = connectorRegistry.GetRequired(source.Provider);
        var validation = connector.ValidateConfiguration(source.ConnectorConfigurationJson);
        if (!validation.IsValid)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CONNECTOR_CONFIGURATION",
                validation.Message);
        }

        var result = await connector.TestConnectionAsync(
            source.ConnectorConfigurationJson,
            source.CredentialReference,
            cancellationToken);
        RecordAudit(
            tenantId,
            source,
            "TEST_CONNECTOR",
            source.ConfigurationVersion,
            source.ConfigurationVersion,
            actorId,
            correlationId,
            new { result.Status, result.Message, Result = result.Status });
        LogMutation(tenantId, source, "TEST_CONNECTOR", correlationId, result.Status);

        return new ConnectorTestResponse(
            source.Id,
            source.Provider,
            result.Status,
            result.Message,
            DateTimeOffset.UtcNow);
    }

    private async Task<ValidatedInput> ValidateInputAsync(
        Guid tenantId,
        string sourceType,
        string emailAddress,
        string provider,
        string purpose,
        string processingProfileCode,
        JsonElement? connectorConfiguration,
        string? credentialReference,
        Guid? sourceId,
        CancellationToken cancellationToken)
    {
        var normalizedSourceType = sourceTypeRegistry.GetRequired(sourceType);
        var normalizedPurpose = purposeRegistry.GetRequired(purpose);
        var normalizedProfileCode = profileRegistry.GetRequired(processingProfileCode).Code;
        if (normalizedSourceType == IntakeSourceTypes.Manual)
        {
            await EnsureProfileAvailableAsync(tenantId, normalizedProfileCode, cancellationToken);
            var manualAddress = string.IsNullOrWhiteSpace(emailAddress)
                ? $"manual-{Guid.NewGuid():N}@intake.invalid"
                : emailAddress.Trim();
            return new ValidatedInput(
                normalizedSourceType,
                manualAddress,
                manualAddress.ToLowerInvariant(),
                "MANUAL",
                normalizedPurpose,
                normalizedProfileCode,
                "{}",
                null);
        }

        var normalizedProvider = connectorRegistry.GetRequired(provider).ProviderCode;
        compatibilityRegistry.EnsureCompatible(normalizedPurpose, normalizedProfileCode);
        var normalizedEmail = EmailAddressNormalizer.Normalize(emailAddress);
        var configurationJson = GetConfigurationJson(connectorConfiguration);
        var connectorValidation = connectorRegistry
            .GetRequired(normalizedProvider)
            .ValidateConfiguration(configurationJson);

        if (!connectorValidation.IsValid)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CONNECTOR_CONFIGURATION",
                connectorValidation.Message);
        }

        ValidateCredentialReference(credentialReference);
        await EnsureProfileAvailableAsync(tenantId, normalizedProfileCode, cancellationToken);

        return new ValidatedInput(
            normalizedSourceType,
            emailAddress.Trim(),
            normalizedEmail,
            normalizedProvider,
            normalizedPurpose,
            normalizedProfileCode,
            configurationJson,
            credentialReference);
    }

    private async Task EnsureTenantIntakeEnabledAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetConfigurationAsync(tenantId, cancellationToken);
        if (configuration is null || !configuration.IsEnabled)
        {
            throw IntakeConfigurationException.BadRequest(
                "TENANT_INTAKE_DISABLED",
                "Intake must be configured and enabled before an Intake source can be managed.");
        }
    }

    private async Task EnsureProfileAvailableAsync(
        Guid tenantId,
        string profileCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await configurationService.ResolveAsync(tenantId, profileCode, cancellationToken);
        }
        catch (IntakeConfigurationException exception)
        {
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_SOURCE_PROFILE_UNAVAILABLE",
                $"Processing profile '{profileCode}' is not available for this tenant: {exception.Code}.");
        }
    }

    private async Task EnsureDefaultCanBeSetAsync(
        Guid tenantId,
        string purpose,
        CancellationToken cancellationToken)
    {
        await EnsureTenantIntakeEnabledAsync(tenantId, cancellationToken);
        _ = purpose;
    }

    private async Task<IReadOnlyList<TenantIntakeSource>> ClearOtherDefaultsAsync(
        Guid tenantId,
        string purpose,
        Guid? exceptSourceId,
        Guid? actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sources = await repository.ListTenantPurposeSourcesAsync(
            tenantId,
            purpose,
            cancellationToken);
        var clearedDefaults = sources
            .Where(item => item.IsDefault && item.Id != exceptSourceId)
            .ToArray();
        foreach (var source in clearedDefaults)
        {
            source.IsDefault = false;
            source.DefaultTenantPurposeKey = null;
            Touch(source, actorId, now);
        }

        return clearedDefaults;
    }

    private async Task SaveAsync(CancellationToken cancellationToken) =>
        await repository.SaveChangesAsync(cancellationToken);

    private async Task<T> ExecuteTransactionalMutationAsync<T>(
        Func<List<Action>, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var pendingPublications = new List<Action>();
        var result = await repository.ExecuteInTransactionAsync(
            () => operation(pendingPublications),
            cancellationToken);

        foreach (var publication in pendingPublications)
            publication();

        return result;
    }

    private void QueueAudit(
        List<Action> pendingPublications,
        Guid tenantId,
        TenantIntakeSource source,
        string operation,
        int? previousVersion,
        int newVersion,
        Guid? actorId,
        string? correlationId,
        object metadata) =>
        pendingPublications.Add(() => RecordAudit(
            tenantId,
            source,
            operation,
            previousVersion,
            newVersion,
            actorId,
            correlationId,
            metadata));

    private void RecordAudit(
        Guid tenantId,
        TenantIntakeSource source,
        string operation,
        int? previousVersion,
        int newVersion,
        Guid? actorId,
        string? correlationId,
        object metadata) =>
        _ = auditSink.RecordAsync(
            new ConfigurationAuditEntry(
                tenantId,
                "TenantIntakeSource",
                source.Id.ToString(),
                operation,
                previousVersion,
                newVersion,
                actorId,
                correlationId,
                metadata),
            CancellationToken.None);

    private void QueueClearedDefaultAudits(
        List<Action> pendingPublications,
        Guid tenantId,
        IReadOnlyList<TenantIntakeSource> clearedDefaults,
        Guid? actorId,
        string? correlationId)
    {
        foreach (var source in clearedDefaults)
        {
            QueueAudit(
                pendingPublications,
                tenantId,
                source,
                "REMOVE_DEFAULT_SOURCE",
                source.ConfigurationVersion - 1,
                source.ConfigurationVersion,
                actorId,
                correlationId,
                new { source.Purpose, Result = "succeeded" });
        }
    }

    private void QueueLog(
        List<Action> pendingPublications,
        Guid tenantId,
        TenantIntakeSource source,
        string operation,
        string? correlationId,
        string result = "succeeded") =>
        pendingPublications.Add(() => LogMutation(
            tenantId,
            source,
            operation,
            correlationId,
            result));

    private void LogMutation(
        Guid tenantId,
        TenantIntakeSource source,
        string operation,
        string? correlationId,
        string result = "succeeded") =>
        logger.LogInformation(
            "Intake source mutation {Result} CorrelationId={CorrelationId} TenantId={TenantId} SourceId={SourceId} Provider={Provider} Purpose={Purpose} ProcessingProfileCode={ProcessingProfileCode} ConfigurationVersion={ConfigurationVersion} Operation={Operation}",
            result,
            correlationId,
            tenantId,
            source.Id,
            source.Provider,
            source.Purpose,
            source.ProcessingProfileCode,
            source.ConfigurationVersion,
            operation);

    private static void RequireExpectedVersion(int? expected, int actual)
    {
        if (!expected.HasValue)
        {
            throw IntakeConfigurationException.BadRequest(
                "SOURCE_CONFIGURATION_VERSION_REQUIRED",
                "configurationVersion is required for source mutation.");
        }

        if (expected.Value != actual)
        {
            throw IntakeConfigurationException.Conflict(
                "STALE_SOURCE_CONFIGURATION_VERSION",
                $"The supplied configurationVersion {expected.Value} is stale; current version is {actual}.");
        }
    }

    private static void ValidateCredentialReference(string? credentialReference)
    {
        if (credentialReference is null)
            return;

        if (!CredentialReferencePattern.IsMatch(credentialReference))
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CREDENTIAL_REFERENCE",
                "CredentialReference must be an approved secret://, credential://, or connection:// reference.");
        }
    }

    private static string GetConfigurationJson(JsonElement? configuration)
    {
        if (!configuration.HasValue || configuration.Value.ValueKind == JsonValueKind.Null)
            return "{}";

        if (configuration.Value.ValueKind != JsonValueKind.Object)
        {
            throw IntakeConfigurationException.BadRequest(
                "INVALID_CONNECTOR_CONFIGURATION",
                "ConnectorConfiguration must be a JSON object.");
        }

        return configuration.Value.GetRawText();
    }

    private static JsonElement ParseConfiguration(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement.Clone();
    }

    private static string DefaultKey(Guid tenantId, string purpose) =>
        $"{tenantId:N}:{purpose}";

    private static void Touch(TenantIntakeSource source, Guid? actorId, DateTimeOffset now)
    {
        source.ConfigurationVersion++;
        source.UpdatedAt = now;
        source.UpdatedBy = actorId;
    }

    private static IntakeSourceResponse Map(TenantIntakeSource source)
    {
        using var document = JsonDocument.Parse(source.ConnectorConfigurationJson);
        return new IntakeSourceResponse(
            source.Id,
            source.TenantId,
            source.OrgId,
            source.SourceType,
            source.EmailAddress,
            source.NormalizedEmailAddress,
            source.Provider,
            source.Purpose,
            source.ProcessingProfileCode,
            source.IsActive,
            source.IsDefault,
            source.CredentialReference,
            document.RootElement.Clone(),
            source.ValidationStatus,
            source.LastValidatedAt,
            source.LastValidationMessage,
            source.ConfigurationVersion,
            source.CreatedAt,
            source.CreatedBy,
            source.UpdatedAt,
            source.UpdatedBy);
    }

    private sealed record ValidatedInput(
        string SourceType,
        string EmailAddress,
        string NormalizedEmailAddress,
        string Provider,
        string Purpose,
        string ProcessingProfileCode,
        string ConnectorConfigurationJson,
        string? CredentialReference);
}