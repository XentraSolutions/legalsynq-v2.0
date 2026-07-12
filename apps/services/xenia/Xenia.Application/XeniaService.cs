using Xenia.Domain;

namespace Xenia.Application;

public sealed class XeniaService(
    IXeniaStateStore store,
    IAiProviderGateway providerGateway,
    IAiCredentialStore credentialStore,
    IAiUsageNormalizer usageNormalizer,
    IAiProviderHealthCheck providerHealthCheck,
    IProviderRoutingPolicy providerRoutingPolicy,
    IProviderFailoverPolicy providerFailoverPolicy) : IXeniaService
{
    private static readonly Guid ManagedConfigurationTenantId = Guid.Empty;

    public XeniaAdminOverviewResponse GetAdminOverview()
    {
        var tenantConfigurations = store.ListTenantConfigurations()
            .Where(configuration => configuration.TenantId != ManagedConfigurationTenantId)
            .ToList();
        var usage = GetUsage();

        return new XeniaAdminOverviewResponse(
            EnabledTenantCount: tenantConfigurations.Count(configuration => configuration.Enabled),
            DeploymentModelDistribution: tenantConfigurations
                .GroupBy(configuration => configuration.DeploymentModel.ToString())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            ProviderCount: store.ListProviders().Count,
            ConversationCount: tenantConfigurations.Sum(configuration => store.ListConversations(configuration.TenantId).Count),
            Usage: usage.Summary,
            ProviderHealth: GetProviderHealth());
    }

    public XeniaTenantConfigurationResponse GetManagedConfiguration() =>
        MapTenantConfiguration(GetOrCreateManagedConfiguration(), ManagedConfigurationTenantId);

    public XeniaTenantConfigurationResponse SaveManagedConfiguration(XeniaTenantConfigurationRequest request, string actorUserId)
    {
        var configuration = GetOrCreateManagedConfiguration();
        configuration.Enabled = request.Enabled;
        configuration.DeploymentModel = XeniaDeploymentModel.Managed;
        configuration.DefaultProviderConfigurationId = request.DefaultProviderConfigurationId;
        configuration.DefaultModel = string.IsNullOrWhiteSpace(request.DefaultModel) ? configuration.DefaultModel : request.DefaultModel.Trim();
        configuration.Temperature = request.Temperature;
        configuration.MaxTokens = request.MaxTokens;
        configuration.ReasoningLevel = request.ReasoningLevel.Trim();
        configuration.RetentionPolicy = request.RetentionPolicy.Trim();
        configuration.ModerationPolicy = request.ModerationPolicy.Trim();
        configuration.FailoverEnabled = request.FailoverEnabled;
        configuration.AllowedSkills = NormalizeList(request.AllowedSkills);
        configuration.AllowedAgents = NormalizeList(request.AllowedAgents);
        configuration.AllowedTools = NormalizeList(request.AllowedTools);
        configuration.UpdatedAtUtc = DateTime.UtcNow;

        var saved = store.SaveTenantConfiguration(configuration);
        AppendAudit(Guid.Empty, actorUserId, "xenia.managed.configuration.updated", "Platform managed AI configuration updated.");

        return MapTenantConfiguration(saved, ManagedConfigurationTenantId);
    }

    public XeniaTenantConfigurationResponse GetTenantConfiguration(Guid tenantId) =>
        MapTenantConfiguration(ResolveEffectiveConfiguration(tenantId), tenantId);

    public IReadOnlyList<XeniaProviderConfigurationResponse> ListProviders(Guid? tenantId = null)
    {
        var providers = store.ListProviders();
        return providers
            .Where(provider => tenantId == null || provider.Scope == XeniaProviderScope.Platform || provider.TenantId == tenantId)
            .OrderBy(provider => provider.Scope)
            .ThenBy(provider => provider.FailoverPriority)
            .ThenBy(provider => provider.DisplayName)
            .Select(MapProvider)
            .ToList();
    }

    public XeniaProviderConfigurationResponse CreatePlatformProvider(XeniaProviderConfigurationRequest request, string actorUserId)
    {
        var provider = BuildProviderConfiguration(request, XeniaProviderScope.Platform, null, existing: null);
        PersistCredential(provider, request);
        var saved = store.SaveProvider(provider);

        AppendAudit(Guid.Empty, actorUserId, "xenia.platform.provider.created", $"Platform provider '{saved.DisplayName}' created.");
        return MapProvider(saved);
    }

    public XeniaProviderConfigurationResponse UpdatePlatformProvider(Guid providerConfigurationId, XeniaProviderConfigurationRequest request, string actorUserId)
    {
        var existing = store.GetProvider(providerConfigurationId)
            ?? throw new InvalidOperationException($"Provider configuration '{providerConfigurationId}' was not found.");
        if (existing.Scope != XeniaProviderScope.Platform)
            throw new InvalidOperationException("Only platform-scoped providers can be updated from the admin API.");

        var updated = BuildProviderConfiguration(request, existing.Scope, existing.TenantId, existing);
        updated.ProviderConfigurationId = providerConfigurationId;
        PersistCredential(updated, request);

        var saved = store.SaveProvider(updated);
        AppendAudit(Guid.Empty, actorUserId, "xenia.platform.provider.updated", $"Platform provider '{saved.DisplayName}' updated.");

        return MapProvider(saved);
    }

    public XeniaProviderTestResponse TestProvider(Guid? tenantId, Guid? providerConfigurationId, XeniaProviderConfigurationRequest? request, string actorUserId)
    {
        XeniaProviderConfiguration provider;
        XeniaResolvedCredential? credential;

        if (providerConfigurationId.HasValue)
        {
            provider = store.GetProvider(providerConfigurationId.Value)
                ?? throw new InvalidOperationException($"Provider configuration '{providerConfigurationId}' was not found.");
            credential = credentialStore.Resolve(provider.ProviderConfigurationId, provider);
        }
        else if (request is not null)
        {
            provider = BuildProviderConfiguration(
                request,
                tenantId.HasValue ? XeniaProviderScope.Tenant : XeniaProviderScope.Platform,
                tenantId,
                existing: null);

            credential = ResolveRequestCredential(provider, request);
        }
        else
        {
            throw new InvalidOperationException("A provider configuration identifier or request body is required.");
        }

        var result = credential is null
            ? new XeniaProviderValidationResult(false, "CredentialMissing", $"Provider '{provider.DisplayName}' does not have a usable credential.", DateTime.UtcNow, null)
            : providerGateway.Validate(provider, credential);

        provider.VerificationStatus = result.Success ? XeniaVerificationStatus.Verified : XeniaVerificationStatus.Failed;
        provider.LastVerifiedAtUtc = result.VerifiedAtUtc;
        provider.CredentialFingerprint ??= result.Fingerprint;
        provider.CredentialLastFour ??= credential?.LastFour;
        provider.HasStoredCredential = credential is not null;

        if (providerConfigurationId.HasValue)
            store.SaveProvider(provider);

        store.AppendProviderHealth(providerHealthCheck.CreateHealthEvent(provider, result));
        AppendAudit(tenantId ?? Guid.Empty, actorUserId, "xenia.provider.tested", $"Provider '{provider.DisplayName}' connectivity test executed.");

        return new XeniaProviderTestResponse(result.Success, result.Status, result.Message, result.VerifiedAtUtc, result.Fingerprint);
    }

    public IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels() =>
        store.ListModels()
            .OrderBy(model => model.Provider)
            .ThenBy(model => model.ModelCode)
            .ToList();

    public IReadOnlyList<XeniaPromptTemplateResponse> ListPromptTemplates() =>
        store.ListPromptTemplates()
            .OrderBy(item => item.DisplayName)
            .Select(item => new XeniaPromptTemplateResponse(
                item.PromptTemplateId,
                item.TemplateCode,
                item.DisplayName,
                item.Description,
                item.Enabled,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaPromptVersionResponse> ListPromptVersions(Guid? promptTemplateId = null) =>
        store.ListPromptVersions(promptTemplateId)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => new XeniaPromptVersionResponse(
                item.PromptVersionId,
                item.PromptTemplateId,
                item.VersionNumber,
                item.Content,
                item.ApprovalState,
                item.IsCurrent,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaSkillResponse> ListSkills() =>
        store.ListSkills()
            .OrderBy(item => item.DisplayName)
            .Select(item => new XeniaSkillResponse(
                item.SkillId,
                item.SkillCode,
                item.DisplayName,
                item.Description,
                item.Enabled,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaSkillVersionResponse> ListSkillVersions(Guid? skillId = null) =>
        store.ListSkillVersions(skillId)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => new XeniaSkillVersionResponse(
                item.SkillVersionId,
                item.SkillId,
                item.VersionNumber,
                item.DefinitionJson,
                item.ApprovalState,
                item.IsCurrent,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaAgentResponse> ListAgents() =>
        store.ListAgents()
            .OrderBy(item => item.DisplayName)
            .Select(item => new XeniaAgentResponse(
                item.AgentId,
                item.AgentCode,
                item.DisplayName,
                item.Description,
                item.Enabled,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaAgentVersionResponse> ListAgentVersions(Guid? agentId = null) =>
        store.ListAgentVersions(agentId)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => new XeniaAgentVersionResponse(
                item.AgentVersionId,
                item.AgentId,
                item.VersionNumber,
                item.DefinitionJson,
                item.ApprovalState,
                item.IsCurrent,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaKnowledgeSourceResponse> ListKnowledgeSources(Guid? tenantId = null) =>
        store.ListKnowledgeSources(tenantId)
            .OrderBy(item => item.DisplayName)
            .Select(item => new XeniaKnowledgeSourceResponse(
                item.KnowledgeSourceId,
                item.TenantId,
                item.SourceCode,
                item.DisplayName,
                item.SourceType,
                item.Status,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaMarketplaceAssetResponse> ListMarketplaceAssets() =>
        store.ListMarketplaceAssets()
            .OrderBy(item => item.DisplayName)
            .Select(item => new XeniaMarketplaceAssetResponse(
                item.MarketplaceAssetId,
                item.AssetCode,
                item.AssetType,
                item.DisplayName,
                item.Description,
                item.Enabled,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public IReadOnlyList<XeniaMarketplaceInstallationResponse> ListMarketplaceInstallations(Guid? tenantId = null) =>
        store.ListMarketplaceInstallations(tenantId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new XeniaMarketplaceInstallationResponse(
                item.MarketplaceInstallationId,
                item.MarketplaceAssetId,
                item.TenantId,
                item.Status,
                item.InstalledAtUtc,
                item.UpdatedAtUtc))
            .ToList();

    public XeniaUsageReportResponse GetUsage(Guid? tenantId = null)
    {
        var events = store.ListUsage()
            .Where(item => tenantId == null || item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        var summary = new XeniaUsageSummaryResponse(
            RequestCount: events.Count,
            PromptTokens: events.Sum(item => item.PromptTokens),
            CompletionTokens: events.Sum(item => item.CompletionTokens),
            EstimatedCostUsd: events.Sum(item => item.EstimatedCostUsd));

        return new XeniaUsageReportResponse(
            Summary: summary,
            Items: events.Select(MapUsage).ToList());
    }

    public IReadOnlyList<XeniaAuditEventResponse> GetAudit(Guid? tenantId = null) =>
        store.ListAudit()
            .Where(item => tenantId == null || item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(MapAudit)
            .ToList();

    public IReadOnlyList<XeniaProviderHealthResponse> GetProviderHealth() =>
        store.ListProviderHealth()
            .OrderByDescending(item => item.CheckedAtUtc)
            .GroupBy(item => item.ProviderConfigurationId)
            .Select(group => group.First())
            .OrderBy(item => item.ProviderName)
            .Select(item => new XeniaProviderHealthResponse(
                item.ProviderConfigurationId,
                item.ProviderName,
                item.Status,
                item.Message,
                item.CheckedAtUtc))
            .ToList();

    public XeniaTenantConfigurationResponse SaveTenantByoAiConfiguration(Guid tenantId, XeniaProviderConfigurationRequest request, string actorUserId)
    {
        var existingTenantProvider = store.ListProviders()
            .FirstOrDefault(provider =>
                provider.Scope == XeniaProviderScope.Tenant
                && provider.TenantId == tenantId
                && string.Equals(provider.DisplayName, request.DisplayName, StringComparison.OrdinalIgnoreCase));

        var provider = BuildProviderConfiguration(request, XeniaProviderScope.Tenant, tenantId, existingTenantProvider);
        PersistCredential(provider, request);
        var savedProvider = store.SaveProvider(provider);

        var configuration = store.FindTenantConfiguration(tenantId) ?? CreateTenantByoAiConfiguration(tenantId);
        configuration.Enabled = true;
        configuration.DeploymentModel = XeniaDeploymentModel.BringYourOwnAI;
        configuration.DefaultProviderConfigurationId = savedProvider.ProviderConfigurationId;
        configuration.DefaultModel = savedProvider.DefaultModel;
        configuration.UpdatedAtUtc = DateTime.UtcNow;
        store.SaveTenantConfiguration(configuration);

        AppendAudit(tenantId, actorUserId, "xenia.tenant.byoai.updated", $"Tenant BYOAI configuration updated for {tenantId}.");

        return MapTenantConfiguration(configuration, tenantId);
    }

    public XeniaConversationResponse CreateConversation(Guid tenantId, string userId, XeniaCreateConversationRequest request)
    {
        EnsureTenantEnabled(tenantId);

        var conversation = new XeniaConversation
        {
            ConversationId = Guid.CreateVersion7(),
            TenantId = tenantId,
            CreatedByUserId = userId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Xenia Conversation" : request.Title.Trim(),
            ActivationSource = string.IsNullOrWhiteSpace(request.ActivationSource) ? "UserClick" : request.ActivationSource.Trim(),
            ProductCode = NormalizeProductCode(request.ProductCode),
            SourceReference = request.SourceReference?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        store.SaveConversation(conversation);
        AppendAudit(tenantId, userId, "xenia.conversation.created", $"Conversation '{conversation.Title}' created.");

        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            _ = AddConversationMessage(
                tenantId,
                conversation.ConversationId,
                userId,
                new XeniaConversationMessageRequest(request.InitialMessage!, null, request.ProductCode));
        }

        return GetConversation(tenantId, conversation.ConversationId);
    }

    public IReadOnlyList<XeniaConversationResponse> ListConversations(Guid tenantId) =>
        store.ListConversations(tenantId)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc)
            .Select(MapConversation)
            .ToList();

    public XeniaConversationResponse GetConversation(Guid tenantId, Guid conversationId)
    {
        var conversation = store.GetConversation(tenantId, conversationId)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found for tenant '{tenantId}'.");

        return MapConversation(conversation);
    }

    public XeniaConversationTurnResponse AddConversationMessage(Guid tenantId, Guid conversationId, string userId, XeniaConversationMessageRequest request)
    {
        var conversation = store.GetConversation(tenantId, conversationId)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found for tenant '{tenantId}'.");

        var configuration = EnsureTenantEnabled(tenantId);
        var provider = ResolveProvider(configuration, tenantId);

        var userMessage = new XeniaConversationMessage
        {
            MessageId = Guid.CreateVersion7(),
            ConversationId = conversation.ConversationId,
            Role = XeniaMessageRole.User,
            Content = request.Content.Trim(),
            ActionLabel = request.ActionLabel?.Trim(),
            ProductCode = NormalizeProductCode(request.ProductCode) ?? conversation.ProductCode,
            CreatedAtUtc = DateTime.UtcNow,
        };
        conversation.Messages.Add(userMessage);

        var executionContext = new XeniaAiExecutionContext(
            tenantId,
            userId,
            NormalizeProductCode(request.ProductCode) ?? conversation.ProductCode ?? "XENIA",
            userMessage.Content,
            conversation.ActivationSource,
            "Conversation",
            null,
            request.ActionLabel,
            "Conversation",
            null,
            [conversation.SourceReference ?? $"conversation:{conversation.ConversationId}"]);

        var aiResponse = ExecuteWithFallback(provider, configuration, tenantId, executionContext);

        var assistantMessage = new XeniaConversationMessage
        {
            MessageId = Guid.CreateVersion7(),
            ConversationId = conversation.ConversationId,
            Role = XeniaMessageRole.Assistant,
            Content = aiResponse.Output,
            ProductCode = userMessage.ProductCode,
            CreatedAtUtc = DateTime.UtcNow,
        };
        conversation.Messages.Add(assistantMessage);
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        store.SaveConversation(conversation);

        var usage = usageNormalizer.CreateUsageEvent(tenantId, userId, "Conversation", aiResponse);
        store.AppendUsage(usage);
        AppendAudit(tenantId, userId, "xenia.conversation.message.created", $"Conversation '{conversationId}' received a new user message.");

        return new XeniaConversationTurnResponse(
            Conversation: MapConversation(conversation),
            UserMessage: MapMessage(userMessage),
            AssistantMessage: MapMessage(assistantMessage),
            OutputChunks: aiResponse.OutputChunks,
            Usage: new XeniaUsageSummaryResponse(1, usage.PromptTokens, usage.CompletionTokens, usage.EstimatedCostUsd));
    }

    public XeniaExecutionResponse ExecuteInternal(Guid tenantId, string userId, XeniaExecutionRequest request, string mode, string? skillCode = null, string? agentCode = null, string? toolCode = null)
    {
        var configuration = EnsureTenantEnabled(tenantId);
        var provider = ResolveProvider(configuration, tenantId);
        var productCode = NormalizeProductCode(request.ProductCode) ?? "XENIA";
        var activationSource = string.IsNullOrWhiteSpace(request.ActivationSource) ? "InternalService" : request.ActivationSource.Trim();

        var executionContext = new XeniaAiExecutionContext(
            tenantId,
            userId,
            productCode,
            request.Prompt,
            activationSource,
            mode,
            request.Context,
            request.ActionLabel,
            request.AuditClassification,
            request.ApplyOutcome,
            request.SourceObjectReferences);

        var aiResponse = ExecuteWithFallback(provider, configuration, tenantId, executionContext, skillCode, agentCode, toolCode);

        var usage = usageNormalizer.CreateUsageEvent(tenantId, userId, mode, aiResponse);
        store.AppendUsage(usage);
        AppendAudit(tenantId, userId, $"xenia.internal.{mode.ToLowerInvariant()}.executed", $"Internal {mode} execution completed for product '{productCode}'.");

        return new XeniaExecutionResponse(
            Mode: mode,
            TenantId: tenantId,
            ProductCode: productCode,
            Provider: aiResponse.Provider,
            Model: aiResponse.Model,
            Output: aiResponse.Output,
            OutputChunks: aiResponse.OutputChunks,
            Usage: new XeniaUsageSummaryResponse(1, usage.PromptTokens, usage.CompletionTokens, usage.EstimatedCostUsd),
            ActivationSource: activationSource,
            SkillCode: skillCode,
            AgentCode: agentCode,
            ToolCode: toolCode);
    }

    private XeniaAiResponse ExecuteWithFallback(
        XeniaProviderConfiguration provider,
        XeniaTenantConfiguration configuration,
        Guid tenantId,
        XeniaAiExecutionContext executionContext,
        string? skillCode = null,
        string? agentCode = null,
        string? toolCode = null)
    {
        try
        {
            return providerGateway.Execute(provider, executionContext);
        }
        catch when (configuration.FailoverEnabled)
        {
            try
            {
                var fallback = providerFailoverPolicy.SelectFallback(configuration, tenantId, store.ListProviders(), provider.ProviderConfigurationId);
                return providerGateway.Execute(fallback, executionContext);
            }
            catch
            {
                return BuildSyntheticResponse(provider, executionContext, skillCode, agentCode, toolCode);
            }
        }
        catch
        {
            return BuildSyntheticResponse(provider, executionContext, skillCode, agentCode, toolCode);
        }
    }

    private static string? NormalizeProductCode(string? productCode) =>
        string.IsNullOrWhiteSpace(productCode) ? null : productCode.Trim().ToUpperInvariant();

    private static List<string> NormalizeList(IReadOnlyList<string>? values) =>
        values?
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    private XeniaTenantConfiguration EnsureTenantEnabled(Guid tenantId)
    {
        var configuration = ResolveEffectiveConfiguration(tenantId);
        if (!configuration.Enabled)
            throw new InvalidOperationException($"Xenia is disabled for tenant '{tenantId}'.");
        return configuration;
    }

    private XeniaTenantConfiguration ResolveEffectiveConfiguration(Guid tenantId)
    {
        var tenantConfiguration = store.FindTenantConfiguration(tenantId);
        if (tenantConfiguration?.DeploymentModel == XeniaDeploymentModel.BringYourOwnAI)
            return tenantConfiguration;

        return GetOrCreateManagedConfiguration();
    }

    private XeniaTenantConfiguration GetOrCreateManagedConfiguration()
    {
        var configuration = store.FindTenantConfiguration(ManagedConfigurationTenantId);
        if (configuration is not null)
            return configuration;

        var created = CreateDefaultManagedConfiguration();
        return store.SaveTenantConfiguration(created);
    }

    private XeniaTenantConfiguration CreateDefaultManagedConfiguration()
    {
        var defaultProvider = store.ListProviders()
            .Where(item => item.Scope == XeniaProviderScope.Platform && item.Enabled)
            .OrderBy(item => item.FailoverPriority)
            .FirstOrDefault();

        return new XeniaTenantConfiguration
        {
            TenantId = ManagedConfigurationTenantId,
            DeploymentModel = XeniaDeploymentModel.Managed,
            DefaultProviderConfigurationId = defaultProvider?.ProviderConfigurationId,
            DefaultModel = defaultProvider?.DefaultModel ?? "gpt-4.1-mini",
            AllowedSkills = ["summary", "analysis", "drafting"],
            AllowedAgents = ["workspace-assistant"],
            AllowedTools = ["document-search", "timeline-builder"],
        };
    }

    private static XeniaTenantConfiguration CreateTenantByoAiConfiguration(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            DeploymentModel = XeniaDeploymentModel.BringYourOwnAI,
            AllowedSkills = ["summary", "analysis", "drafting"],
            AllowedAgents = ["workspace-assistant"],
            AllowedTools = ["document-search", "timeline-builder"],
        };

    private XeniaProviderConfiguration ResolveProvider(XeniaTenantConfiguration configuration, Guid tenantId) =>
        providerRoutingPolicy.Resolve(configuration, tenantId, store.ListProviders());

    private XeniaProviderConfiguration BuildProviderConfiguration(
        XeniaProviderConfigurationRequest request,
        XeniaProviderScope scope,
        Guid? tenantId,
        XeniaProviderConfiguration? existing)
    {
        var now = DateTime.UtcNow;
        var provider = existing ?? new XeniaProviderConfiguration
        {
            ProviderConfigurationId = Guid.CreateVersion7(),
            CreatedAtUtc = now,
        };

        provider.ProviderType = request.ProviderType;
        provider.Scope = scope;
        provider.TenantId = tenantId;
        provider.DisplayName = request.DisplayName.Trim();
        provider.Endpoint = request.Endpoint?.Trim();
        provider.Region = request.Region?.Trim();
        provider.AzureDeploymentName = request.AzureDeploymentName?.Trim();
        provider.DefaultModel = request.DefaultModel.Trim();
        provider.AllowedModels = NormalizeList(request.AllowedModels);
        provider.TimeoutSeconds = request.TimeoutSeconds <= 0 ? 60 : request.TimeoutSeconds;
        provider.RetryCount = request.RetryCount < 0 ? 0 : request.RetryCount;
        provider.FailoverPriority = request.FailoverPriority;
        provider.Enabled = request.Enabled;
        provider.CredentialStorageMode = request.CredentialStorageMode;
        provider.SecretReference = request.ExternalSecretReference?.Trim();
        provider.UpdatedAtUtc = now;

        return provider;
    }

    private void PersistCredential(XeniaProviderConfiguration provider, XeniaProviderConfigurationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey) && string.IsNullOrWhiteSpace(request.ExternalSecretReference))
        {
            var metadata = credentialStore.GetActiveMetadata(provider.ProviderConfigurationId);
            if (metadata is not null)
            {
                provider.CredentialFingerprint = metadata.Fingerprint;
                provider.CredentialLastFour = metadata.LastFour;
                provider.HasStoredCredential = metadata.IsActive;
                provider.VerificationStatus = metadata.VerificationStatus;
                provider.LastVerifiedAtUtc = metadata.LastVerifiedAtUtc;
            }
            return;
        }

        var credential = credentialStore.Save(
            provider.ProviderConfigurationId,
            request.CredentialStorageMode,
            request.ApiKey,
            request.ExternalSecretReference);

        provider.CredentialFingerprint = credential.Fingerprint;
        provider.CredentialLastFour = credential.LastFour;
        provider.HasStoredCredential = true;
        provider.VerificationStatus = credential.VerificationStatus;
        provider.LastVerifiedAtUtc = credential.LastVerifiedAtUtc;
    }

    private static XeniaResolvedCredential? ResolveRequestCredential(XeniaProviderConfiguration provider, XeniaProviderConfigurationRequest request)
    {
        var secret = request.CredentialStorageMode == XeniaCredentialStorageMode.ExternalSecretReference
            ? request.ExternalSecretReference?.Trim()
            : request.ApiKey?.Trim();

        if (string.IsNullOrWhiteSpace(secret))
            return null;

        return new XeniaResolvedCredential(
            request.CredentialStorageMode,
            secret,
            BuildFingerprint(secret),
            secret.Length <= 4 ? secret : secret[^4..]);
    }

    private void AppendAudit(Guid tenantId, string actorUserId, string eventType, string description)
    {
        store.AppendAudit(new XeniaAuditEvent
        {
            AuditEventId = Guid.CreateVersion7(),
            TenantId = tenantId,
            EventType = eventType,
            ActorUserId = actorUserId,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static XeniaAiResponse BuildSyntheticResponse(
        XeniaProviderConfiguration provider,
        XeniaAiExecutionContext executionContext,
        string? skillCode,
        string? agentCode,
        string? toolCode)
    {
        var output = executionContext.RequestKind.Equals("Conversation", StringComparison.OrdinalIgnoreCase)
            ? BuildAssistantResponse(executionContext.Prompt, provider.DisplayName, provider.DefaultModel, executionContext.ProductCode, executionContext.ActivationSource)
            : BuildInternalResponse(executionContext.Prompt, executionContext.RequestKind, executionContext.ProductCode, provider.DisplayName, provider.DefaultModel, skillCode, agentCode, toolCode);

        var promptTokens = EstimateTokens(executionContext.Prompt);
        var completionTokens = EstimateTokens(output);

        return new XeniaAiResponse(
            provider.DisplayName,
            provider.DefaultModel,
            output,
            Chunk(output),
            promptTokens,
            completionTokens,
            Math.Round((promptTokens + completionTokens) * 0.00001m, 6),
            0,
            false,
            "synthetic_fallback");
    }

    private static IReadOnlyList<string> Chunk(string value)
    {
        const int chunkSize = 180;
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var chunks = new List<string>();
        for (var index = 0; index < value.Length; index += chunkSize)
            chunks.Add(value.Substring(index, Math.Min(chunkSize, value.Length - index)));
        return chunks;
    }

    private static int EstimateTokens(string value) =>
        Math.Max(1, value.Trim().Length / 4);

    private static string BuildFingerprint(string rawValue)
    {
        var trimmed = rawValue.Trim();
        var suffix = trimmed.Length <= 4 ? trimmed : trimmed[^4..];
        return $"fp-{suffix}";
    }

    private static string BuildAssistantResponse(string prompt, string providerName, string model, string? productCode, string activationSource)
    {
        var productContext = string.IsNullOrWhiteSpace(productCode) ? "platform" : productCode;
        return $"Xenia reviewed this {productContext} request through {providerName} using {model}. " +
               $"Activation source: {activationSource}. Review the draft before applying any business changes.\n\n" +
               $"Suggested response:\n{prompt.Trim()}";
    }

    private static string BuildInternalResponse(
        string prompt,
        string mode,
        string productCode,
        string providerName,
        string model,
        string? skillCode,
        string? agentCode,
        string? toolCode)
    {
        var descriptor = skillCode is not null
            ? $"skill '{skillCode}'"
            : agentCode is not null
                ? $"agent '{agentCode}'"
                : toolCode is not null
                    ? $"tool '{toolCode}'"
                    : "completion";

        return $"Xenia {mode.ToLowerInvariant()} executed {descriptor} for product '{productCode}' via {providerName} ({model}).\n\nPrompt:\n{prompt.Trim()}";
    }

    private static XeniaTenantConfigurationResponse MapTenantConfiguration(XeniaTenantConfiguration configuration, Guid tenantId) =>
        new(
            tenantId,
            configuration.Enabled,
            configuration.DeploymentModel,
            configuration.DefaultProviderConfigurationId,
            configuration.DefaultModel,
            configuration.Temperature,
            configuration.MaxTokens,
            configuration.ReasoningLevel,
            configuration.RetentionPolicy,
            configuration.ModerationPolicy,
            configuration.FailoverEnabled,
            configuration.AllowedSkills,
            configuration.AllowedAgents,
            configuration.AllowedTools,
            configuration.CreatedAtUtc,
            configuration.UpdatedAtUtc);

    private static XeniaProviderConfigurationResponse MapProvider(XeniaProviderConfiguration provider) =>
        new(
            provider.ProviderConfigurationId,
            provider.ProviderType,
            provider.Scope,
            provider.TenantId,
            provider.DisplayName,
            provider.Endpoint,
            provider.Region,
            provider.AzureDeploymentName,
            provider.DefaultModel,
            provider.AllowedModels,
            provider.TimeoutSeconds,
            provider.RetryCount,
            provider.FailoverPriority,
            provider.Enabled,
            provider.VerificationStatus,
            provider.LastVerifiedAtUtc,
            provider.CredentialStorageMode,
            provider.SecretReference,
            provider.CredentialFingerprint,
            provider.CredentialLastFour,
            provider.HasStoredCredential,
            provider.CreatedAtUtc,
            provider.UpdatedAtUtc);

    private static XeniaUsageEventResponse MapUsage(XeniaUsageEvent usageEvent) =>
        new(
            usageEvent.UsageEventId,
            usageEvent.TenantId,
            usageEvent.UserId,
            usageEvent.EventKind,
            usageEvent.Provider,
            usageEvent.Model,
            usageEvent.PromptTokens,
            usageEvent.CompletionTokens,
            usageEvent.EstimatedCostUsd,
            usageEvent.CreatedAtUtc);

    private static XeniaAuditEventResponse MapAudit(XeniaAuditEvent auditEvent) =>
        new(
            auditEvent.AuditEventId,
            auditEvent.TenantId,
            auditEvent.EventType,
            auditEvent.ActorUserId,
            auditEvent.Description,
            auditEvent.CreatedAtUtc);

    private static XeniaConversationResponse MapConversation(XeniaConversation conversation) =>
        new(
            conversation.ConversationId,
            conversation.TenantId,
            conversation.CreatedByUserId,
            conversation.Title,
            conversation.ActivationSource,
            conversation.ProductCode,
            conversation.SourceReference,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            conversation.Messages.OrderBy(item => item.CreatedAtUtc).Select(MapMessage).ToList());

    private static XeniaConversationMessageResponse MapMessage(XeniaConversationMessage message) =>
        new(
            message.MessageId,
            message.Role,
            message.Content,
            message.ActionLabel,
            message.ProductCode,
            message.CreatedAtUtc);
}
