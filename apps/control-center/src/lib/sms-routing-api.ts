/**
 * LS-NOTIF-SMS-014: Control Center API client for SMS Routing admin endpoints.
 * All endpoints require PlatformAdmin role.
 * No credentials, CredentialsJson, SettingsJson, auth tokens, or raw phone numbers
 * are returned by these endpoints.
 */

const API_BASE = '/api';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface SmsProviderCapability {
  providerType: string;
  displayName: string;
  supportsSend: boolean;
  supportsStatusLookup: boolean;
  supportsHealthCheck: boolean;
  supportsCostEstimate: boolean;
  supportsRegionalRouting: boolean;
  supportsTenantOwnedConfig: boolean;
  supportsPlatformConfig: boolean;
  supportedCountries: string | null;
  defaultCurrency: string | null;
  notes: string | null;
}

export interface SmsRoutingPolicy {
  id: string;
  tenantId: string | null;
  name: string;
  enabled: boolean;
  region: string | null;
  countryCode: string | null;
  routingMode: string;
  preferredProvidersJson: string | null;
  excludedProvidersJson: string | null;
  maxEstimatedCostPerMessage: number | null;
  requireHealthyProvider: boolean;
  fallbackToPlatform: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface SmsRoutingPolicyListResult {
  items: SmsRoutingPolicy[];
  total: number;
  limit: number;
  offset: number;
}

export interface CreateSmsRoutingPolicyRequest {
  tenantId?: string;
  name: string;
  enabled: boolean;
  region?: string;
  countryCode?: string;
  routingMode: string;
  preferredProvidersJson?: string;
  excludedProvidersJson?: string;
  maxEstimatedCostPerMessage?: number;
  requireHealthyProvider: boolean;
  fallbackToPlatform: boolean;
  priority: number;
}

export interface SmsRoutingDecision {
  id: string;
  tenantId: string | null;
  notificationId: string | null;
  attemptId: string | null;
  routingPolicyId: string | null;
  routingMode: string;
  selectedProvider: string;
  selectedProviderConfigId: string | null;
  providerOwnershipMode: string | null;
  candidateProvidersJson: string | null;
  excludedProvidersJson: string | null;
  decisionReason: string;
  estimatedCostAmount: number | null;
  costCurrency: string | null;
  region: string | null;
  countryCode: string | null;
  createdAt: string;
}

export interface SmsRoutingDecisionListResult {
  items: SmsRoutingDecision[];
  total: number;
  limit: number;
  offset: number;
}

export interface SmsRoutingDecisionSummary {
  totalDecisions: number;
  byMode: Record<string, number>;
  byProvider: Record<string, number>;
  priorityModeCount: number;
  costOptimizedCount: number;
  healthOptimizedCount: number;
  hybridCount: number;
  regionalCount: number;
  noRouteCount: number;
}

export interface SmsProviderHealth {
  providerType: string;
  ownershipMode: string | null;
  providerConfigId: string | null;
  healthStatus: string;
  latencyMs: number | null;
  checkedAt: string | null;
}

// ── API functions ─────────────────────────────────────────────────────────────

async function fetchAdmin<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`SMS Routing API error ${res.status}: ${text}`);
  }
  return res.json() as Promise<T>;
}

export async function getSmsProviderCapabilities(): Promise<{ items: SmsProviderCapability[]; total: number }> {
  return fetchAdmin('/notifications/v1/admin/sms/routing/capabilities');
}

export async function getSmsRoutingPolicies(params?: {
  tenantId?: string;
  enabled?: boolean;
  routingMode?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsRoutingPolicyListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.enabled != null) q.set('enabled', String(params.enabled));
  if (params?.routingMode) q.set('routingMode', params.routingMode);
  if (params?.limit)       q.set('limit',       String(params.limit));
  if (params?.offset)      q.set('offset',      String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies?${q}`);
}

export async function getSmsRoutingPolicy(id: string): Promise<SmsRoutingPolicy> {
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies/${id}`);
}

export async function createSmsRoutingPolicy(body: CreateSmsRoutingPolicyRequest): Promise<SmsRoutingPolicy> {
  return fetchAdmin('/notifications/v1/admin/sms/routing/policies', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function updateSmsRoutingPolicy(id: string, body: Omit<CreateSmsRoutingPolicyRequest, 'tenantId'>): Promise<SmsRoutingPolicy> {
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export async function disableSmsRoutingPolicy(id: string): Promise<{ message: string; policy: SmsRoutingPolicy }> {
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies/${id}/disable`, { method: 'POST' });
}

export async function getSmsRoutingDecisions(params?: {
  tenantId?: string;
  provider?: string;
  routingMode?: string;
  policyId?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsRoutingDecisionListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.provider)    q.set('provider',    params.provider);
  if (params?.routingMode) q.set('routingMode', params.routingMode);
  if (params?.policyId)    q.set('policyId',    params.policyId);
  if (params?.limit)       q.set('limit',       String(params.limit));
  if (params?.offset)      q.set('offset',      String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/routing/decisions?${q}`);
}

export async function getSmsRoutingDecisionSummary(params?: {
  tenantId?: string;
  provider?: string;
}): Promise<SmsRoutingDecisionSummary> {
  const q = new URLSearchParams();
  if (params?.tenantId) q.set('tenantId', params.tenantId);
  if (params?.provider) q.set('provider', params.provider);
  return fetchAdmin(`/notifications/v1/admin/sms/routing/decisions/summary?${q}`);
}

export async function getSmsProviderHealth(): Promise<{ items: SmsProviderHealth[]; total: number }> {
  return fetchAdmin('/notifications/v1/admin/sms/routing/providers/health');
}
