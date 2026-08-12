const XENIA_BASE = process.env.XENIA_API_BASE ?? 'http://127.0.0.1:5035';

export interface XeniaServiceInfo {
  service: string;
  version: string;
  environment: string;
  started_at: string;
  uptime_seconds: number;
  is_standalone: boolean;
}

export interface XeniaModuleDto {
  id: string;
  module_key: string;
  name: string;
  version: string;
  description: string;
  global_enabled: boolean;
  status: string;
  configuration_namespace: string;
  created_at_utc: string;
  updated_at_utc: string;
}

export interface XeniaAdapterDto {
  id: string;
  adapter_key: string;
  adapter_type: string;
  name: string;
  version: string;
  /** Mandatory | Optional | Disabled — controls /ready behavior */
  criticality: string;
  configuration_status: string;
  availability_status: string;
  health_status: string;
  last_health_check_at: string | null;
  diagnostic_message: string | null;
}

export interface XeniaHealthResponse {
  status: string;
  service: string;
  timestamp: string;
}

export interface XeniaReadyResponse {
  status: string;
  checks: Record<string, unknown>;
}

async function fetchXenia<T>(path: string, token?: string): Promise<T | null> {
  try {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const res = await fetch(`${XENIA_BASE}${path}`, {
      headers,
      cache: 'no-store',
    });

    if (!res.ok) return null;
    return res.json() as Promise<T>;
  } catch {
    return null;
  }
}

