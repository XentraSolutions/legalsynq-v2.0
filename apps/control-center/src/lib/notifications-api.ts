/**
 * notifications-api.ts — Platform-wide HTTP client for the Notifications service.
 *
 * Notifications are platform-scoped — no x-tenant-id header is sent.
 * This module wraps fetch() for server-side use with Bearer auth from
 * the active platform_session cookie.
 *
 * Base path through the Gateway: /notifications/v1/...
 */

import { redirect }                  from 'next/navigation';
import { cookies }                   from 'next/headers';
import { logInfo, logWarn, logError } from '@/lib/logger';
import { CONTROL_CENTER_API_BASE }   from '@/lib/env';
import { ApiError }                  from '@/lib/api-client';

const API_BASE = CONTROL_CENTER_API_BASE;
const NOTIF_PREFIX = '/notifications/v1';

export interface NotifFetchOptions {
  method?:            string;
  body?:              unknown;
  revalidateSeconds?: number;
  tags?:              string[];
}

export async function notifFetch<T>(
  path:    string,
  options: NotifFetchOptions = {},
): Promise<T> {
  const requestId = crypto.randomUUID();
  const method    = options.method ?? 'GET';

  const cookieStore = cookies();
  const token = cookieStore.get('platform_session')?.value;

  if (!token) {
    logWarn('security.session.missing_token', { requestId, method, endpoint: path });
    redirect('/login?reason=session_expired');
  }

  const headers: Record<string, string> = {
    'Content-Type':  'application/json',
    'Accept':        'application/json',
    'X-Request-Id':  requestId,
    'Authorization': `Bearer ${token}`,
  };

  const isRead = method === 'GET' || method === 'HEAD';
  let fetchCache: RequestCache | undefined;
  let nextOptions: { revalidate?: number; tags?: string[] } | undefined;

  if (isRead && options.revalidateSeconds !== undefined) {
    nextOptions = {
      revalidate: options.revalidateSeconds,
      ...(options.tags && options.tags.length > 0 ? { tags: options.tags } : {}),
    };
  } else {
    fetchCache = 'no-store';
  }

  logInfo('notif.api.request.start', { requestId, method, endpoint: path });

  const startMs = Date.now();
  let res: Response;

  try {
    res = await fetch(`${API_BASE}${NOTIF_PREFIX}${path}`, {
      method,
      headers,
      body:    options.body !== undefined ? JSON.stringify(options.body) : undefined,
      ...(fetchCache  ? { cache: fetchCache } : {}),
      ...(nextOptions ? { next:  nextOptions } : {}),
    });
  } catch (networkErr: unknown) {
    const durationMs = Date.now() - startMs;
    logError('notif.api.network_failure', networkErr, { requestId, method, endpoint: path, durationMs });
    throw networkErr;
  }

  const durationMs = Date.now() - startMs;

  if (res.status === 401) {
    logInfo('notif.api.redirect_401', { requestId, durationMs, status: 401 });
    redirect('/login?reason=session_expired');
  }

  if (!res.ok) {
    let message = `HTTP ${res.status} ${res.statusText}`;
    try {
      const errBody = await res.json() as Record<string, unknown>;
      if (typeof errBody.message === 'string') message = errBody.message;
      else if (typeof errBody.title === 'string') message = errBody.title;
      else if (typeof errBody.error === 'string') message = errBody.error;
    } catch { /* non-JSON error body — use status text */ }
    const apiErr = new ApiError(res.status, message);
    logError('notif.api.request.error', apiErr, { requestId, method, endpoint: path, durationMs, status: res.status });
    throw apiErr;
  }

  logInfo('notif.api.request.success', { requestId, method, endpoint: path, durationMs, status: res.status });

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const NOTIF_CACHE_TAGS = {
  notifications: 'notif:notifications',
  templates:     'notif:templates',
  providers:     'notif:providers',
  billing:       'notif:billing',
  contacts:      'notif:contacts',
} as const;

export const notifClient = {
  get:   <T>(path: string, revalidateSeconds?: number, tags?: string[]) =>
    notifFetch<T>(path, { revalidateSeconds, tags }),
  post:  <T>(path: string, body: unknown) =>
    notifFetch<T>(path, { method: 'POST',  body }),
  patch: <T>(path: string, body: unknown) =>
    notifFetch<T>(path, { method: 'PATCH', body }),
  put:   <T>(path: string, body: unknown) =>
    notifFetch<T>(path, { method: 'PUT',   body }),
  del:   <T>(path: string) =>
    notifFetch<T>(path, { method: 'DELETE' }),
};

// ── Frontend shape types ───────────────────────────────────────────────────────

export type NotifChannel = 'email' | 'sms' | 'push' | 'in-app';
export type NotifStatus  = 'accepted' | 'processing' | 'sent' | 'failed' | 'blocked';
export type NotifFailureCategory =
  | 'retryable_provider_failure'
  | 'non_retryable_failure'
  | 'provider_unavailable'
  | 'invalid_recipient'
  | 'auth_config_failure';

export interface NotifSummary {
  id:               string;
  tenantId:         string;
  channel:          NotifChannel;
  status:           NotifStatus;
  recipientJson:    string;
  providerUsed:     string | null;
  failureCategory:  NotifFailureCategory | null;
  lastErrorMessage: string | null;
  templateKey:      string | null;
  renderedSubject:  string | null;
  blockedByPolicy:  boolean;
  blockedReasonCode: string | null;
  platformFallbackUsed: boolean;
  createdAt:        string;
  updatedAt:        string;
}

export interface NotifDetail extends NotifSummary {
  messageJson:     string;
  metadataJson:    string | null;
  idempotencyKey:  string | null;
  templateId:      string | null;
  renderedSubject: string | null;
  renderedBody:    string | null;
}

export interface NotifEvent {
  id:        string;
  eventType: string;
  occurredAt: string;
  metadata:  Record<string, unknown> | null;
}

export interface NotifIssue {
  id:           string;
  issueType:    string;
  severity:     string;
  message:      string;
  occurredAt:   string;
  resolvedAt:   string | null;
}

export interface NotifListResponse {
  data:  NotifSummary[];
  meta: {
    total:  number;
    limit:  number;
    offset: number;
  };
}

export interface NotifStats {
  total:     number;
  byStatus:  Record<string, number>;
  byChannel: Record<string, number>;
  last24h: { total: number; sent: number; failed: number; blocked: number };
  last7d:  { total: number; sent: number; failed: number; blocked: number };
}

export interface NotifTemplate {
  id:               string;
  tenantId:         string | null;
  channel:          NotifChannel;
  templateKey:      string;
  name:             string;
  description:      string | null;
  status:           'active' | 'inactive' | 'archived';
  currentVersionId: string | null;
  createdAt:        string;
  updatedAt:        string;
}

export interface NotifTemplateVersion {
  id:              string;
  templateId:      string;
  versionNumber:   number;
  subjectTemplate: string | null;
  bodyHtml:        string | null;
  bodyText:        string | null;
  variables:       string[] | null;
  status:          'draft' | 'published' | 'archived';
  publishedAt:     string | null;
  createdAt:       string;
}

export interface NotifProviderConfig {
  id:               string;
  tenantId:         string | null;
  providerType:     string;
  channel:          NotifChannel;
  ownershipMode:    string;
  displayName?:     string | null;
  status:           'active' | 'inactive';
  validationStatus: 'not_validated' | 'valid' | 'invalid';
  healthStatus:     string | null;
  createdAt:        string;
  updatedAt:        string;
}

export interface NotifCatalogProvider {
  providerType:  string;
  channel:       NotifChannel;
  displayName:   string;
}

export interface NotifChannelSetting {
  channel:                       NotifChannel;
  primaryProvider:               string | null;
  fallbackProvider:              string | null;
  primaryTenantProviderConfigId?:   string | null;
  fallbackTenantProviderConfigId?:  string | null;
  mode:                          string;
  providerMode?:                 string;
  allowPlatformFallback?:        boolean;
  allowAutomaticFailover?:       boolean;
  updatedAt:                     string | null;
}

export interface NotifProviderHealth {
  id:            string;
  tenantId:      string | null;
  channel:       NotifChannel;
  provider:      string;
  status:        'healthy' | 'degraded' | 'down';
  failureCount:  number;
  lastCheckedAt: string | null;
  lastFailureAt: string | null;
}

export interface NotifUsageSummary {
  period?:   string;
  totals:    Record<string, number>;
  byChannel: Record<string, Record<string, number>>;
}

export interface NotifUsageEvent {
  id:        string;
  unit:      string;
  quantity:  number;
  channel:   NotifChannel | null;
  provider:  string | null;
  occurredAt: string;
}

export interface NotifBillingPlan {
  id:            string;
  tenantId:      string;
  name:          string;
  mode:          string;
  status:        string;
  currency?:     string | null;
  effectiveFrom?: string | null;
  effectiveTo?:  string | null;
  createdAt:     string;
}

export interface NotifBillingRate {
  id:                   string;
  planId:               string;
  usageUnit:            string;
  channel:              NotifChannel | null;
  providerOwnershipMode: string | null;
  includedQuantity:     number | null;
  unitPrice:            number | null;
  isBillable:           boolean;
  createdAt:            string;
}

export interface NotifRateLimitPolicy {
  id:        string;
  tenantId:  string;
  channel:   NotifChannel | null;
  limitCount: number;
  windowSeconds: number;
  status:    string;
  createdAt: string;
}

export interface NotifSuppression {
  id:              string;
  tenantId:        string;
  channel:         NotifChannel;
  contactValue:    string;
  suppressionType: string;
  source:          string;
  status:          'active' | 'expired' | 'lifted';
  reason:          string | null;
  expiresAt:       string | null;
  createdAt:       string;
  updatedAt:       string;
}

export interface NotifContactHealth {
  id:           string;
  channel:      NotifChannel;
  contactValue: string;
  status:       string;
  lastEvent:    string | null;
  lastEventAt:  string | null;
  updatedAt:    string;
}

export interface NotifContactPolicy {
  id:        string;
  tenantId:  string;
  channel:   NotifChannel | null;
  policyType: string;
  config:    Record<string, unknown>;
  status:    string;
  createdAt: string;
}
