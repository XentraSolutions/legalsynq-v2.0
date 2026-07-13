import { revalidatePath } from 'next/cache';
import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';
import { buildXeniaAssistantAdminSettingsPayload } from '@/lib/xenia-assistant-settings';
import {
  getXeniaAssistantAdminSettings,
  getXeniaAssistantGlobalConfig,
  getXeniaAssistantUsage,
  updateXeniaAssistantAdminSettings,
  type XeniaAssistantAdminSettings,
  type XeniaAssistantUsageRow,
  type XeniaConfigurationEntry,
} from '@/lib/xenia-api';

export const dynamic = 'force-dynamic';

type XeniaSettingsPageProps = {
  searchParams?: Promise<{
    saved?: string;
    error?: string;
    detail?: string;
  }>;
};

const DEFAULT_ASSISTANT_SETTINGS: XeniaAssistantAdminSettings = {
  provider: 'Fake',
  modelKey: 'xenia-fake',
  openAiBaseUrl: 'https://api.openai.com',
  openAiApiKeyConfigured: false,
  openAiTimeoutSeconds: 60,
  openAiReasoningEffort: null,
  openAiTextVerbosity: null,
  openAiMaxOutputTokens: null,
  lastUpdatedAtUtc: null,
};

export default async function XeniaSettingsPage({ searchParams }: XeniaSettingsPageProps) {
  await requirePlatformAdmin();

  const params = await searchParams;
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value ?? '';

  const [assistantSettings, config, usage] = await Promise.all([
    getXeniaAssistantAdminSettings(token),
    getXeniaAssistantGlobalConfig(token),
    getXeniaAssistantUsage(token),
  ]);

  const effectiveSettings = assistantSettings ?? DEFAULT_ASSISTANT_SETTINGS;

  async function handleSave(formData: FormData) {
    'use server';

    await requirePlatformAdmin();

    const jar2 = await cookies();
    const tok = jar2.get(SESSION_COOKIE_NAME)?.value ?? '';
    const payload = buildXeniaAssistantAdminSettingsPayload({
      provider: formData.get('provider'),
      modelKey: formData.get('modelKey'),
      openAiBaseUrl: formData.get('openAiBaseUrl'),
      openAiTimeoutSeconds: formData.get('openAiTimeoutSeconds'),
      openAiReasoningEffort: formData.get('openAiReasoningEffort'),
      openAiTextVerbosity: formData.get('openAiTextVerbosity'),
      openAiMaxOutputTokens: formData.get('openAiMaxOutputTokens'),
    });

    try {
      await updateXeniaAssistantAdminSettings(tok, payload);
      revalidatePath('/xenia/settings');
    } catch (error) {
      const detail =
        error instanceof Error && error.message.trim().length > 0
          ? error.message.trim()
          : 'Unknown save failure';
      redirect(`/xenia/settings?error=1&detail=${encodeURIComponent(detail)}`);
    }

    redirect('/xenia/settings?saved=1');
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Xenia Assistant Settings</h2>
        <p className="mt-1 text-sm text-gray-500">
          Configure the Xenia assistant provider, model, reasoning effort, verbosity, and OpenAI runtime settings from control-center.
        </p>
      </div>

      <div className="space-y-6">
        {params?.saved === '1' && (
          <StatusBanner tone="success" message="Xenia assistant settings updated." />
        )}
        {params?.error === '1' && (
          <StatusBanner
            tone="error"
            message={
              params?.detail
                ? `Failed to update Xenia assistant settings. ${params.detail}`
                : 'Failed to update Xenia assistant settings.'
            }
          />
        )}
        {!assistantSettings && (
          <StatusBanner
            tone="warn"
            message="Unable to load effective settings from the Xenia service. The form below is showing safe defaults."
          />
        )}

        <AssistantSettingsForm
          settings={effectiveSettings}
          onSave={handleSave}
        />

        <SettingsSection
          title="Persisted Configuration Entries"
          description="Stored assistant configuration rows. The OpenAI API key remains appsettings-only and is not persisted here."
          entries={config}
        />

        <UsageSection usage={usage} />

        <div className="rounded-lg border border-gray-200 bg-gray-50 p-5">
          <h3 className="mb-2 text-sm font-semibold text-gray-700">Configuration Architecture</h3>
          <p className="mb-3 text-sm text-gray-600">
            Xenia resolves assistant settings in this order: control-center configuration first, then `appsettings`
            fallback values where no persisted override exists.
          </p>
          <ol className="list-decimal list-inside space-y-1 text-sm text-gray-600">
            <li>Global platform overrides</li>
            <li>Tenant overrides</li>
            <li>Static service defaults</li>
          </ol>
          <p className="mt-3 text-xs text-gray-400">
            OpenAI API keys are read only from Xenia `appsettings` and are never persisted from control-center.
          </p>
        </div>
      </div>
    </div>
  );
}

