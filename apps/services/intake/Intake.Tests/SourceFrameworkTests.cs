using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Application.Sources;
using Intake.Contracts.Configuration;
using Intake.Contracts.Sources;
using Intake.Domain.Configuration;
using Intake.Domain.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class SourceFrameworkTests
{
    [Fact]
    public async Task Create_normalizes_email_and_starts_source_version_at_one()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);

        var source = await service.CreateAsync(
            tenantId,
            new CreateIntakeSourceRequest
            {
                SourceType = " email ",
                EmailAddress = " Intake@Example.COM ",
                Provider = IntakeSourceProviders.Generic,
                Purpose = IntakeSourcePurposes.LienIntake,
                ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
                ConnectorConfiguration = Json("{}"),
            },
            null,
            "corr-source-1",
            CancellationToken.None);

        Assert.Equal("Intake@Example.COM", source.EmailAddress);
        Assert.Equal("Intake@example.com", source.NormalizedEmailAddress);
        Assert.Equal(1, source.ConfigurationVersion);
        Assert.Equal(IntakeSourceValidationStatuses.NotValidated, source.ValidationStatus);
    }

    [Fact]
    public async Task Duplicate_normalized_email_is_rejected_across_tenants()
    {
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);
        var request = new CreateIntakeSourceRequest
        {
            SourceType = IntakeSourceTypes.Email,
            EmailAddress = "intake@example.com",
            Provider = IntakeSourceProviders.Generic,
            Purpose = IntakeSourcePurposes.LienIntake,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
        };

        await service.CreateAsync(Guid.NewGuid(), request, null, null, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.CreateAsync(
                Guid.NewGuid(),
                new CreateIntakeSourceRequest
                {
                    SourceType = request.SourceType,
                    EmailAddress = "intake@EXAMPLE.COM",
                    Provider = request.Provider,
                    Purpose = request.Purpose,
                    ProcessingProfileCode = request.ProcessingProfileCode,
                },
                null,
                null,
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("DUPLICATE_SOURCE_EMAIL", exception.Code);
    }

    [Fact]
    public async Task Current_default_cannot_be_disabled_and_status_changes_increment_version()
    {
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);
        var source = await service.CreateAsync(
            Guid.NewGuid(),
            NewRequest("default@example.com", isDefault: true),
            null,
            null,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.UpdateStatusAsync(
                source.TenantId,
                source.SourceId,
                new UpdateIntakeSourceStatusRequest
                {
                    IsActive = false,
                    ConfigurationVersion = source.ConfigurationVersion,
                },
                null,
                null,
                CancellationToken.None));

        Assert.Equal("DEFAULT_SOURCE_MUST_BE_CHANGED_FIRST", exception.Code);

        var updated = await service.UpdateAsync(
            source.TenantId,
            source.SourceId,
            new UpdateIntakeSourceRequest
            {
                SourceType = source.SourceType,
                EmailAddress = source.EmailAddress,
                Provider = source.Provider,
                Purpose = source.Purpose,
                ProcessingProfileCode = source.ProcessingProfileCode,
                IsDefault = false,
                ConfigurationVersion = source.ConfigurationVersion,
            },
            null,
            null,
            CancellationToken.None);

        var disabled = await service.UpdateStatusAsync(
            source.TenantId,
            source.SourceId,
            new UpdateIntakeSourceStatusRequest
            {
                IsActive = false,
                ConfigurationVersion = updated.ConfigurationVersion,
            },
            null,
            null,
            CancellationToken.None);

        Assert.False(disabled.IsActive);
        Assert.Equal(3, disabled.ConfigurationVersion);
        Assert.False(disabled.IsDefault);
    }

    [Fact]
    public async Task Resolver_uses_recipient_ownership_and_rejects_inactive_or_unknown_sources()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);
        var source = await service.CreateAsync(
            tenantId,
            NewRequest("resolve@example.com"),
            null,
            null,
            CancellationToken.None);
        var resolver = new IntakeSourceResolver(
            repository,
            new FakeConfigurationService(),
            new IntakeSourceProfileCompatibilityRegistry());

        var resolved = await resolver.ResolveByEmailAddressAsync(
            "resolve@EXAMPLE.COM",
            CancellationToken.None);

        Assert.Equal(source.SourceId, resolved.SourceId);
        Assert.Equal(tenantId, resolved.TenantId);
        Assert.Equal(IntakeSourcePurposes.LienIntake, resolved.Purpose);
        Assert.Equal(ProcessingProfileCodes.LienIntakeV1, resolved.ProcessingProfileCode);

        source = await service.UpdateStatusAsync(
            tenantId,
            source.SourceId,
            new UpdateIntakeSourceStatusRequest
            {
                IsActive = false,
                ConfigurationVersion = source.ConfigurationVersion,
            },
            null,
            null,
            CancellationToken.None);

        var inactive = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            resolver.ResolveByEmailAddressAsync(
                "resolve@example.com",
                CancellationToken.None));
        Assert.Equal("INTAKE_SOURCE_INACTIVE", inactive.Code);

        var unknown = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            resolver.ResolveByEmailAddressAsync(
                "unknown@example.com",
                CancellationToken.None));
        Assert.Equal("INTAKE_SOURCE_NOT_FOUND", unknown.Code);
    }

    [Fact]
    public async Task Create_rejects_unavailable_profile_and_disabled_tenant()
    {
        var repository = new FakeSourceRepository();
        var unavailableService = CreateService(
            repository,
            new FakeConfigurationService(profileAvailable: false));

        var profileException = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            unavailableService.CreateAsync(
                Guid.NewGuid(),
                NewRequest("unavailable@example.com"),
                null,
                null,
                CancellationToken.None));
        Assert.Equal("INTAKE_SOURCE_PROFILE_UNAVAILABLE", profileException.Code);

        var disabledService = CreateService(
            new FakeSourceRepository(),
            new FakeConfigurationService(enabled: false));
        var tenantException = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            disabledService.CreateAsync(
                Guid.NewGuid(),
                NewRequest("disabled@example.com"),
                null,
                null,
                CancellationToken.None));
        Assert.Equal("TENANT_INTAKE_DISABLED", tenantException.Code);
    }

    [Fact]
    public void Provider_registry_rejects_unknown_provider_and_connector_fields()
    {
        var registry = new EmailConnectorRegistry();
        var exception = Assert.Throws<IntakeConfigurationException>(() =>
            registry.GetRequired("UNKNOWN_PROVIDER"));
        Assert.Equal("UNSUPPORTED_EMAIL_PROVIDER", exception.Code);

        var validation = registry
            .GetRequired(IntakeSourceProviders.Generic)
            .ValidateConfiguration("""{"accessToken":"secret"}""");
        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task Manual_source_type_does_not_require_mailbox_or_connector_configuration()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);

        var source = await service.CreateAsync(
            tenantId,
            new CreateIntakeSourceRequest
            {
                SourceType = IntakeSourceTypes.Manual,
                Purpose = IntakeSourcePurposes.LienIntake,
                ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            },
            null,
            null,
            CancellationToken.None);

        Assert.Equal(IntakeSourceTypes.Manual, source.SourceType);
        Assert.Equal("MANUAL", source.Provider);
        Assert.StartsWith("manual-", source.EmailAddress, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_source_update_returns_conflict()
    {
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);
        var source = await service.CreateAsync(
            Guid.NewGuid(),
            NewRequest("stale@example.com"),
            null,
            null,
            CancellationToken.None);

        var updated = await service.UpdateAsync(
            source.TenantId,
            source.SourceId,
            UpdateRequest(source, "new@example.com", source.ConfigurationVersion),
            null,
            null,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.UpdateAsync(
                source.TenantId,
                source.SourceId,
                UpdateRequest(source, "another@example.com", source.ConfigurationVersion),
                null,
                null,
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("STALE_SOURCE_CONFIGURATION_VERSION", exception.Code);
        Assert.Equal(2, updated.ConfigurationVersion);
    }

    [Fact]
    public async Task Stale_source_validation_returns_conflict()
    {
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);
        var source = await service.CreateAsync(
            Guid.NewGuid(),
            NewRequest("validate@example.com"),
            null,
            null,
            CancellationToken.None);

        var validated = await service.ValidateAsync(
            source.TenantId,
            source.SourceId,
            source.ConfigurationVersion,
            null,
            null,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.ValidateAsync(
                source.TenantId,
                source.SourceId,
                source.ConfigurationVersion,
                null,
                null,
                CancellationToken.None));

        Assert.Equal(IntakeSourceValidationStatuses.Valid, validated.ValidationStatus);
        Assert.Equal("STALE_SOURCE_CONFIGURATION_VERSION", exception.Code);
    }

    [Fact]
    public async Task Tenant_source_listing_is_isolated()
    {
        var repository = new FakeSourceRepository();
        var service = CreateService(repository);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await service.CreateAsync(
            tenantA,
            NewRequest("tenant-a@example.com"),
            null,
            null,
            CancellationToken.None);

        Assert.Single(await service.ListAsync(tenantA, CancellationToken.None));
        Assert.Empty(await service.ListAsync(tenantB, CancellationToken.None));
        Assert.Null(await service.GetAsync(
            tenantB,
            repository.Sources.Single().Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task Failed_source_transaction_does_not_publish_audit()
    {
        var repository = new FakeSourceRepository { ThrowOnTransaction = true };
        var audit = new RecordingAuditSink();
        var service = CreateService(repository, audit: audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                Guid.NewGuid(),
                NewRequest("rollback@example.com"),
                null,
                null,
                CancellationToken.None));

        Assert.Empty(audit.Entries);
    }

    private static IntakeSourceService CreateService(
        FakeSourceRepository repository,
        FakeConfigurationService? configurationService = null,
        RecordingAuditSink? audit = null) =>
        new(
            repository,
            configurationService ?? new FakeConfigurationService(),
            new ProcessingProfileRegistry(),
            new IntakeSourceTypeRegistry(),
            new IntakeSourcePurposeRegistry(),
            new IntakeSourceProfileCompatibilityRegistry(),
            new EmailConnectorRegistry(),
            audit ?? new RecordingAuditSink(),
            NullLogger<IntakeSourceService>.Instance);

    private static CreateIntakeSourceRequest NewRequest(
        string emailAddress,
        bool isDefault = false) =>
        new()
        {
            SourceType = IntakeSourceTypes.Email,
            EmailAddress = emailAddress,
            Provider = IntakeSourceProviders.Generic,
            Purpose = IntakeSourcePurposes.LienIntake,
            ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            IsDefault = isDefault,
        };

    private static UpdateIntakeSourceRequest UpdateRequest(
        IntakeSourceResponse source,
        string emailAddress,
        int version) =>
        new()
        {
            SourceType = source.SourceType,
            EmailAddress = emailAddress,
            Provider = source.Provider,
            Purpose = source.Purpose,
            ProcessingProfileCode = source.ProcessingProfileCode,
            IsDefault = source.IsDefault,
            ConnectorConfiguration = source.ConnectorConfiguration,
            CredentialReference = source.CredentialReference,
            ConfigurationVersion = version,
        };

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class RecordingAuditSink : IIntakeConfigurationAuditSink
    {
        public List<ConfigurationAuditEntry> Entries { get; } = [];

        public Task RecordAsync(
            ConfigurationAuditEntry entry,
            CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfigurationService(
        bool enabled = true,
        bool profileAvailable = true) : IIntakeConfigurationService
    {
        public Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantIntakeConfigurationResponse?>(
                new(
                    tenantId,
                    null,
                    enabled,
                    ProcessingProfileCodes.LienIntakeV1,
                    true,
                    false,
                    1,
                    DateTimeOffset.UtcNow,
                    null,
                    DateTimeOffset.UtcNow,
                    null));

        public Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(
            Guid tenantId,
            UpsertTenantIntakeConfigurationRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TenantProcessingProfileResponse> AssignProfileAsync(
            Guid tenantId,
            AssignTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileStatusRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ResolvedProcessingConfiguration> ResolveAsync(
            Guid tenantId,
            string? profileCode,
            CancellationToken cancellationToken)
        {
            if (!enabled)
                throw IntakeConfigurationException.BadRequest(
                    "TENANT_INTAKE_DISABLED",
                    "Intake is disabled.");
            if (!profileAvailable)
                throw IntakeConfigurationException.NotFound(
                    "PROFILE_ASSIGNMENT_NOT_FOUND",
                    "Profile is unavailable.");

            return Task.FromResult(new ResolvedProcessingConfiguration(
                tenantId,
                ProcessingProfileCodes.LienIntakeV1,
                1,
                1,
                1,
                new LienIntakeV1Configuration(),
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeSourceRepository : IIntakeSourceRepository
    {
        public List<TenantIntakeSource> Sources { get; } = [];
        public bool ThrowOnTransaction { get; init; }

        public Task<IReadOnlyList<TenantIntakeSource>> ListTenantSourcesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantIntakeSource>>(
                Sources.Where(source => source.TenantId == tenantId).ToArray());

        public Task<TenantIntakeSource?> FindTenantSourceAsync(
            Guid tenantId,
            Guid sourceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Sources.SingleOrDefault(
                source => source.TenantId == tenantId && source.Id == sourceId));

        public Task<TenantIntakeSource?> FindByNormalizedEmailAddressAsync(
            string normalizedEmailAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(Sources.SingleOrDefault(
                source => source.NormalizedEmailAddress == normalizedEmailAddress));

        public Task<IReadOnlyList<TenantIntakeSource>> ListTenantPurposeSourcesAsync(
            Guid tenantId,
            string purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantIntakeSource>>(
                Sources.Where(source =>
                    source.TenantId == tenantId && source.Purpose == purpose).ToArray());

        public void Add(TenantIntakeSource source) => Sources.Add(source);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken)
        {
            if (ThrowOnTransaction)
                throw new InvalidOperationException("Simulated transaction failure.");

            return operation();
        }
    }
}