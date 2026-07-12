'use client';

import { useEffect, useState, useTransition } from 'react';
import { ApiError, apiClient } from '@/lib/api-client';
import { XeniaProductShell } from '../xenia-product-shell';

type XeniaDeploymentModel = 'Managed' | 'BringYourOwnAI';
type XeniaProviderType = 'OpenAI' | 'Anthropic' | 'Gemini' | 'AzureOpenAI' | 'AwsBedrock';

interface XeniaTenantConfiguration {
  tenantId: string;
  enabled: boolean;
  deploymentModel: XeniaDeploymentModel;
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

interface ProviderDraft {
  providerType: XeniaProviderType;
  displayName: string;
  endpoint: string;
  region: string;
  azureDeploymentName: string;
  defaultModel: string;
  allowedModels: string;
  timeoutSeconds: number;
  retryCount: number;
  failoverPriority: number;
  enabled: boolean;
  apiKey: string;
}

interface ProviderTestResult {
  success: boolean;
  status: string;
  message: string;
  verifiedAtUtc: string;
  fingerprint: string | null;
}

interface ProviderSummary {
  providerConfigurationId: string;
  providerType: XeniaProviderType;
  scope: 'Platform' | 'Tenant';
  tenantId: string | null;
  displayName: string;
  defaultModel: string;
  verificationStatus: 'Unverified' | 'Verified' | 'Failed';
  lastVerifiedAtUtc: string | null;
  credentialStorageMode: 'EncryptedDatabase' | 'ExternalSecretReference';
  secretReference: string | null;
  credentialFingerprint: string | null;
  credentialLastFour: string | null;
  hasStoredCredential: boolean;
}

const EMPTY_PROVIDER_DRAFT: ProviderDraft = {
  providerType: 'OpenAI',
  displayName: '',
  endpoint: '',
  region: '',
  azureDeploymentName: '',
  defaultModel: 'gpt-4.1-mini',
  allowedModels: 'gpt-4.1-mini',
  timeoutSeconds: 60,
  retryCount: 2,
  failoverPriority: 100,
  enabled: true,
  apiKey: '',
};

function asCsv(values: string[]) {
  return values.length > 0 ? values.join(', ') : 'Not configured';
}

function formatTimestamp(value: string) {
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

function parseAllowedModels(value: string) {
  return value
    .split(',')
    .map(item => item.trim())
    .filter(Boolean);
}

export function XeniaSettingsClient({
  sessionEmail,
  tenantCode,
}: {
  sessionEmail: string;
  tenantCode: string;
}) {
  const [configuration, setConfiguration] = useState<XeniaTenantConfiguration | null>(null);
  const [draft, setDraft] = useState<ProviderDraft>(EMPTY_PROVIDER_DRAFT);
  const [providers, setProviders] = useState<ProviderSummary[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<ProviderTestResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isPending, startTransition] = useTransition();

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [response, providerResponse] = await Promise.all([
          apiClient.get<XeniaTenantConfiguration>('/xenia/tenant/configuration'),
          apiClient.get<ProviderSummary[]>('/xenia/tenant/providers').catch(() => ({ data: [] as ProviderSummary[] })),
        ]);
        if (cancelled) return;
        setConfiguration(response.data);
        setProviders(providerResponse.data);
        setDraft(current => ({
          ...current,
          defaultModel: response.data.defaultModel || current.defaultModel,
        }));
        setLoadError(null);
      } catch (error) {
        if (cancelled) return;
        setLoadError(error instanceof ApiError ? error.message : 'Unable to load the Xenia tenant configuration.');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    }

    void load();
    return () => { cancelled = true; };
  }, []);

  const submitPayload = {
    providerType: draft.providerType,
    displayName: draft.displayName.trim(),
    endpoint: draft.endpoint.trim() || null,
    region: draft.region.trim() || null,
    azureDeploymentName: draft.azureDeploymentName.trim() || null,
    defaultModel: draft.defaultModel.trim(),
    allowedModels: parseAllowedModels(draft.allowedModels),
    timeoutSeconds: Number(draft.timeoutSeconds),
    retryCount: Number(draft.retryCount),
    failoverPriority: Number(draft.failoverPriority),
    enabled: draft.enabled,
    apiKey: draft.apiKey.trim() || null,
  };

  const validateDraft = () => {
    if (!submitPayload.displayName) return 'Display name is required.';
    if (!submitPayload.defaultModel) return 'Default model is required.';
    if (submitPayload.allowedModels.length === 0) return 'Enter at least one allowed model.';
    return null;
  };

  const handleTest = () => {
    const validationError = validateDraft();
    if (validationError) {
      setActionError(validationError);
      return;
    }

    setActionError(null);
    setSuccessMessage(null);

    startTransition(async () => {
      try {
        const response = await apiClient.post<ProviderTestResult>('/xenia/tenant/byoai/providers/test', submitPayload);
        setTestResult(response.data);
        setSuccessMessage(response.data.message);
      } catch (error) {
        setActionError(error instanceof ApiError ? error.message : 'Unable to test this BYOAI provider.');
      }
    });
  };

  const handleSave = () => {
    const validationError = validateDraft();
    if (validationError) {
      setActionError(validationError);
      return;
    }

    setActionError(null);
    setSuccessMessage(null);

    startTransition(async () => {
      try {
        const response = await apiClient.put<XeniaTenantConfiguration>('/xenia/tenant/byoai/configuration', submitPayload);
        setConfiguration(response.data);
        const providerResponse = await apiClient.get<ProviderSummary[]>('/xenia/tenant/providers').catch(() => ({ data: [] as ProviderSummary[] }));
        setProviders(providerResponse.data);
        setSuccessMessage('Tenant BYOAI configuration saved. Xenia is now using the tenant-scoped provider.');
      } catch (error) {
        setActionError(error instanceof ApiError ? error.message : 'Unable to save the BYOAI configuration.');
      }
    });
  };

  return (
    <XeniaProductShell
      eyebrow="Xenia Settings"
      title="Tenant AI configuration"
      description="Review current Xenia posture for this tenant and manage the Bring Your Own AI setup exposed by the tenant service boundary."
    >
      <div className="space-y-6">
        <section className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-2xl border border-slate-200 bg-white/90 px-4 py-3 shadow-sm">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Session</p>
            <p className="mt-2 text-sm font-medium text-slate-900">{sessionEmail}</p>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-white/90 px-4 py-3 shadow-sm">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Tenant</p>
            <p className="mt-2 text-sm font-medium text-slate-900">{tenantCode}</p>
          </div>
        </section>

        {loadError ? (
          <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {loadError}
          </div>
        ) : null}

        <section className="grid gap-6 lg:grid-cols-[1.05fr_1fr]">
          <div className="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-base font-semibold text-slate-950">Current Xenia posture</h2>
                <p className="mt-1 text-sm text-slate-500">What the Xenia tenant endpoint currently returns for this tenant.</p>
              </div>
              {isLoading ? (
                <span className="rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-500">Loading</span>
              ) : configuration ? (
                <span className={`rounded-full px-3 py-1 text-xs font-semibold ${configuration.enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
                  {configuration.enabled ? 'Enabled' : 'Disabled'}
                </span>
              ) : null}
            </div>

            {configuration ? (
              <div className="mt-5 grid gap-4 sm:grid-cols-2">
                {[
                  ['Deployment Model', configuration.deploymentModel],
                  ['Default Model', configuration.defaultModel],
                  ['Reasoning', configuration.reasoningLevel],
                  ['Moderation', configuration.moderationPolicy],
                  ['Retention', configuration.retentionPolicy],
                  ['Failover', configuration.failoverEnabled ? 'Enabled' : 'Disabled'],
                  ['Allowed Skills', asCsv(configuration.allowedSkills)],
                  ['Allowed Agents', asCsv(configuration.allowedAgents)],
                  ['Allowed Tools', asCsv(configuration.allowedTools)],
                  ['Updated', formatTimestamp(configuration.updatedAtUtc)],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-2xl border border-slate-100 bg-slate-50 px-4 py-3">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</p>
                    <p className="mt-2 text-sm font-medium leading-6 text-slate-900">{value}</p>
                  </div>
                ))}
              </div>
            ) : (
              <div className="mt-5 rounded-2xl border border-dashed border-slate-200 bg-slate-50 px-4 py-10 text-center text-sm text-slate-500">
                Configuration has not loaded yet.
              </div>
            )}

            {providers.length > 0 ? (
              <div className="mt-5 rounded-2xl border border-slate-100 bg-slate-50 p-4">
                <h3 className="text-sm font-semibold text-slate-900">Visible provider metadata</h3>
                <div className="mt-3 space-y-3">
                  {providers.map((provider) => (
                    <div key={provider.providerConfigurationId} className="rounded-2xl border border-slate-200 bg-white px-4 py-3">
                      <div className="flex items-center justify-between gap-3">
                        <p className="text-sm font-semibold text-slate-900">{provider.displayName}</p>
                        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700">{provider.scope}</span>
                      </div>
                      <p className="mt-2 text-sm text-slate-600">
                        {provider.providerType} · {provider.defaultModel} · {provider.verificationStatus}
                      </p>
                      <p className="mt-1 text-xs text-slate-500">
                        {provider.credentialStorageMode} · {provider.credentialFingerprint ?? 'No fingerprint'} · {provider.secretReference ?? provider.credentialLastFour ?? 'No reference'}
                      </p>
                      <p className="mt-1 text-xs text-slate-400">Last verified {formatTimestamp(provider.lastVerifiedAtUtc)}</p>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </div>

          <div className="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-base font-semibold text-slate-950">What tenant admins can change</h2>
            <div className="mt-4 space-y-3">
              {[
                'Switch the tenant into Bring Your Own AI mode by saving a tenant-scoped provider configuration.',
                'Run a provider connectivity test before saving credentials.',
                'Review the current Xenia deployment mode, default model, and platform-defined governance settings.',
              ].map((item) => (
                <div key={item} className="flex items-start gap-3 rounded-2xl bg-slate-50 px-4 py-3">
                  <div className="mt-0.5 flex h-7 w-7 items-center justify-center rounded-full bg-slate-900 text-xs text-white">
                    <i className="ri-check-line" />
                  </div>
                  <p className="text-sm leading-6 text-slate-600">{item}</p>
                </div>
              ))}
            </div>

            <div className="mt-5 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-4 text-sm text-slate-600">
              Xenia does not yet expose a tenant endpoint to read back stored BYOAI credential metadata. This form is optimized for creating or rotating a tenant-scoped provider, not for revealing previously stored secrets.
            </div>
          </div>
        </section>

        <section className="rounded-[28px] border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-slate-950">Bring Your Own AI provider</h2>
              <p className="mt-1 text-sm text-slate-500">
                Saving this form will configure a tenant-scoped provider and switch the tenant deployment model to `BringYourOwnAI`.
              </p>
            </div>
            <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
              API keys are only sent when you test or save.
            </div>
          </div>

          {actionError ? (
            <div className="mt-5 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {actionError}
            </div>
          ) : null}

          {successMessage ? (
            <div className="mt-5 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
              {successMessage}
            </div>
          ) : null}

          <div className="mt-6 grid gap-5 lg:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Provider Type</span>
              <select
                value={draft.providerType}
                onChange={(event) => setDraft(current => ({ ...current, providerType: event.target.value as XeniaProviderType }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
              >
                {['OpenAI', 'Anthropic', 'Gemini', 'AzureOpenAI', 'AwsBedrock'].map((value) => (
                  <option key={value} value={value}>{value}</option>
                ))}
              </select>
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Display Name</span>
              <input
                value={draft.displayName}
                onChange={(event) => setDraft(current => ({ ...current, displayName: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="Tenant OpenAI account"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Endpoint</span>
              <input
                value={draft.endpoint}
                onChange={(event) => setDraft(current => ({ ...current, endpoint: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="Optional custom endpoint"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Region</span>
              <input
                value={draft.region}
                onChange={(event) => setDraft(current => ({ ...current, region: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="Optional region"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Azure Deployment Name</span>
              <input
                value={draft.azureDeploymentName}
                onChange={(event) => setDraft(current => ({ ...current, azureDeploymentName: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="Only needed for Azure OpenAI"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Default Model</span>
              <input
                value={draft.defaultModel}
                onChange={(event) => setDraft(current => ({ ...current, defaultModel: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="gpt-4.1-mini"
              />
            </label>

            <label className="block lg:col-span-2">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Allowed Models</span>
              <input
                value={draft.allowedModels}
                onChange={(event) => setDraft(current => ({ ...current, allowedModels: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="gpt-4.1-mini, gpt-4.1"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Timeout Seconds</span>
              <input
                type="number"
                min={1}
                value={draft.timeoutSeconds}
                onChange={(event) => setDraft(current => ({ ...current, timeoutSeconds: Number(event.target.value) }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Retry Count</span>
              <input
                type="number"
                min={0}
                value={draft.retryCount}
                onChange={(event) => setDraft(current => ({ ...current, retryCount: Number(event.target.value) }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
              />
            </label>

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Failover Priority</span>
              <input
                type="number"
                min={0}
                value={draft.failoverPriority}
                onChange={(event) => setDraft(current => ({ ...current, failoverPriority: Number(event.target.value) }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
              />
            </label>

            <label className="block lg:col-span-2">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">API Key</span>
              <input
                type="password"
                value={draft.apiKey}
                onChange={(event) => setDraft(current => ({ ...current, apiKey: event.target.value }))}
                className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 focus:border-amber-300 focus:outline-none"
                placeholder="Paste a credential for test or save"
              />
            </label>

            <label className="inline-flex items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-4 text-sm text-slate-700">
              <input
                type="checkbox"
                checked={draft.enabled}
                onChange={(event) => setDraft(current => ({ ...current, enabled: event.target.checked }))}
                className="h-4 w-4 rounded border-slate-300 text-amber-600 focus:ring-amber-500"
              />
              Enable this provider for tenant use
            </label>
          </div>

          {testResult ? (
            <div className="mt-5 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-4 text-sm text-slate-700">
              <p className="font-semibold text-slate-900">Last test: {testResult.status}</p>
              <p className="mt-1">{testResult.message}</p>
              <p className="mt-1 text-xs text-slate-500">
                Verified {formatTimestamp(testResult.verifiedAtUtc)}
                {testResult.fingerprint ? ` · Fingerprint ${testResult.fingerprint}` : ''}
              </p>
            </div>
          ) : null}

          <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
            <button
              type="button"
              disabled={isPending}
              onClick={handleTest}
              className="inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-5 py-3 text-sm font-semibold text-slate-700 transition hover:border-amber-300 hover:text-amber-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isPending ? <i className="ri-loader-4-line animate-spin" /> : <i className="ri-plug-line" />}
              Test provider
            </button>
            <button
              type="button"
              disabled={isPending}
              onClick={handleSave}
              className="inline-flex items-center gap-2 rounded-full bg-slate-950 px-5 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-300"
            >
              {isPending ? <i className="ri-loader-4-line animate-spin" /> : <i className="ri-save-line" />}
              Save BYOAI configuration
            </button>
          </div>
        </section>
      </div>
    </XeniaProductShell>
  );
}