function AssistantSettingsForm({
  settings,
  onSave,
}: {
  settings: XeniaAssistantAdminSettings;
  onSave: (formData: FormData) => Promise<void>;
}) {
  return (
    <form action={onSave} className="rounded-lg border border-gray-200 bg-white p-5">
      <div className="mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Assistant Runtime</h3>
        <p className="mt-0.5 text-xs text-gray-500">
          These values control which assistant provider Xenia uses for live responses, including model-specific OpenAI runtime tuning.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field>
          <label htmlFor="provider" className="text-sm font-medium text-gray-700">Provider</label>
          <select
            id="provider"
            name="provider"
            defaultValue={settings.provider}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          >
            <option value="Fake">Fake</option>
            <option value="OpenAI">OpenAI</option>
          </select>
        </Field>

        <Field>
          <label htmlFor="modelKey" className="text-sm font-medium text-gray-700">Model Key</label>
          <input
            id="modelKey"
            name="modelKey"
            type="text"
            defaultValue={settings.modelKey}
            placeholder="gpt-4.1-mini"
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
        </Field>

        <Field className="md:col-span-2">
          <label htmlFor="openAiBaseUrl" className="text-sm font-medium text-gray-700">OpenAI Base URL</label>
          <input
            id="openAiBaseUrl"
            name="openAiBaseUrl"
            type="url"
            defaultValue={settings.openAiBaseUrl}
            placeholder="https://api.openai.com"
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
        </Field>

        <Field>
          <label htmlFor="openAiReasoningEffort" className="text-sm font-medium text-gray-700">OpenAI Reasoning Effort</label>
          <select
            id="openAiReasoningEffort"
            name="openAiReasoningEffort"
            defaultValue={settings.openAiReasoningEffort ?? ''}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          >
            <option value="">Default / unset</option>
            <option value="minimal">Minimal</option>
            <option value="low">Low</option>
            <option value="medium">Medium</option>
            <option value="high">High</option>
          </select>
          <p className="mt-1 text-xs text-gray-500">
            Optional. Only applies to OpenAI reasoning-capable models.
          </p>
        </Field>

        <Field>
          <label htmlFor="openAiTextVerbosity" className="text-sm font-medium text-gray-700">OpenAI Text Verbosity</label>
          <select
            id="openAiTextVerbosity"
            name="openAiTextVerbosity"
            defaultValue={settings.openAiTextVerbosity ?? ''}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          >
            <option value="">Default / unset</option>
            <option value="low">Low</option>
            <option value="medium">Medium</option>
            <option value="high">High</option>
          </select>
          <p className="mt-1 text-xs text-gray-500">
            Optional. Leave blank to let the model use its default verbosity.
          </p>
        </Field>

        <Field>
          <label htmlFor="openAiTimeoutSeconds" className="text-sm font-medium text-gray-700">OpenAI Timeout (seconds)</label>
          <input
            id="openAiTimeoutSeconds"
            name="openAiTimeoutSeconds"
            type="number"
            min={1}
            max={600}
            defaultValue={settings.openAiTimeoutSeconds}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
        </Field>

        <Field>
          <label htmlFor="openAiMaxOutputTokens" className="text-sm font-medium text-gray-700">OpenAI Max Output Tokens</label>
          <input
            id="openAiMaxOutputTokens"
            name="openAiMaxOutputTokens"
            type="number"
            min={1}
            defaultValue={settings.openAiMaxOutputTokens ?? ''}
            placeholder="Leave blank to use model default"
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
          />
          <p className="mt-1 text-xs text-gray-500">
            Optional hard cap for generated output tokens.
          </p>
        </Field>

        <Field className="md:col-span-2">
          <label className="text-sm font-medium text-gray-700">OpenAI API Key Source</label>
          <div className="mt-1 rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-700">
            {settings.openAiApiKeyConfigured
              ? 'Configured in Xenia appsettings.'
              : 'Missing from Xenia appsettings.'}
          </div>
          <p className="mt-1 text-xs text-gray-500">
            Control-center does not save the OpenAI API key. Set `XeniaAssistant:OpenAI:ApiKey` in the Xenia service appsettings.
          </p>
        </Field>
      </div>

      <div className="mt-4 flex items-center justify-between gap-4 border-t border-gray-100 pt-4">
        <p className="text-xs text-gray-500">
          {settings.lastUpdatedAtUtc
            ? `Last updated ${new Date(settings.lastUpdatedAtUtc).toLocaleString()}`
            : 'No persisted assistant override has been saved yet.'}
        </p>
        <button
          type="submit"
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Save Xenia Assistant Settings
        </button>
      </div>
    </form>
  );
}

