const XENIA_BASE = process.env.XENIA_URL ?? 'http://127.0.0.1:5035';

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
      ...((init?.headers ?? {}) as Record<string, string>),
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`Xenia ${path} → ${res.status}: ${text}`);
  }
  if (res.status === 204) return undefined as unknown;
  return res.json();
}

export async function getEmailSources(
  token: string,
): Promise<{ sources: EmailSource[]; total: number }> {
  return xeniaFetch('/api/v1/email/sources', token);
}

export async function getEmailSource(token: string, id: string): Promise<EmailSource> {
  return xeniaFetch(`/api/v1/email/sources/${id}`, token);
}

export async function createEmailSource(
  token: string,
  payload: CreateEmailSourcePayload,
): Promise<EmailSource> {
  return xeniaFetch('/api/v1/email/sources', token, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function updateEmailSource(
  token: string,
  id: string,
  payload: UpdateEmailSourcePayload,
): Promise<EmailSource> {
  return xeniaFetch(`/api/v1/email/sources/${id}`, token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });
}

export async function deleteEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/api/v1/email/sources/${id}`, token, { method: 'DELETE' });
}

export async function enableEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/api/v1/email/sources/${id}/enable`, token, { method: 'PUT', body: '{}' });
}

export async function disableEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/api/v1/email/sources/${id}/disable`, token, { method: 'PUT', body: '{}' });
}

export async function validateEmailSource(
  token: string,
  id: string,
): Promise<EmailValidationResult> {
  return xeniaFetch(`/api/v1/email/sources/${id}/validate`, token, { method: 'POST', body: '{}' });
}

export async function getValidationHistory(
  token: string,
  id: string,
  limit = 10,
): Promise<{ history: ValidationHistoryEntry[] }> {
  return xeniaFetch(`/api/v1/email/sources/${id}/validation-history?limit=${limit}`, token);
}

export async function getEmailProviders(
  token: string,
): Promise<{ providers: EmailProviderDefinition[] }> {
  return xeniaFetch('/api/v1/email/providers', token);
}
