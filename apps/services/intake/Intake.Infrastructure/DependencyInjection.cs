using Intake.Application.Configuration;
using Intake.Application.Artifacts;
using Intake.Application.Emails;
using Intake.Application.Sources;
using Intake.Application.Manual;
using Intake.Application.Classification;
using Intake.Application.Extraction;
using Intake.Application.Normalization;
using Intake.Application.Matching;
using Intake.Application.Policy;
using Intake.Application.Review;
using Intake.Application.Snapshot;
using Intake.Application.Operations;
using Intake.Infrastructure.Artifacts;
using Intake.Infrastructure.Audit;
using Intake.Infrastructure.Classification;
using Intake.Infrastructure.Health;
using Intake.Infrastructure.Persistence;
using Intake.Infrastructure.Matching;
using Intake.Infrastructure.Snapshot;
using Intake.Infrastructure.Operations;
using Intake.Domain.Operations;
using Intake.Domain.Matching;
using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using BuildingBlocks.Authentication.ServiceTokens;

namespace Intake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IntakeDatabase");

        services.AddDbContextFactory<IntakeDbContext>(options =>
        {
            // Development can start without a database so /health remains
            // useful. Readiness reports the missing/unreachable database.
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseMySql(
                    connectionString,
                    new MySqlServerVersion(new Version(8, 0, 0)));
            }
        });

        services.AddScoped<IIntakeConfigurationRepository, EfIntakeConfigurationRepository>();
        services.AddSingleton<IProcessingProfileRegistry, ProcessingProfileRegistry>();
        services.AddScoped<IIntakeConfigurationAuditSink, IntakeConfigurationAuditSink>();
        services.AddScoped<IIntakeConfigurationService, IntakeConfigurationService>();
        services.AddScoped<IIntakeSourceRepository, EfIntakeSourceRepository>();
        services.AddSingleton<IIntakeSourceTypeRegistry, IntakeSourceTypeRegistry>();
        services.AddSingleton<IIntakeSourcePurposeRegistry, IntakeSourcePurposeRegistry>();
        services.AddSingleton<IIntakeSourceProfileCompatibilityRegistry, IntakeSourceProfileCompatibilityRegistry>();
        services.AddSingleton<IEmailConnectorRegistry, EmailConnectorRegistry>();
        services.AddScoped<IIntakeSourceService, IntakeSourceService>();
        services.AddScoped<IIntakeSourceResolver, IntakeSourceResolver>();
        services.AddSingleton(
            configuration.GetSection("Intake:EmailCapture").Get<EmailCaptureOptions>() ?? new EmailCaptureOptions());
        services.AddScoped<IEmailCaptureService, EmailCaptureService>();
        services.AddScoped<IInboundEmailQueryService, InboundEmailQueryService>();
        services.AddScoped<IInboundEmailRepository, EfInboundEmailRepository>();
        services.AddScoped<IIntakeArtifactRepository, EfIntakeArtifactRepository>();
        services.AddScoped<IManualIntakeRepository, EfManualIntakeRepository>();
        services.AddScoped<IEmailArtifactAuditSink, EmailArtifactAuditSink>();
        services.AddScoped<IManualIntakeAuditSink, ManualIntakeAuditSink>();
        var artifactOptions = configuration.GetSection(EmailArtifactProcessingOptions.SectionName)
            .Get<EmailArtifactProcessingOptions>() ?? new EmailArtifactProcessingOptions();
        artifactOptions.DocumentsServiceDocumentTypeId ??=
            configuration["DocumentsService:DocumentTypeId"];
        artifactOptions.DocumentsServiceBaseUrl =
            configuration["DocumentsService:BaseUrl"] ??
            artifactOptions.DocumentsServiceBaseUrl;
        services.AddSingleton(artifactOptions);
        services.AddScoped<IEmailArtifactProcessingService, EmailArtifactProcessingService>();
        services.AddScoped<IManualIntakeService, ManualIntakeService>();
        services.AddScoped<IClassificationRepository, EfClassificationRepository>();
        services.AddScoped<IClassificationService, ClassificationService>();
        services.AddScoped<IArtifactExtractionRepository, EfArtifactExtractionRepository>();
        services.AddScoped<IArtifactExtractionService, ExtractionService>();
        services.AddSingleton<IManagedAiPolicyDefaults, ConfiguredManagedAiPolicyDefaults>();
        services.AddScoped<IClassificationAuditSink, ClassificationAuditSink>();
        services.AddScoped<IExtractionAuditSink, ExtractionAuditSink>();
        services.AddScoped<IIntakeArtifactContentReader, IntakeArtifactContentReader>();
        services.AddScoped<IArtifactNormalizationRepository, EfArtifactNormalizationRepository>();
        services.AddScoped<IArtifactNormalizationService, NormalizationService>();
        services.AddScoped<INormalizationAuditSink, NormalizationAuditSink>();
        services.AddScoped<IArtifactMatchingRepository, EfArtifactMatchingRepository>();
        services.AddScoped<IArtifactMatchingService, MatchingService>();
        services.AddScoped<IMatchingAuditSink, MatchingAuditSink>();
        services.AddScoped<IArtifactPolicyRepository, EfArtifactPolicyRepository>();
        services.AddScoped<IArtifactPolicyService, PolicyService>();
        services.AddScoped<IPolicyAuditSink, PolicyAuditSink>();
        services.AddScoped<IIntakeReviewRepository, EfIntakeReviewRepository>();
        services.AddScoped<IIntakeReviewService, IntakeReviewService>();
        services.AddScoped<IReviewAuditSink, ReviewAuditSink>();
        services.AddScoped<ISnapshotAuditSink, SnapshotAuditSink>();
        services.AddScoped<IApprovedSnapshotRepository, EfApprovedSnapshotRepository>();
        services.AddScoped<IAdapterExecutionRepository, EfAdapterExecutionRepository>();
        services.AddScoped<IDocumentAssociationExecutionRepository, EfDocumentAssociationExecutionRepository>();
        services.AddScoped<IReviewedIntakeProjectionService, B12ReviewedIntakeProjectionService>();
        services.AddScoped<IApprovedIntakeSnapshotService, ApprovedIntakeSnapshotService>();
        services.AddScoped<IIntakeAdapterExecutionService, IntakeAdapterExecutionService>();
        services.AddSingleton<IDocumentAssociationPolicy, SynqLienDocumentAssociationPolicy>();
        services.AddScoped<IDocumentAssociationExecutionService, DocumentAssociationExecutionService>();
        var recoveryOptions = configuration.GetSection(IntakeRecoveryOptions.SectionName)
            .Get<IntakeRecoveryOptions>() ?? new IntakeRecoveryOptions();
        recoveryOptions.ScanIntervalSeconds = Math.Clamp(recoveryOptions.ScanIntervalSeconds, 5, 3600);
        recoveryOptions.ProcessingStaleAfterMinutes =
            Math.Clamp(recoveryOptions.ProcessingStaleAfterMinutes, 1, 1440);
        recoveryOptions.MaxItemsPerScan = Math.Clamp(recoveryOptions.MaxItemsPerScan, 1, 500);
        recoveryOptions.MaxRecoveryAttempts = Math.Clamp(recoveryOptions.MaxRecoveryAttempts, 1, 50);
        recoveryOptions.MaxConcurrentRecoveries = Math.Clamp(recoveryOptions.MaxConcurrentRecoveries, 1, 32);
        services.AddSingleton(recoveryOptions);
        services.AddSingleton<RecoveryWorkerState>();
        services.AddSingleton<IntakeMetrics>();
        services.AddScoped<IIntakeRecoveryRepository, EfIntakeRecoveryRepository>();
        services.AddScoped<IRecoveryAuditSink, RecoveryAuditSink>();
        services.AddScoped<IIntakeRecoveryService, IntakeRecoveryService>();
        services.AddScoped<IIntakeRecoveryHandler, SnapshotRecoveryHandler>();
        services.AddScoped<IIntakeRecoveryHandler, AdapterRecoveryHandler>();
        services.AddScoped<IIntakeRecoveryHandler, DocumentAssociationRecoveryHandler>();
        foreach (var stage in new[]
        {
            IntakeRecoveryStages.EmailCapture,
            IntakeRecoveryStages.ArtifactProcessing,
            IntakeRecoveryStages.Classification,
            IntakeRecoveryStages.Extraction,
            IntakeRecoveryStages.Normalization,
            IntakeRecoveryStages.Matching,
            IntakeRecoveryStages.Policy,
            IntakeRecoveryStages.Review,
        })
        {
            var currentStage = stage;
            services.AddScoped<IIntakeRecoveryHandler>(_ =>
                new DeterministicAttentionRecoveryHandler(currentStage));
        }
        services.AddScoped<IIntakeRecoveryRegistry, IntakeRecoveryRegistry>();
        services.AddHostedService<IntakeRecoveryWorker>();
        services.AddSingleton<ICanonicalSnapshotSerializer, CanonicalSnapshotSerializer>();
        services.AddSingleton<IIntakeDestinationAdapter, NoopV1Adapter>();
        var synqLienOptions = configuration.GetSection(SynqLienDestinationOptions.SectionName)
            .Get<SynqLienDestinationOptions>() ?? new SynqLienDestinationOptions();
        services.AddSingleton(synqLienOptions);
        services.AddSingleton<IIntakeDestinationAdapter, SynqLienV1Adapter>();
        services.AddSingleton<IIntakeDestinationAdapterRegistry, IntakeDestinationAdapterRegistry>();
        services.AddSingleton(
            configuration.GetSection(IntakeAdapterOptions.SectionName).Get<IntakeAdapterOptions>()
            ?? new IntakeAdapterOptions());
        services.AddHttpClient<ISynqLienClient, SynqLienClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<SynqLienDestinationOptions>();
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 300));
        });
        services.AddHttpClient<IDocumentAssociationDestinationClient, SynqLienDocumentAssociationClient>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<SynqLienDestinationOptions>();
                if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                    client.BaseAddress = baseUri;
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 300));
            });
        services.AddSingleton<IPolicyRule, ClassificationEligibilityRule>();
        services.AddSingleton<IPolicyRule, ClassificationConfidenceRule>();
        services.AddSingleton<IPolicyRule, RequiredFactsRule>();
        services.AddSingleton<IPolicyRule, CriticalFactConfidenceRule>();
        services.AddSingleton<IPolicyRule, StructuralValidityRule>();
        services.AddSingleton<IPolicyRule, EvidencePresenceRule>();
        services.AddSingleton<IPolicyRule, PatientMatchRule>();
        services.AddSingleton<IPolicyRule, ProviderFacilityMatchRule>();
        services.AddSingleton<IPolicyRule, CaseMatchRule>();
        services.AddSingleton<IPolicyRule, HardIdentifierRule>();
        services.AddSingleton<IPolicyRule, HardConflictRule>();
        services.AddSingleton<IPolicyRule, DuplicateRule>();
        services.AddSingleton<IPolicyRule, NormalizationWarningRule>();
        services.AddSingleton<IPolicyRuleRegistry, PolicyRuleRegistry>();
        services.Configure<TenantMatchCandidateOptions>(
            configuration.GetSection(TenantMatchCandidateOptions.SectionName));
        services.PostConfigure<TenantMatchCandidateOptions>(options =>
        {
            options.InternalToken ??= configuration["PublicTrustBoundary:InternalRequestSecret"];
        });
        services.AddHttpClient<ITenantMatchCandidateSource, HttpTenantMatchCandidateSource>();
        services.AddScoped<ITenantMatchCandidateProvider>(serviceProvider =>
            new TenantMatchCandidateProvider(
                serviceProvider.GetRequiredService<ITenantMatchCandidateSource>(),
                MatchingEntityTypes.Patient));
        services.AddScoped<ITenantMatchCandidateProvider>(serviceProvider =>
            new TenantMatchCandidateProvider(
                serviceProvider.GetRequiredService<ITenantMatchCandidateSource>(),
                MatchingEntityTypes.Provider));
        services.AddScoped<ITenantMatchCandidateProvider>(serviceProvider =>
            new TenantMatchCandidateProvider(
                serviceProvider.GetRequiredService<ITenantMatchCandidateSource>(),
                MatchingEntityTypes.Facility));
        services.AddScoped<ITenantMatchCandidateProvider>(serviceProvider =>
            new TenantMatchCandidateProvider(
                serviceProvider.GetRequiredService<ITenantMatchCandidateSource>(),
                MatchingEntityTypes.Attorney));
        services.AddScoped<ITenantMatchCandidateProvider>(serviceProvider =>
            new TenantMatchCandidateProvider(
                serviceProvider.GetRequiredService<ITenantMatchCandidateSource>(),
                MatchingEntityTypes.LawFirm));
        services.AddScoped<ITenantMatchCandidateProvider>(serviceProvider =>
            new TenantMatchCandidateProvider(
                serviceProvider.GetRequiredService<ITenantMatchCandidateSource>(),
                MatchingEntityTypes.Case));
        services.AddScoped<IMatchCandidateProviderRegistry, MatchCandidateProviderRegistry>();
        services.AddSingleton<IFactNormalizerRegistry, FactNormalizerRegistry>();
        services.AddSingleton<IFactNormalizer, PersonNameNormalizer>();
        services.AddSingleton<IFactNormalizer, OrganizationNormalizer>();
        services.AddSingleton<IFactNormalizer, DateNormalizer>();
        services.AddSingleton<IFactNormalizer, MoneyNormalizer>();
        services.AddSingleton<IFactNormalizer, PhoneNormalizer>();
        services.AddSingleton<IFactNormalizer, EmailNormalizer>();
        services.AddSingleton<IFactNormalizer, AddressNormalizer>();
        services.AddSingleton<IFactNormalizer, IdentifierNormalizer>();
        services.AddSingleton<IFactNormalizer, TextNormalizer>();
        services.AddSingleton<IAiCredentialResolver, EnvironmentAiCredentialResolver>();
        services.AddSingleton<ISynqAiProvider, OpenAiSynqAiProvider>();
        services.AddSingleton<ISynqAiProviderRegistry, ConfiguredSynqAiProviderRegistry>();
        services.Configure<SynqAiOptions>(
            configuration.GetSection(SynqAiOptions.SectionName));
        services.AddScoped<IEmailArtifactExtractor, MimeKitEmailArtifactExtractor>();
        services.AddHttpClient("DocumentsService", client =>
        {
            client.BaseAddress = new Uri(
                (configuration["DocumentsService:BaseUrl"] ??
                 configuration[$"{EmailArtifactProcessingOptions.SectionName}:DocumentsServiceBaseUrl"] ??
                 "http://localhost:5006").TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue(
                    $"{EmailArtifactProcessingOptions.SectionName}:DocumentsServiceTimeoutSeconds",
                    60));
        });
        services.AddHttpClient("SynqAiOpenAI", client =>
        {
            var baseUrl = configuration["SynqAi:OpenAi:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue("SynqAi:OpenAi:TimeoutSeconds", 60));
        });
        services.AddScoped<IIntakeDocumentsClient, DocumentsServiceClient>();
        services.AddScoped<IIntakeDocumentContentClient>(provider =>
            (DocumentsServiceClient)provider.GetRequiredService<IIntakeDocumentsClient>());
        services.AddAuditEventClient(configuration);

        services.AddHealthChecks()
            .AddCheck(
                "process",
                () => HealthCheckResult.Healthy("Intake process is running"),
                tags: ["live"])
            .AddCheck<IntakeDatabaseHealthCheck>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);
        services.AddHealthChecks()
            .AddCheck<RecoveryWorkerHealthCheck>(
                "recovery-worker",
                failureStatus: HealthStatus.Degraded,
                tags: ["live", "ready"]);

        return services;
    }
}