const XENIA_BASE = process.env.XENIA_URL || 'http://127.0.0.1:5035';
const GATEWAY_BASE = process.env.CONTROL_CENTER_API_BASE || 'http://127.0.0.1:5010';

export interface EmailSource {
  id: string;
  tenantId: string;
  moduleKey: string;
  displayName: string;
  description?: string;
  providerType: string;
  authType: string;
  emailAddress: string;
  username?: string;
  incomingHost?: string;
  incomingPort?: number;
  useTls: boolean;
  mailboxFolder?: string;
  hasSecretReference: boolean;
  hasOAuthConnection: boolean;
  enabled: boolean;
  status: string;
  healthStatus: string;
  validationStatus: string;
  lastValidatedAt?: string;
  lastSuccessfulValidationAt?: string;
  lastValidationLatencyMs?: number;
  lastConnectionAt?: string;
  lastErrorCode?: string;
  lastErrorSummary?: string;
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface EmailProviderDefinition {
  providerKey: string;
  displayName: string;
  category: string;
  supportedAuthTypes: string[];
  defaultIncomingHost?: string;
  defaultPort?: number;
  requiresTls: boolean;
  supportsOAuth: boolean;
  supportsUsernamePassword: boolean;
  validationAvailable: boolean;
  helpText?: string;
}

export interface ValidationHistoryEntry {
  id: string;
  emailSourceId: string;
  providerType: string;
  validationType: string;
  startedAt: string;
  completedAt?: string;
  durationMs?: number;
  result: string;
  errorCode?: string;
  errorSummary?: string;
  createdAtUtc: string;
}

export interface EmailValidationResult {
  sourceId: string;
  success: boolean;
  result: string;
  durationMs: number;
  errorCode?: string;
  safeErrorSummary?: string;
  validatedAt: string;
}

export interface EmailModuleState {
  moduleKey: string;
  name: string;
  version: string;
  globalEnabled: boolean;
  tenantEnabled: boolean;
  effectiveEnabled: boolean;
  status: string;
}

export interface CreateEmailSourcePayload {
  displayName: string;
  description?: string;
  providerType: string;
  authType: string;
  emailAddress: string;
  username?: string;
  incomingHost?: string;
  incomingPort?: number;
  useTls: boolean;
  mailboxFolder?: string;
  secretReferenceId?: string;
  oauthConnectionRef?: string;
  enabled?: boolean;
  providerConfigurationJson?: string;
}

export interface UpdateEmailSourcePayload {
  displayName?: string;
  description?: string;
  username?: string;
  incomingHost?: string;
  incomingPort?: number;
  useTls?: boolean;
  mailboxFolder?: string;
  secretReferenceId?: string;
  oauthConnectionRef?: string;
  providerConfigurationJson?: string;
  expectedRowVersion?: number;
}

async function xeniaFetch(path: string, token: string, init?: RequestInit) {
  const res = await fetch(`${XENIA_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(init?.headers as Record<string, string> | undefined),
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => 'unknown error');
    throw new Error(`Xenia API ${path} → ${res.status}: ${text}`);
  }
  return res.json();
}

export async function getEmailModuleState(token: string): Promise<EmailModuleState> {
  return xeniaFetch('/email/module', token);
}

export async function enableEmailModule(token: string): Promise<void> {
  await xeniaFetch('/email/module/enable', token, { method: 'PUT' });
}

export async function disableEmailModule(token: string): Promise<void> {
  await xeniaFetch('/email/module/disable', token, { method: 'PUT' });
}

export async function getEmailSources(token: string): Promise<{ sources: EmailSource[]; total: number }> {
  return xeniaFetch('/email/sources', token);
}

export async function getEmailSource(token: string, id: string): Promise<EmailSource> {
  return xeniaFetch(`/email/sources/${id}`, token);
}

export async function createEmailSource(
  token: string,
  payload: CreateEmailSourcePayload,
): Promise<EmailSource> {
  return xeniaFetch('/email/sources', token, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function updateEmailSource(
  token: string,
  id: string,
  payload: UpdateEmailSourcePayload,
): Promise<EmailSource> {
  return xeniaFetch(`/email/sources/${id}`, token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });
}

export async function deleteEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/email/sources/${id}`, token, { method: 'DELETE' });
}

export async function enableEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/email/sources/${id}/enable`, token, { method: 'PUT' });
}

export async function disableEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/email/sources/${id}/disable`, token, { method: 'PUT' });
}

export async function validateEmailSource(token: string, id: string): Promise<EmailValidationResult> {
  return xeniaFetch(`/email/sources/${id}/validate`, token, { method: 'POST' });
}

export async function getValidationHistory(
  token: string,
  id: string,
  limit = 20,
): Promise<{ source_id: string; history: ValidationHistoryEntry[]; total: number }> {
  return xeniaFetch(`/email/sources/${id}/validation-history?limit=${limit}`, token);
}

export async function getEmailProviders(
  token: string,
): Promise<{ providers: EmailProviderDefinition[]; total: number }> {
  return xeniaFetch('/email/providers', token);
}

export async function getEmailProvider(token: string, key: string): Promise<EmailProviderDefinition> {
  return xeniaFetch(`/email/providers/${key}`, token);
}

// ── Email Settings ────────────────────────────────────────────────────────────

export interface EmailSettings {
  id: string;
  tenantId: string;
  connectionTimeoutSeconds: number;
  allowedProviderTypes: string;
  validationRetryLimit: number;
  validationHistoryRetentionDays: number;
  allowedPorts: string;
  requireTls: boolean;
  allowCustomHosts: boolean;
  ssrfPolicyMode: string;
  defaultSourceEnabled: boolean;
  version: number;
  updatedAtUtc: string;
}

export interface UpdateEmailSettingsPayload {
  connectionTimeoutSeconds?: number;
  allowedProviderTypes?: string;
  validationRetryLimit?: number;
  validationHistoryRetentionDays?: number;
  allowedPorts?: string;
  requireTls?: boolean;
  allowCustomHosts?: boolean;
  ssrfPolicyMode?: string;
  defaultSourceEnabled?: boolean;
  expectedVersion: number;
}

export async function getEmailSettings(token: string): Promise<EmailSettings> {
  return xeniaFetch('/email/settings', token);
}

export async function updateEmailSettings(
  token: string,
  payload: UpdateEmailSettingsPayload,
): Promise<EmailSettings> {
  return xeniaFetch('/email/settings', token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });
}
