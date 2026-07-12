'use client';

import { useRouter } from 'next/navigation';
import { useState, useTransition } from 'react';
import {
  createXeniaPlatformProvider,
  loadXeniaManagedConfiguration,
  saveXeniaManagedConfiguration,
  testXeniaPlatformProvider,
  updateXeniaPlatformProvider,
  type XeniaProviderConfigurationInput,
  type XeniaTenantConfigurationInput,
} from './actions';

type XeniaProviderType = 'OpenAI' | 'Anthropic' | 'Gemini' | 'AzureOpenAI' | 'AwsBedrock';

interface OverviewResponse {
  enabledTenantCount: number;
  deploymentModelDistribution: Record<string, number>;
  providerCount: number;
  conversationCount: number;
  usage: {
    requestCount: number;
    promptTokens: number;
    completionTokens: number;
    estimatedCostUsd: number;
  };
  providerHealth: Array<{
    providerConfigurationId: string;
    providerName: string;
    status: string;
    message: string;
    checkedAtUtc: string;
  }>;
}

interface ProviderResponse {
  providerConfigurationId: string;
  providerType: XeniaProviderType;
  scope: 'Platform' | 'Tenant';
  tenantId: string | null;
  displayName: string;
  endpoint: string | null;
  region: string | null;
  azureDeploymentName: string | null;
  defaultModel: string;
  allowedModels: string[];
  timeoutSeconds: number;
  retryCount: number;
  failoverPriority: number;
  enabled: boolean;
  verificationStatus: 'Unverified' | 'Verified' | 'Failed';
  lastVerifiedAtUtc: string | null;
  credentialStorageMode: 'EncryptedDatabase' | 'ExternalSecretReference';
  secretReference: string | null;
  credentialFingerprint: string | null;
  credentialLastFour: string | null;
  hasStoredCredential: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface ModelEntry {
  provider: string;
  modelCode: string;
  displayName: string;
  supportsStreaming: boolean;
  supportsEmbeddings: boolean;
  enabled: boolean;
}

interface TenantConfigurationResponse {
  tenantId: string;
  enabled: boolean;
  deploymentModel: 'Managed' | 'BringYourOwnAI';
  defaultProviderConfigurationId: string | null;
  defaultModel: string;
  temperature: number;
  maxTokens: number;
  reasoningLevel: string;
  retentionPolicy: string;
  moderationPolicy: string;
  failoverEnabled: boolean;
  allowedSkills: string[];
  allowedAgents: string[];
  allowedTools: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface UsageReport {
  summary: {
    requestCount: number;
    promptTokens: number;
    completionTokens: number;
    estimatedCostUsd: number;
  };
  items: Array<{
    usageEventId: string;
    tenantId: string;
    userId: string;
    eventKind: string;
    provider: string;
    model: string;
    promptTokens: number;
    completionTokens: number;
    estimatedCostUsd: number;
    createdAtUtc: string;
  }>;
}

interface AuditEvent {
  auditEventId: string;
  tenantId: string;
  eventType: string;
  actorUserId: string;
  description: string;
  createdAtUtc: string;
}

interface PromptTemplateResponse {
  promptTemplateId: string;
  templateCode: string;
  displayName: string;
  description: string;
  enabled: boolean;
}

interface SkillResponse {
  skillId: string;
  skillCode: string;
  displayName: string;
  description: string;
  enabled: boolean;
}

interface AgentResponse {
  agentId: string;
  agentCode: string;
  displayName: string;
  description: string;
  enabled: boolean;
}

interface KnowledgeSourceResponse {
  knowledgeSourceId: string;
  tenantId: string | null;
  sourceCode: string;
  displayName: string;
  sourceType: string;
  status: string;
}

interface MarketplaceAssetResponse {
  marketplaceAssetId: string;
  assetCode: string;
  assetType: string;
  displayName: string;
  description: string;
  enabled: boolean;
}

const EMPTY_PROVIDER_FORM: XeniaProviderConfigurationInput = {
  providerType: 'OpenAI',
  displayName: '',
  endpoint: '',
  region: '',
  azureDeploymentName: '',
  defaultModel: 'gpt-4.1-mini',
  allowedModels: ['gpt-4.1-mini'],
  timeoutSeconds: 60,
  retryCount: 2,
  failoverPriority: 100,
  enabled: true,
  apiKey: '',
};

const EMPTY_TENANT_CONFIGURATION_FORM: XeniaTenantConfigurationInput & {
  allowedSkillsCsv: string;
  allowedAgentsCsv: string;
  allowedToolsCsv: string;
} = {
  enabled: true,
  deploymentModel: 'Managed',
  defaultProviderConfigurationId: null,
  defaultModel: 'gpt-4.1-mini',
  temperature: 0.2,
  maxTokens: 2000,
  reasoningLevel: 'Standard',
  retentionPolicy: 'TenantDefault',
  moderationPolicy: 'Standard',
  failoverEnabled: true,
  allowedSkills: [],
  allowedAgents: [],
  allowedTools: [],
  allowedSkillsCsv: 'summary, analysis, drafting',
  allowedAgentsCsv: 'workspace-assistant',
  allowedToolsCsv: 'document-search, timeline-builder',
};

function formatTimestamp(value: string | null) {
  if (!value) return 'Never';
  try {
    return new Date(value).toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return value;
  }
}

function toAllowedModelsCsv(models: string[]) {
  return models.join(', ');
}

function parseAllowedModelsCsv(value: string) {
  return value
    .split(',')
    .map(item => item.trim())
    .filter(Boolean);
}

function parseCsv(value: string) {
  return value
    .split(',')
    .map(item => item.trim())
    .filter(Boolean);
}

const cardClassName = 'rounded-lg border border-gray-200 bg-white';
const mutedPanelClassName = 'rounded-lg border border-gray-200 bg-gray-50';
const emptyStateClassName = 'rounded-lg border border-dashed border-gray-200 bg-gray-50 px-4 py-8 text-center text-sm text-gray-500';
const primaryButtonClassName = 'rounded-lg bg-gray-900 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-gray-800 disabled:cursor-not-allowed disabled:bg-gray-300';
const secondaryButtonClassName = 'rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 transition hover:border-gray-300 hover:bg-gray-50';
const messageErrorClassName = 'rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700';
const messageSuccessClassName = 'rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700';

export function XeniaAdminClient({
  initialOverview,
  initialProviders,
  initialModels,
  initialUsage,
  initialAudit,
  initialPrompts,
  initialSkills,
  initialAgents,
  initialKnowledgeSources,
  initialMarketplaceAssets,
}: {
  initialOverview: OverviewResponse;
  initialProviders: ProviderResponse[];
  initialModels: ModelEntry[];
  initialUsage: UsageReport;
  initialAudit: AuditEvent[];
  initialPrompts: PromptTemplateResponse[];
  initialSkills: SkillResponse[];
  initialAgents: AgentResponse[];
  initialKnowledgeSources: KnowledgeSourceResponse[];
  initialMarketplaceAssets: MarketplaceAssetResponse[];
}) {
  const router = useRouter();
  const [overview] = useState(initialOverview);
  const [providers, setProviders] = useState(initialProviders);
  const [models] = useState(initialModels);
  const [usage] = useState(initialUsage);
  const [audit] = useState(initialAudit);
  const [prompts] = useState(initialPrompts);
  const [skills] = useState(initialSkills);
  const [agents] = useState(initialAgents);
  const [knowledgeSources] = useState(initialKnowledgeSources);
  const [marketplaceAssets] = useState(initialMarketplaceAssets);
  const [selectedProviderId, setSelectedProviderId] = useState<string | 'new'>('new');
  const [providerForm, setProviderForm] = useState<{ allowedModelsCsv: string } & XeniaProviderConfigurationInput>({
    ...EMPTY_PROVIDER_FORM,
    allowedModelsCsv: 'gpt-4.1-mini',
  });
  const [tenantConfigurationForm, setTenantConfigurationForm] = useState(EMPTY_TENANT_CONFIGURATION_FORM);
  const [loadedTenantConfiguration, setLoadedTenantConfiguration] = useState<TenantConfigurationResponse | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [tenantActionMessage, setTenantActionMessage] = useState<string | null>(null);
  const [tenantActionError, setTenantActionError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const platformProviders = providers.filter(provider => provider.scope === 'Platform');
  const tenantProviders = providers.filter(provider => provider.scope === 'Tenant');

  const selectedProvider = selectedProviderId === 'new'
    ? null
    : providers.find(provider => provider.providerConfigurationId === selectedProviderId) ?? null;

  const resetForm = () => {
    setSelectedProviderId('new');
    setProviderForm({
      ...EMPTY_PROVIDER_FORM,
      allowedModelsCsv: 'gpt-4.1-mini',
    });
    setActionMessage(null);
    setActionError(null);
  };

  const resetTenantConfigurationForm = () => {
    setLoadedTenantConfiguration(null);
    setTenantConfigurationForm(EMPTY_TENANT_CONFIGURATION_FORM);
    setTenantActionMessage(null);
    setTenantActionError(null);
  };

  const loadProviderIntoForm = (providerId: string | 'new') => {
    setSelectedProviderId(providerId);
    setActionMessage(null);
    setActionError(null);

    if (providerId === 'new') {
      resetForm();
      return;
    }

    const provider = providers.find(item => item.providerConfigurationId === providerId);
    if (!provider) return;

    setProviderForm({
      providerType: provider.providerType,
      displayName: provider.displayName,
      endpoint: provider.endpoint ?? '',
      region: provider.region ?? '',
      azureDeploymentName: provider.azureDeploymentName ?? '',
      defaultModel: provider.defaultModel,
      allowedModels: provider.allowedModels,
      allowedModelsCsv: toAllowedModelsCsv(provider.allowedModels),
      timeoutSeconds: provider.timeoutSeconds,
      retryCount: provider.retryCount,
      failoverPriority: provider.failoverPriority,
      enabled: provider.enabled,
      apiKey: '',
      credentialStorageMode: provider.credentialStorageMode,
      externalSecretReference: provider.secretReference ?? '',
    });
  };

  const submitPayload: XeniaProviderConfigurationInput = {
    providerType: providerForm.providerType,
    displayName: providerForm.displayName.trim(),
    endpoint: providerForm.endpoint?.trim() || null,
    region: providerForm.region?.trim() || null,
    azureDeploymentName: providerForm.azureDeploymentName?.trim() || null,
    defaultModel: providerForm.defaultModel.trim(),
    allowedModels: parseAllowedModelsCsv(providerForm.allowedModelsCsv),
    timeoutSeconds: Number(providerForm.timeoutSeconds),
    retryCount: Number(providerForm.retryCount),
    failoverPriority: Number(providerForm.failoverPriority),
    enabled: providerForm.enabled,
    apiKey: providerForm.apiKey?.trim() || null,
    credentialStorageMode: providerForm.credentialStorageMode ?? 'EncryptedDatabase',
    externalSecretReference: providerForm.externalSecretReference?.trim() || null,
  };

  const tenantConfigurationPayload: XeniaTenantConfigurationInput = {
    enabled: tenantConfigurationForm.enabled,
    deploymentModel: tenantConfigurationForm.deploymentModel,
    defaultProviderConfigurationId: tenantConfigurationForm.defaultProviderConfigurationId || null,
    defaultModel: tenantConfigurationForm.defaultModel.trim(),
    temperature: Number(tenantConfigurationForm.temperature),
    maxTokens: Number(tenantConfigurationForm.maxTokens),
    reasoningLevel: tenantConfigurationForm.reasoningLevel.trim(),
    retentionPolicy: tenantConfigurationForm.retentionPolicy.trim(),
    moderationPolicy: tenantConfigurationForm.moderationPolicy.trim(),
    failoverEnabled: tenantConfigurationForm.failoverEnabled,
    allowedSkills: parseCsv(tenantConfigurationForm.allowedSkillsCsv),
    allowedAgents: parseCsv(tenantConfigurationForm.allowedAgentsCsv),
    allowedTools: parseCsv(tenantConfigurationForm.allowedToolsCsv),
  };

  const validateProviderForm = () => {
    if (!submitPayload.displayName.trim()) return 'Display name is required.';
    if (!submitPayload.defaultModel.trim()) return 'Default model is required.';
    if (submitPayload.allowedModels.length === 0) return 'Enter at least one allowed model.';
    return null;
  };

  const validateTenantConfigurationForm = () => {
    if (!tenantConfigurationPayload.defaultModel) return 'Default model is required.';
    if (!tenantConfigurationPayload.defaultProviderConfigurationId) return 'Managed mode requires a default platform provider.';
    return null;
  };

  const upsertProvider = () => {
    const validationError = validateProviderForm();
    if (validationError) {
      setActionError(validationError);
      return;
    }

    setActionError(null);
    setActionMessage(null);

    startTransition(async () => {
      const result = selectedProvider
        ? await updateXeniaPlatformProvider(selectedProvider.providerConfigurationId, submitPayload)
        : await createXeniaPlatformProvider(submitPayload);

      if (!result.success) {
        setActionError(result.error ?? 'Unable to save the Xenia provider.');
        return;
      }

      const saved = result.data as unknown as ProviderResponse;
      setProviders(current => {
        const next = current.filter(item => item.providerConfigurationId !== saved.providerConfigurationId);
        return [...next, saved].sort((left, right) => left.failoverPriority - right.failoverPriority);
      });
      setActionMessage(selectedProvider ? 'Platform provider updated.' : 'Platform provider created.');
      loadProviderIntoForm(saved.providerConfigurationId);
      router.refresh();
    });
  };

  const handleTestProvider = (providerId: string) => {
    setActionError(null);
    setActionMessage(null);

    startTransition(async () => {
      const result = await testXeniaPlatformProvider(providerId);
      if (!result.success) {
        setActionError(result.error ?? 'Unable to test the Xenia provider.');
        return;
      }

      const response = result.data as unknown as { message: string };
      setActionMessage(response.message);
      router.refresh();
    });
  };

  const handleLoadTenantConfiguration = () => {
    setTenantActionError(null);
    setTenantActionMessage(null);

    startTransition(async () => {
      const result = await loadXeniaManagedConfiguration();
      if (!result.success) {
        setTenantActionError(result.error ?? 'Unable to load the managed AI configuration.');
        return;
      }

      const config = result.data as TenantConfigurationResponse;
      setLoadedTenantConfiguration(config);
      setTenantConfigurationForm({
        enabled: config.enabled,
        deploymentModel: config.deploymentModel,
        defaultProviderConfigurationId: config.defaultProviderConfigurationId,
        defaultModel: config.defaultModel,
        temperature: config.temperature,
        maxTokens: config.maxTokens,
        reasoningLevel: config.reasoningLevel,
        retentionPolicy: config.retentionPolicy,
        moderationPolicy: config.moderationPolicy,
        failoverEnabled: config.failoverEnabled,
        allowedSkills: config.allowedSkills,
        allowedAgents: config.allowedAgents,
        allowedTools: config.allowedTools,
        allowedSkillsCsv: toAllowedModelsCsv(config.allowedSkills),
        allowedAgentsCsv: toAllowedModelsCsv(config.allowedAgents),
        allowedToolsCsv: toAllowedModelsCsv(config.allowedTools),
      });
      setTenantActionMessage('Loaded the platform managed AI configuration.');
    });
  };

  const handleSaveTenantConfiguration = () => {
    const validationError = validateTenantConfigurationForm();
    if (validationError) {
      setTenantActionError(validationError);
      return;
    }

    setTenantActionError(null);
    setTenantActionMessage(null);

    startTransition(async () => {
      const result = await saveXeniaManagedConfiguration(tenantConfigurationPayload);
      if (!result.success) {
        setTenantActionError(result.error ?? 'Unable to save the managed AI configuration.');
        return;
      }

      const config = result.data as TenantConfigurationResponse;
      setLoadedTenantConfiguration(config);
      setTenantActionMessage('Saved the platform managed AI configuration.');
      router.refresh();
    });
  };

  return (
    <main className="space-y-6">
      <section>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Xenia</h1>
              <span className="inline-flex items-center rounded-full bg-amber-100 px-2.5 py-1 text-[11px] font-semibold text-amber-700">
                IN PROGRESS
              </span>
            </div>
            <p className="mt-1 max-w-3xl text-sm text-gray-500">
              Manage the shared managed AI posture, platform providers, model availability, usage, audit history, and current provider health for Xenia.
            </p>
          </div>
          <div className="flex items-center gap-4 text-xs text-gray-500">
            <span className="font-medium text-gray-700 tabular-nums">{platformProviders.length} platform providers</span>
            <span className="text-gray-300">|</span>
            <span>{models.length} catalog models</span>
            <span className="text-gray-300">|</span>
            <span>{usage.items.length} usage events</span>
          </div>
        </div>
      </section>

      <section className="grid gap-4 lg:grid-cols-4">
        <MetricCard label="Enabled Tenants" value={String(overview.enabledTenantCount)} />
        <MetricCard label="Platform Providers" value={String(platformProviders.length)} />
        <MetricCard label="Conversations" value={String(overview.conversationCount)} />
        <MetricCard label="Estimated Cost" value={`$${usage.summary.estimatedCostUsd.toFixed(4)}`} />
      </section>

      <section className="grid gap-6 xl:grid-cols-[0.85fr_1.15fr]">
        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Deployment mix</h2>
          <div className="mt-4 space-y-3">
            {Object.entries(overview.deploymentModelDistribution).map(([model, count]) => (
              <div key={model} className={`${mutedPanelClassName} flex items-center justify-between px-4 py-3`}>
                <span className="text-sm font-medium text-gray-700">{model}</span>
                <span className="text-sm font-semibold text-gray-900">{count}</span>
              </div>
            ))}
          </div>
        </div>

        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Usage totals</h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-3">
            <StatPill label="Requests" value={String(usage.summary.requestCount)} />
            <StatPill label="Prompt Tokens" value={String(usage.summary.promptTokens)} />
            <StatPill label="Completion Tokens" value={String(usage.summary.completionTokens)} />
          </div>
          <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            Provider validation updates are refreshed from the server after create, update, or test actions.
          </div>
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
        <div className={`${cardClassName} p-6`}>
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-base font-semibold text-gray-900">Managed AI configuration</h2>
              <p className="mt-1 text-sm text-gray-500">Platform admins configure one shared managed Xenia posture here. Tenant-specific overrides are reserved for BYOAI.</p>
              <p className="mt-2 text-xs font-medium text-amber-700">
                API keys are configured on platform providers below. This section only chooses which managed provider and defaults Xenia should use.
              </p>
            </div>
            <button
              type="button"
              onClick={resetTenantConfigurationForm}
              className={secondaryButtonClassName}
            >
              Reset managed form
            </button>
          </div>

          {tenantActionError ? (
            <div className={`mt-4 ${messageErrorClassName}`}>
              {tenantActionError}
            </div>
          ) : null}
          {tenantActionMessage ? (
            <div className={`mt-4 ${messageSuccessClassName}`}>
              {tenantActionMessage}
            </div>
          ) : null}

          <div className="mt-5 grid gap-4">
            <div className="flex justify-end">
              <button
                type="button"
                disabled={isPending}
                onClick={handleLoadTenantConfiguration}
                className={primaryButtonClassName}
              >
                Load managed configuration
              </button>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Deployment Model">
                <select
                  value={tenantConfigurationForm.deploymentModel}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, deploymentModel: event.target.value as 'Managed' | 'BringYourOwnAI' }))}
                  className={inputClassName}
                  disabled
                >
                  <option value="Managed">Managed</option>
                </select>
              </Field>
              <Field label="Default Platform Provider">
                <select
                  value={tenantConfigurationForm.defaultProviderConfigurationId ?? ''}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, defaultProviderConfigurationId: event.target.value || null }))}
                  className={inputClassName}
                >
                  <option value="">Select provider</option>
                  {platformProviders.map((provider) => (
                    <option key={provider.providerConfigurationId} value={provider.providerConfigurationId}>
                      {provider.displayName} · {provider.defaultModel}
                    </option>
                  ))}
                </select>
              </Field>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Default Model">
                <input
                  value={tenantConfigurationForm.defaultModel}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, defaultModel: event.target.value }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Temperature">
                <input
                  type="number"
                  step="0.1"
                  min={0}
                  max={2}
                  value={tenantConfigurationForm.temperature}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, temperature: Number(event.target.value) }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Max Tokens">
                <input
                  type="number"
                  min={1}
                  value={tenantConfigurationForm.maxTokens}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, maxTokens: Number(event.target.value) }))}
                  className={inputClassName}
                />
              </Field>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Reasoning Level">
                <input
                  value={tenantConfigurationForm.reasoningLevel}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, reasoningLevel: event.target.value }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Retention Policy">
                <input
                  value={tenantConfigurationForm.retentionPolicy}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, retentionPolicy: event.target.value }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Moderation Policy">
                <input
                  value={tenantConfigurationForm.moderationPolicy}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, moderationPolicy: event.target.value }))}
                  className={inputClassName}
                />
              </Field>
            </div>

            <Field label="Allowed Skills">
              <input
                value={tenantConfigurationForm.allowedSkillsCsv}
                onChange={(event) => setTenantConfigurationForm(current => ({ ...current, allowedSkillsCsv: event.target.value }))}
                className={inputClassName}
              />
            </Field>
            <Field label="Allowed Agents">
              <input
                value={tenantConfigurationForm.allowedAgentsCsv}
                onChange={(event) => setTenantConfigurationForm(current => ({ ...current, allowedAgentsCsv: event.target.value }))}
                className={inputClassName}
              />
            </Field>
            <Field label="Allowed Tools">
              <input
                value={tenantConfigurationForm.allowedToolsCsv}
                onChange={(event) => setTenantConfigurationForm(current => ({ ...current, allowedToolsCsv: event.target.value }))}
                className={inputClassName}
              />
            </Field>

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="inline-flex items-center gap-3 rounded-lg border border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-700">
                <input
                  type="checkbox"
                  checked={tenantConfigurationForm.enabled}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, enabled: event.target.checked }))}
                  className="h-4 w-4 rounded border-gray-300 text-gray-900 focus:ring-gray-500"
                />
                Managed AI enabled
              </label>
              <label className="inline-flex items-center gap-3 rounded-lg border border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-700">
                <input
                  type="checkbox"
                  checked={tenantConfigurationForm.failoverEnabled}
                  onChange={(event) => setTenantConfigurationForm(current => ({ ...current, failoverEnabled: event.target.checked }))}
                  className="h-4 w-4 rounded border-gray-300 text-gray-900 focus:ring-gray-500"
                />
                Managed failover enabled
              </label>
            </div>

            <div className="flex justify-end">
              <button
                type="button"
                disabled={isPending}
                onClick={handleSaveTenantConfiguration}
                className={primaryButtonClassName}
              >
                Save managed configuration
              </button>
            </div>
          </div>
        </div>

        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Loaded managed summary</h2>
          {loadedTenantConfiguration ? (
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              {[
                ['Scope', 'Platform managed'],
                ['Deployment', loadedTenantConfiguration.deploymentModel],
                ['Default Provider', loadedTenantConfiguration.defaultProviderConfigurationId ?? 'Unassigned'],
                ['Default Model', loadedTenantConfiguration.defaultModel],
                ['Reasoning', loadedTenantConfiguration.reasoningLevel],
                ['Retention', loadedTenantConfiguration.retentionPolicy],
                ['Moderation', loadedTenantConfiguration.moderationPolicy],
                ['Failover', loadedTenantConfiguration.failoverEnabled ? 'Enabled' : 'Disabled'],
                ['Enabled', loadedTenantConfiguration.enabled ? 'Yes' : 'No'],
                ['Updated', formatTimestamp(loadedTenantConfiguration.updatedAtUtc)],
              ].map(([label, value]) => (
                <div key={label} className={`${mutedPanelClassName} px-4 py-3`}>
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-gray-500">{label}</p>
                  <p className="mt-2 text-sm font-medium leading-6 text-gray-900">{value}</p>
                </div>
              ))}
            </div>
          ) : (
            <div className={`mt-4 ${emptyStateClassName}`}>
              Load the shared managed configuration to review or update Xenia defaults.
            </div>
          )}
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
        <div className={`${cardClassName} p-6`}>
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-base font-semibold text-gray-900">Platform providers</h2>
              <p className="mt-1 text-sm text-gray-500">Create, update, and validate Xenia’s platform-scoped AI providers.</p>
            </div>
            <button
              type="button"
              onClick={resetForm}
              className={secondaryButtonClassName}
            >
              New provider
            </button>
          </div>

          <div className="mt-5 space-y-3">
            {platformProviders.length === 0 ? (
              <div className={emptyStateClassName}>
                No platform providers configured yet.
              </div>
            ) : null}

            {platformProviders.map((provider) => (
              <div key={provider.providerConfigurationId} className={`${mutedPanelClassName} px-4 py-4`}>
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-semibold text-gray-900">{provider.displayName}</p>
                      <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                        provider.enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-200 text-gray-700'
                      }`}>
                        {provider.enabled ? 'Enabled' : 'Disabled'}
                      </span>
                      <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                        provider.verificationStatus === 'Verified'
                          ? 'bg-blue-50 text-blue-700'
                          : provider.verificationStatus === 'Failed'
                            ? 'bg-red-50 text-red-700'
                            : 'bg-gray-100 text-gray-600'
                      }`}>
                        {provider.verificationStatus}
                      </span>
                    </div>
                    <p className="mt-2 text-sm text-gray-600">
                      {provider.providerType} · {provider.defaultModel} · priority {provider.failoverPriority}
                    </p>
                    <p className="mt-1 text-xs text-gray-500">
                      Credential {provider.credentialStorageMode} · {provider.credentialFingerprint ?? 'No fingerprint'} · {provider.secretReference ?? provider.credentialLastFour ?? 'No reference'}
                    </p>
                    <p className="mt-1 text-xs text-gray-400">
                      Last verified {formatTimestamp(provider.lastVerifiedAtUtc)}
                    </p>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      onClick={() => loadProviderIntoForm(provider.providerConfigurationId)}
                      className={secondaryButtonClassName}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      disabled={isPending}
                      onClick={() => handleTestProvider(provider.providerConfigurationId)}
                      className={primaryButtonClassName}
                    >
                      Test
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">
            {selectedProvider ? `Edit ${selectedProvider.displayName}` : 'Create platform provider'}
          </h2>
          <p className="mt-1 text-sm text-gray-500">
            This form writes directly to the Xenia admin provider endpoints exposed by the platform service.
          </p>
          <p className="mt-2 text-xs font-medium text-amber-700">
            Enter the managed provider API key here, then assign that provider in the managed AI configuration section above.
          </p>

          {actionError ? (
            <div className={`mt-4 ${messageErrorClassName}`}>
              {actionError}
            </div>
          ) : null}
          {actionMessage ? (
            <div className={`mt-4 ${messageSuccessClassName}`}>
              {actionMessage}
            </div>
          ) : null}

          <div className="mt-5 grid gap-4">
            <Field label="Provider Type">
              <select
                value={providerForm.providerType}
                onChange={(event) => setProviderForm(current => ({ ...current, providerType: event.target.value as XeniaProviderType }))}
                className={inputClassName}
              >
                {['OpenAI', 'Anthropic', 'Gemini', 'AzureOpenAI', 'AwsBedrock'].map((value) => (
                  <option key={value} value={value}>{value}</option>
                ))}
              </select>
            </Field>

            <Field label="Display Name">
              <input
                value={providerForm.displayName}
                onChange={(event) => setProviderForm(current => ({ ...current, displayName: event.target.value }))}
                className={inputClassName}
              />
            </Field>

            <Field label="Default Model">
              <input
                value={providerForm.defaultModel}
                onChange={(event) => setProviderForm(current => ({ ...current, defaultModel: event.target.value }))}
                className={inputClassName}
              />
            </Field>

            <Field label="Allowed Models">
              <input
                value={providerForm.allowedModelsCsv}
                onChange={(event) => setProviderForm(current => ({ ...current, allowedModelsCsv: event.target.value }))}
                className={inputClassName}
              />
            </Field>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Endpoint">
                <input
                  value={providerForm.endpoint ?? ''}
                  onChange={(event) => setProviderForm(current => ({ ...current, endpoint: event.target.value }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Region">
                <input
                  value={providerForm.region ?? ''}
                  onChange={(event) => setProviderForm(current => ({ ...current, region: event.target.value }))}
                  className={inputClassName}
                />
              </Field>
            </div>

            <Field label="Azure Deployment Name">
              <input
                value={providerForm.azureDeploymentName ?? ''}
                onChange={(event) => setProviderForm(current => ({ ...current, azureDeploymentName: event.target.value }))}
                className={inputClassName}
              />
            </Field>

            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Timeout">
                <input
                  type="number"
                  min={1}
                  value={providerForm.timeoutSeconds}
                  onChange={(event) => setProviderForm(current => ({ ...current, timeoutSeconds: Number(event.target.value) }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Retries">
                <input
                  type="number"
                  min={0}
                  value={providerForm.retryCount}
                  onChange={(event) => setProviderForm(current => ({ ...current, retryCount: Number(event.target.value) }))}
                  className={inputClassName}
                />
              </Field>
              <Field label="Priority">
                <input
                  type="number"
                  min={0}
                  value={providerForm.failoverPriority}
                  onChange={(event) => setProviderForm(current => ({ ...current, failoverPriority: Number(event.target.value) }))}
                  className={inputClassName}
                />
              </Field>
            </div>

            <Field label="API Key">
              <input
                type="password"
                value={providerForm.apiKey ?? ''}
                onChange={(event) => setProviderForm(current => ({ ...current, apiKey: event.target.value }))}
                className={inputClassName}
                placeholder={selectedProvider?.hasStoredCredential ? 'Leave blank to keep the stored credential' : 'Enter a credential'}
              />
            </Field>

            <Field label="Credential Storage">
              <select
                value={providerForm.credentialStorageMode ?? 'EncryptedDatabase'}
                onChange={(event) => setProviderForm(current => ({ ...current, credentialStorageMode: event.target.value as 'EncryptedDatabase' | 'ExternalSecretReference' }))}
                className={inputClassName}
              >
                <option value="EncryptedDatabase">EncryptedDatabase</option>
                <option value="ExternalSecretReference">ExternalSecretReference</option>
              </select>
            </Field>

            <Field label="External Secret Reference">
              <input
                value={providerForm.externalSecretReference ?? ''}
                onChange={(event) => setProviderForm(current => ({ ...current, externalSecretReference: event.target.value }))}
                className={inputClassName}
                placeholder="platform://xenia/provider"
              />
            </Field>

            <label className="inline-flex items-center gap-3 rounded-lg border border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={providerForm.enabled}
                onChange={(event) => setProviderForm(current => ({ ...current, enabled: event.target.checked }))}
                className="h-4 w-4 rounded border-gray-300 text-gray-900 focus:ring-gray-500"
              />
              Enable this provider
            </label>

            <div className="flex justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={resetForm}
                className={secondaryButtonClassName}
              >
                Reset
              </button>
              <button
                type="button"
                disabled={isPending}
                onClick={upsertProvider}
                className={primaryButtonClassName}
              >
                {selectedProvider ? 'Update provider' : 'Create provider'}
              </button>
            </div>
          </div>
        </div>
      </section>

      {tenantProviders.length > 0 ? (
        <section className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Tenant-scoped providers currently visible to platform admins</h2>
          <div className="mt-4 grid gap-3 lg:grid-cols-2">
            {tenantProviders.map((provider) => (
              <div key={provider.providerConfigurationId} className={`${mutedPanelClassName} px-4 py-3`}>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-gray-900">{provider.displayName}</p>
                  <span className="rounded-full bg-gray-200 px-2 py-0.5 text-[11px] font-semibold text-gray-700">
                    {provider.tenantId ? 'Tenant' : 'Scoped'}
                  </span>
                </div>
                <p className="mt-2 text-sm text-gray-600">
                  {provider.providerType} · {provider.defaultModel}
                </p>
                <p className="mt-1 text-xs text-gray-400">Tenant {provider.tenantId ?? 'unknown'}</p>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      <section className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Provider health</h2>
          <div className="mt-4 space-y-3">
            {overview.providerHealth.length === 0 ? (
              <div className={emptyStateClassName}>
                No provider health checks have been recorded yet.
              </div>
            ) : null}

            {overview.providerHealth.map((provider) => (
              <div key={provider.providerConfigurationId} className={`${mutedPanelClassName} px-4 py-3`}>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-gray-900">{provider.providerName}</p>
                  <span className="rounded-full border border-green-200 bg-green-50 px-2 py-0.5 text-[11px] font-semibold text-green-700">
                    {provider.status}
                  </span>
                </div>
                <p className="mt-2 text-sm text-gray-600">{provider.message}</p>
                <p className="mt-2 text-xs text-gray-400">Checked {formatTimestamp(provider.checkedAtUtc)}</p>
              </div>
            ))}
          </div>
        </div>

        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Model catalog</h2>
          {models.length === 0 ? (
            <div className={`mt-4 ${emptyStateClassName}`}>
              No model catalog entries are available.
            </div>
          ) : (
          <div className="mt-4 overflow-hidden rounded-lg border border-gray-200">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  {['Provider', 'Model', 'Streaming', 'Embeddings', 'Status'].map((label) => (
                    <th key={label} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-gray-500">{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {models.map((model) => (
                  <tr key={`${model.provider}-${model.modelCode}`}>
                    <td className="px-4 py-3 text-gray-700">{model.provider}</td>
                    <td className="px-4 py-3 font-medium text-gray-900">{model.displayName}</td>
                    <td className="px-4 py-3 text-gray-600">{model.supportsStreaming ? 'Yes' : 'No'}</td>
                    <td className="px-4 py-3 text-gray-600">{model.supportsEmbeddings ? 'Yes' : 'No'}</td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${model.enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-600'}`}>
                        {model.enabled ? 'Enabled' : 'Disabled'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          )}
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Usage events</h2>
          <div className="mt-4 space-y-3">
            {usage.items.length === 0 ? (
              <div className={emptyStateClassName}>
                No usage events recorded yet.
              </div>
            ) : null}

            {usage.items.slice(0, 8).map((item) => (
              <div key={item.usageEventId} className={`${mutedPanelClassName} px-4 py-3`}>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-gray-900">{item.provider} · {item.model}</p>
                  <p className="text-xs text-gray-400">{formatTimestamp(item.createdAtUtc)}</p>
                </div>
                <p className="mt-2 text-sm text-gray-600">
                  {item.eventKind} · prompt {item.promptTokens} · completion {item.completionTokens} · ${item.estimatedCostUsd.toFixed(4)}
                </p>
                <p className="mt-1 text-xs text-gray-400">{item.userId}</p>
              </div>
            ))}
          </div>
        </div>

        <div className={`${cardClassName} p-6`}>
          <h2 className="text-base font-semibold text-gray-900">Audit history</h2>
          <div className="mt-4 space-y-3">
            {audit.length === 0 ? (
              <div className={emptyStateClassName}>
                No audit entries recorded yet.
              </div>
            ) : null}

            {audit.slice(0, 10).map((item) => (
              <div key={item.auditEventId} className={`${mutedPanelClassName} px-4 py-3`}>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-gray-900">{item.eventType}</p>
                  <p className="text-xs text-gray-400">{formatTimestamp(item.createdAtUtc)}</p>
                </div>
                <p className="mt-2 text-sm text-gray-600">{item.description}</p>
                <p className="mt-1 text-xs text-gray-400">{item.actorUserId}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-2">
        <CatalogCard
          title="Prompt Catalog"
          empty="No prompt templates stored yet."
          items={prompts.map((prompt) => ({
            key: prompt.promptTemplateId,
            title: prompt.displayName,
            subtitle: `${prompt.templateCode} · ${prompt.enabled ? 'Enabled' : 'Disabled'}`,
            body: prompt.description,
          }))}
        />
        <CatalogCard
          title="Skill Catalog"
          empty="No skills stored yet."
          items={skills.map((skill) => ({
            key: skill.skillId,
            title: skill.displayName,
            subtitle: `${skill.skillCode} · ${skill.enabled ? 'Enabled' : 'Disabled'}`,
            body: skill.description,
          }))}
        />
      </section>

      <section className="grid gap-6 xl:grid-cols-3">
        <CatalogCard
          title="Agents"
          empty="No agents stored yet."
          items={agents.map((agent) => ({
            key: agent.agentId,
            title: agent.displayName,
            subtitle: `${agent.agentCode} · ${agent.enabled ? 'Enabled' : 'Disabled'}`,
            body: agent.description,
          }))}
        />
        <CatalogCard
          title="Knowledge Sources"
          empty="No knowledge sources stored yet."
          items={knowledgeSources.map((source) => ({
            key: source.knowledgeSourceId,
            title: source.displayName,
            subtitle: `${source.sourceType} · ${source.status}`,
            body: `${source.sourceCode}${source.tenantId ? ` · tenant ${source.tenantId}` : ' · global'}`,
          }))}
        />
        <CatalogCard
          title="Marketplace Assets"
          empty="No marketplace assets stored yet."
          items={marketplaceAssets.map((asset) => ({
            key: asset.marketplaceAssetId,
            title: asset.displayName,
            subtitle: `${asset.assetType} · ${asset.enabled ? 'Enabled' : 'Disabled'}`,
            body: `${asset.assetCode} · ${asset.description}`,
          }))}
        />
      </section>
    </main>
  );
}

function MetricCard({
  label,
  value,
}: {
  label: string;
  value: string;
}) {
  return (
    <div className={`${cardClassName} p-5`}>
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-gray-500">{label}</p>
      <p className="mt-3 text-2xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}

function StatPill({
  label,
  value,
}: {
  label: string;
  value: string;
}) {
  return (
    <div className={`${mutedPanelClassName} px-4 py-3`}>
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-gray-500">{label}</p>
      <p className="mt-2 text-lg font-semibold text-gray-900">{value}</p>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-gray-500">{label}</span>
      {children}
    </label>
  );
}

const inputClassName = 'w-full rounded-lg border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 focus:border-gray-400 focus:outline-none';

function CatalogCard({
  title,
  empty,
  items,
}: {
  title: string;
  empty: string;
  items: Array<{ key: string; title: string; subtitle: string; body: string }>;
}) {
  return (
    <div className={`${cardClassName} p-6`}>
      <h2 className="text-base font-semibold text-gray-900">{title}</h2>
      <div className="mt-4 space-y-3">
        {items.length === 0 ? (
          <div className={emptyStateClassName}>
            {empty}
          </div>
        ) : null}

        {items.map((item) => (
          <div key={item.key} className={`${mutedPanelClassName} px-4 py-3`}>
            <p className="text-sm font-semibold text-gray-900">{item.title}</p>
            <p className="mt-1 text-xs font-medium uppercase tracking-[0.16em] text-gray-500">{item.subtitle}</p>
            <p className="mt-2 text-sm text-gray-600">{item.body}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