async function mutateXenia(path: string, token: string, init?: RequestInit): Promise<void> {
  const res = await fetch(`${XENIA_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(init?.headers as Record<string, string> | undefined),
    },
    cache: 'no-store',
  });

  if (!res.ok) {
    const message = await res.text().catch(() => 'unknown error');
    throw new Error(`Xenia API ${path} returned ${res.status}: ${message}`);
  }
}

export async function getXeniaInfo(): Promise<XeniaServiceInfo | null> {
  return fetchXenia<XeniaServiceInfo>('/info');
}

export async function getXeniaHealth(): Promise<XeniaHealthResponse | null> {
  return fetchXenia<XeniaHealthResponse>('/health');
}

export async function getXeniaReady(): Promise<XeniaReadyResponse | null> {
  return fetchXenia<XeniaReadyResponse>('/ready');
}

export async function getXeniaModules(token: string): Promise<XeniaModuleDto[]> {
  const res = await fetchXenia<{ modules: XeniaModuleDto[]; total: number }>(
    '/modules',
    token,
  );
  return res?.modules ?? [];
}

export async function getXeniaAdapters(token: string): Promise<XeniaAdapterDto[]> {
  const res = await fetchXenia<{ adapters: XeniaAdapterDto[]; total: number }>(
    '/adapters',
    token,
  );
  return res?.adapters ?? [];
}

// ── Automation types ────────────────────────────────────────────────

export interface XeniaAutomationManifest {
  automationKey: string;
  displayName: string;
  description: string;
  version: string;
  category: string;
  provider: string;
  status: string;
  capabilities: number;
  dependencies: XeniaAutomationDependency[];
  permissions: string[];
  configurationNamespace: string;
  supportedTriggers: string[];
  tenantEnablementSupported: boolean;
  schedulingSupported: boolean;
  diagnosticsSupported: boolean;
  healthSupported: boolean;
  minimumPlatformVersion: string;
  metadataVersion: number;
}

export interface XeniaAutomationDependency {
  key: string;
  dependencyType: string;
  criticality: string;
  availabilityState: string;
  configurationState: string | null;
  healthImpact: string | null;
  isOptional: boolean;
}

export interface XeniaAutomationRuntimeState {
  automationKey: string;
  automationVersion: string;
  tenantId: string | null;
  globalState: string;
  tenantState: string | null;
  effectiveState: string;
  activeExecutions: number;
  totalExecutions: number;
  failedExecutions: number;
  lastExecutedAt: string | null;
  lastSucceededAt: string | null;
  lastSafeError: string | null;
}

export interface XeniaAutomationDiagnosticsSnapshot {
  generatedAt: string;
  serviceVersion: string;
  environment: string;
  registrations: XeniaAutomationRegistryEntry[];
  workers: XeniaAutomationWorkerStatus[];
  dependencies: XeniaAutomationDependencyStatus[];
  activeExecutions: number;
  deadLetterCount: number;
}

export interface XeniaAutomationRegistryEntry {
  automationKey: string;
  version: string;
  provider: string;
  effectiveState: string;
  activeExecutions: number;
  totalExecutions: number;
  failedExecutions: number;
  lastExecutedAt: string | null;
  lastSafeError: string | null;
}

export interface XeniaAutomationWorkerStatus {
  name: string;
  isRunning: boolean;
  lastRunAt: string | null;
  safeStatus: string | null;
}

export interface XeniaAutomationDependencyStatus {
  key: string;
  dependencyType: string;
  criticality: string;
  availabilityState: string;
  isConfigured: boolean;
}

export interface XeniaDeadLetterEntry {
  id: string;
  automationKey: string;
  automationVersion: string;
  executionId: string;
  triggerType: string;
  failureCategory: string;
  safeErrorSummary: string;
  retryCount: number;
  firstFailedAt: string;
  lastFailedAt: string;
  status: string;
  tenantId: string | null;
}

export interface XeniaConfigurationEntry {
  id: string;
  scopeType: string;
  scopeId: string | null;
  namespace: string;
  configurationKey: string;
  configurationValue: string | null;
  valueType: string | null;
  isSecret: boolean;
  version: number;
  updatedAtUtc: string;
}

export interface XeniaAssistantUsageRow {
  tenantId: string;
  agentKey: string;
  provider: string;
  modelKey: string;
  requests: number;
  inputTokens: number;
  outputTokens: number;
  estimatedCostUsd: number;
  averageLatencyMs: number;
}

export interface XeniaAssistantAdminSettings {
  provider: string;
  modelKey: string;
  openAiBaseUrl: string;
  openAiApiKeyConfigured: boolean;
  openAiTimeoutSeconds: number;
  openAiReasoningEffort: string | null;
  openAiTextVerbosity: string | null;
  openAiMaxOutputTokens: number | null;
  lastUpdatedAtUtc: string | null;
}

export interface UpdateXeniaAssistantAdminSettingsPayload {
  provider: string;
  modelKey: string;
  openAiBaseUrl: string;
  openAiTimeoutSeconds: number;
  openAiReasoningEffort: string | null;
  openAiTextVerbosity: string | null;
  openAiMaxOutputTokens: number | null;
}

// ── Automation API helpers ──────────────────────────────────────────

export async function getXeniaAutomations(token: string): Promise<XeniaAutomationManifest[]> {
  const res = await fetchXenia<{ items: XeniaAutomationManifest[]; totalCount: number }>(
    '/api/v1/automation',
    token,
  );
  return res?.items ?? [];
}

export async function getXeniaAutomationDiagnostics(token: string): Promise<XeniaAutomationDiagnosticsSnapshot | null> {
  return fetchXenia<XeniaAutomationDiagnosticsSnapshot>('/api/v1/automation-diagnostics/snapshot', token);
}

export async function getXeniaDeadLetterEntries(token: string): Promise<XeniaDeadLetterEntry[]> {
  const res = await fetchXenia<{ items: XeniaDeadLetterEntry[] }>(
    '/api/v1/automation-dlq',
    token,
  );
  return res?.items ?? [];
}

export async function getXeniaAssistantGlobalConfig(token: string): Promise<XeniaConfigurationEntry[]> {
  const res = await fetchXenia<{ entries: XeniaConfigurationEntry[] }>(
    '/admin/config/global',
    token,
  );
  return res?.entries ?? [];
}

export async function getXeniaAssistantAdminSettings(token: string): Promise<XeniaAssistantAdminSettings | null> {
  return fetchXenia<XeniaAssistantAdminSettings>('/admin/settings', token);
}

export async function updateXeniaAssistantAdminSettings(
  token: string,
  payload: UpdateXeniaAssistantAdminSettingsPayload,
): Promise<void> {
  await mutateXenia('/admin/settings', token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });
}

export async function getXeniaAssistantUsage(token: string): Promise<XeniaAssistantUsageRow[]> {
  const res = await fetchXenia<{ usage: XeniaAssistantUsageRow[] }>(
    '/admin/usage',
    token,
  );
  return res?.usage ?? [];
}