function SettingsSection({
  title,
  description,
  entries,
}: {
  title: string;
  description: string;
  entries: XeniaConfigurationEntry[];
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5">
      <h3 className="text-sm font-semibold text-gray-700">{title}</h3>
      <p className="mb-4 mt-0.5 text-xs text-gray-500">{description}</p>
      {entries.length > 0 ? (
        <div className="overflow-hidden rounded-md border border-gray-200">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-3 py-2 text-left">Key</th>
                <th className="px-3 py-2 text-left">Value</th>
                <th className="px-3 py-2 text-left">Scope</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {entries.map(entry => (
                <tr key={entry.id}>
                  <td className="px-3 py-2 font-mono text-xs text-gray-700">{entry.configurationKey}</td>
                  <td className="px-3 py-2 text-gray-600">
                    {entry.isSecret ? 'Secret configuration entry' : entry.configurationValue ?? 'Not set'}
                  </td>
                  <td className="px-3 py-2 text-gray-500">{entry.scopeType}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="rounded border border-dashed border-gray-300 bg-gray-50 p-6 text-center">
          <p className="text-sm italic text-gray-400">No assistant configuration entries have been created yet.</p>
        </div>
      )}
    </div>
  );
}

function UsageSection({ usage }: { usage: XeniaAssistantUsageRow[] }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5">
      <h3 className="text-sm font-semibold text-gray-700">Usage</h3>
      <p className="mb-4 mt-0.5 text-xs text-gray-500">
        Last 30 days of assistant usage grouped by tenant, agent, provider, and model.
      </p>
      {usage.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {usage.map(row => (
            <div key={`${row.tenantId}-${row.agentKey}-${row.provider}-${row.modelKey}`} className="rounded-md border border-gray-200 p-4">
              <p className="text-sm font-semibold text-gray-900">{row.agentKey}</p>
              <p className="mt-1 text-xs font-mono text-gray-400">{row.provider} / {row.modelKey}</p>
              <div className="mt-3 grid grid-cols-3 gap-2 text-xs">
                <Metric label="Requests" value={String(row.requests)} />
                <Metric label="Tokens" value={String(row.inputTokens + row.outputTokens)} />
                <Metric label="Avg ms" value={String(Math.round(row.averageLatencyMs))} />
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="rounded border border-dashed border-gray-300 bg-gray-50 p-6 text-center">
          <p className="text-sm italic text-gray-400">No assistant usage has been recorded yet.</p>
        </div>
      )}
    </div>
  );
}

function Field({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <div className={className}>{children}</div>;
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-gray-400">{label}</p>
      <p className="mt-1 font-semibold text-gray-800">{value}</p>
    </div>
  );
}

function StatusBanner({
  tone,
  message,
}: {
  tone: 'success' | 'error' | 'warn';
  message: string;
}) {
  const palette = {
    success: 'border-green-200 bg-green-50 text-green-700',
    error: 'border-red-200 bg-red-50 text-red-700',
    warn: 'border-amber-200 bg-amber-50 text-amber-700',
  }[tone];

  return (
    <div className={`rounded-lg border px-4 py-3 text-sm ${palette}`}>
      {message}
    </div>
  );
}
