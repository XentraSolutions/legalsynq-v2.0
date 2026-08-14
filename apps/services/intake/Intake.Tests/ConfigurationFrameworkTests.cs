using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Contracts.Configuration;
using Intake.Domain.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class ConfigurationFrameworkTests
{
    [Fact]
    public void Registry_exposes_conservative_lien_intake_defaults()
    {
        var registry = new ProcessingProfileRegistry();
        var configuration = registry.ValidateAndDeserialize(
            ProcessingProfileCodes.LienIntakeV1,
            "{}");

        Assert.True(configuration.RequireHumanReview);
        Assert.False(configuration.AllowAutoApproval);
        Assert.Equal(0.95, configuration.AutoApproveThreshold);
        Assert.Equal(0.75, configuration.ReviewThreshold);
        Assert.Equal(0.50, configuration.RejectThreshold);
        Assert.True(configuration.ProcessAttachments);
        Assert.True(configuration.ProcessEmailBody);
        Assert.False(configuration.EnablePatientMatching);
    }

    [Fact]
    public void Registry_rejects_unknown_properties_and_bad_threshold_order()
    {
        var registry = new ProcessingProfileRegistry();

        var unknownProperty = Assert.Throws<IntakeConfigurationException>(() =>
            registry.ValidateAndDeserialize(
                ProcessingProfileCodes.LienIntakeV1,
                """{"notSupported":true}"""));
        Assert.Equal(400, unknownProperty.StatusCode);
        Assert.Equal("INVALID_PROFILE_CONFIGURATION", unknownProperty.Code);

        var badThresholds = Assert.Throws<IntakeConfigurationException>(() =>
            registry.ValidateAndDeserialize(
                ProcessingProfileCodes.LienIntakeV1,
                """{"autoApproveThreshold":0.5,"reviewThreshold":0.75,"rejectThreshold":0.2}"""));
        Assert.Equal("INVALID_THRESHOLD_ORDER", badThresholds.Code);

        var invalidCode = Assert.Throws<IntakeConfigurationException>(() =>
            registry.GetRequired("bad-code"));
        Assert.Equal("INVALID_PROFILE_CODE", invalidCode.Code);
    }

    [Fact]
    public async Task Assigning_default_profile_creates_consistent_configuration_and_resolves_it()
    {
        var repository = new FakeRepository();
        var audit = new RecordingAuditSink();
        var service = CreateService(repository, audit);
        var tenantId = Guid.NewGuid();

        var profile = await service.AssignProfileAsync(
            tenantId,
            new AssignTenantProcessingProfileRequest
            {
                ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
                IsDefault = true,
                Configuration = Json("""{"reviewThreshold":0.7,"rejectThreshold":0.4}"""),
            },
            Guid.NewGuid(),
            "corr-1",
            CancellationToken.None);

        Assert.True(profile.IsDefault);
        Assert.Equal(1, profile.ConfigurationVersion);

        var configuration = await service.GetConfigurationAsync(tenantId, CancellationToken.None);
        Assert.NotNull(configuration);
        Assert.Equal(ProcessingProfileCodes.LienIntakeV1, configuration.DefaultProcessingProfileCode);
        Assert.Equal(1, configuration.ConfigurationVersion);

        var resolved = await service.ResolveAsync(tenantId, null, CancellationToken.None);
        Assert.Equal(ProcessingProfileCodes.LienIntakeV1, resolved.ProcessingProfileCode);
        Assert.Equal(0.7, resolved.EffectiveConfiguration.ReviewThreshold);
        Assert.Equal(0.4, resolved.EffectiveConfiguration.RejectThreshold);
        Assert.Equal(2, audit.Entries.Count);
    }

    [Fact]
    public async Task Disabling_current_default_is_rejected_until_default_changes()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new RecordingAuditSink());
        var tenantId = Guid.NewGuid();

        var profile = await service.AssignProfileAsync(
            tenantId,
            new AssignTenantProcessingProfileRequest
            {
                ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
                IsDefault = true,
            },
            null,
            null,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.UpdateTenantProfileStatusAsync(
                tenantId,
                ProcessingProfileCodes.LienIntakeV1,
                new UpdateTenantProcessingProfileStatusRequest
                {
                    IsEnabled = false,
                    ConfigurationVersion = profile.ConfigurationVersion,
                },
                null,
                null,
                CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("DEFAULT_PROFILE_MUST_BE_CHANGED_FIRST", exception.Code);
    }

    [Fact]
    public async Task Stale_tenant_configuration_update_returns_conflict()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new RecordingAuditSink());
        var tenantId = Guid.NewGuid();

        var created = await service.UpsertConfigurationAsync(
            tenantId,
            new UpsertTenantIntakeConfigurationRequest(),
            null,
            null,
            CancellationToken.None);

        var updated = await service.UpsertConfigurationAsync(
            tenantId,
            new UpsertTenantIntakeConfigurationRequest
            {
                IsEnabled = false,
                ConfigurationVersion = created.ConfigurationVersion,
            },
            null,
            null,
            CancellationToken.None);

        Assert.Equal(2, updated.ConfigurationVersion);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            service.UpsertConfigurationAsync(
                tenantId,
                new UpsertTenantIntakeConfigurationRequest
                {
                    IsEnabled = true,
                    ConfigurationVersion = created.ConfigurationVersion,
                },
                null,
                null,
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("STALE_CONFIGURATION_VERSION", exception.Code);
    }

    [Fact]
    public async Task Configuration_default_update_also_updates_assignment_default_state()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new RecordingAuditSink());
        var tenantId = Guid.NewGuid();

        var assigned = await service.AssignProfileAsync(
            tenantId,
            new AssignTenantProcessingProfileRequest
            {
                ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
                IsDefault = false,
            },
            null,
            null,
            CancellationToken.None);
        var configuration = await service.UpsertConfigurationAsync(
            tenantId,
            new UpsertTenantIntakeConfigurationRequest(),
            null,
            null,
            CancellationToken.None);

        var updated = await service.UpsertConfigurationAsync(
            tenantId,
            new UpsertTenantIntakeConfigurationRequest
            {
                DefaultProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
                ConfigurationVersion = configuration.ConfigurationVersion,
            },
            null,
            null,
            CancellationToken.None);

        Assert.Equal(ProcessingProfileCodes.LienIntakeV1, updated.DefaultProcessingProfileCode);
        var profile = await service.GetTenantProfileAsync(
            tenantId,
            ProcessingProfileCodes.LienIntakeV1,
            CancellationToken.None);
        Assert.NotNull(profile);
        Assert.True(profile.IsDefault);
        Assert.Equal(2, profile.ConfigurationVersion);
        Assert.Equal(1, assigned.ConfigurationVersion);
    }

    [Fact]
    public async Task Tenant_profile_listing_is_isolated_by_tenant_id()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new RecordingAuditSink());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await service.AssignProfileAsync(
            tenantA,
            new AssignTenantProcessingProfileRequest
            {
                ProcessingProfileCode = ProcessingProfileCodes.LienIntakeV1,
            },
            null,
            null,
            CancellationToken.None);

        var tenantAProfiles = await service.ListTenantProfilesAsync(tenantA, CancellationToken.None);
        var tenantBProfiles = await service.ListTenantProfilesAsync(tenantB, CancellationToken.None);

        Assert.Single(tenantAProfiles);
        Assert.Empty(tenantBProfiles);
        Assert.Null(await service.GetTenantProfileAsync(
            tenantB,
            ProcessingProfileCodes.LienIntakeV1,
            CancellationToken.None));
    }

    private static IntakeConfigurationService CreateService(
        FakeRepository repository,
        RecordingAuditSink audit) =>
        new(
            repository,
            new ProcessingProfileRegistry(),
            audit,
            NullLogger<IntakeConfigurationService>.Instance);

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class RecordingAuditSink : IIntakeConfigurationAuditSink
    {
        public List<ConfigurationAuditEntry> Entries { get; } = [];

        public Task RecordAsync(ConfigurationAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepository : IIntakeConfigurationRepository
    {
        private readonly List<TenantIntakeConfiguration> configurations = [];
        private readonly List<TenantProcessingProfile> profiles = [];
        private readonly List<ProcessingProfileDefinition> definitions =
        [
            new()
            {
                Id = ProcessingProfileDefinitionIds.LienIntakeV1,
                Code = ProcessingProfileCodes.LienIntakeV1,
                DisplayName = "Lien Intake V1",
                Description = "Test profile",
                Version = 1,
                IsActive = true,
                IsSystemDefined = true,
            },
        ];

        public Task<TenantIntakeConfiguration?> FindTenantConfigurationAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(configurations.SingleOrDefault(item => item.TenantId == tenantId));

        public Task<ProcessingProfileDefinition?> FindDefinitionByCodeAsync(
            string code,
            CancellationToken cancellationToken) =>
            Task.FromResult(definitions.SingleOrDefault(item => item.Code == code));

        public Task<IReadOnlyList<ProcessingProfileDefinition>> ListActiveDefinitionsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProcessingProfileDefinition>>(
                definitions.Where(item => item.IsActive).ToArray());

        public Task<IReadOnlyList<TenantProcessingProfile>> ListTenantProfilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantProcessingProfile>>(
                profiles.Where(item => item.TenantId == tenantId).ToArray());

        public Task<TenantProcessingProfile?> FindTenantProfileAsync(
            Guid tenantId,
            string code,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                profiles.SingleOrDefault(item =>
                    item.TenantId == tenantId &&
                    item.ProcessingProfileDefinition?.Code == code));

        public void AddTenantConfiguration(TenantIntakeConfiguration configuration) =>
            configurations.Add(configuration);

        public void AddTenantProfile(TenantProcessingProfile profile) =>
            profiles.Add(profile);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}