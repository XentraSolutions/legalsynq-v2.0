'server-only';

import { cookies } from 'next/headers';

/**
 * notif-web-api.ts — Server-side Notifications service client for the web app's
 * embedded Control Center.
 *
 * Uses the same base URL as the standalone Control Center (CONTROL_CENTER_API_BASE
 * or GATEWAY_URL as a legacy alias — see apps/control-center/src/lib/env.ts).
 *
 * Injects:
 *   Authorization: Bearer <platform_session>
 *   x-tenant-id:   <cc_tenant_context>   (required by notifications service)
 *
 * Path convention (matches notifications-api.ts in the standalone CC):
 *   /notifications/v1/<resource>
 */

const GATEWAY_URL = (
  process.env.CONTROL_CENTER_API_BASE ??
  process.env.GATEWAY_URL             ??
  'http://localhost:5010'
);

const NOTIF_PREFIX = '/notifications/v1';

export class NotifWebError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'NotifWebError';
  }
}

async function notifRequest<T>(path: string, options: { method?: string; body?: unknown } = {}): Promise<T> {
  const cookieStore = cookies();
  const token  = cookieStore.get('platform_session')?.value;

  // cc_tenant_context stores JSON: { tenantId, tenantName, tenantCode }
  let tenantId: string | undefined;
  const raw = cookieStore.get('cc_tenant_context')?.value;
  if (raw) {
    try {
      const ctx = JSON.parse(raw) as { tenantId?: string };
      tenantId  = ctx.tenantId;
    } catch {
      tenantId = raw; // fallback if somehow stored as plain UUID
    }
  }

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token)    headers['Authorization'] = `Bearer ${token}`;
  if (tenantId) headers['x-tenant-id']   = tenantId;

  const url = `${GATEWAY_URL}${NOTIF_PREFIX}${path}`;

  const res = await fetch(url, {
    method:  options.method ?? 'GET',
    headers,
    body:    options.body !== undefined ? JSON.stringify(options.body) : undefined,
    cache:   'no-store',
  });

  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try { const e = await res.json(); msg = e.message ?? e.title ?? msg; } catch { /* ignore */ }
    throw new NotifWebError(res.status, msg);
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

function unwrap<T>(r: T[] | { items: T[]; data?: T[] }): T[] {
  if (Array.isArray(r)) return r;
  const obj = r as { items?: T[]; data?: T[] };
  return obj.items ?? obj.data ?? [];
}

export const notifWebApi = {
  get:    <T>(path: string)               => notifRequest<T>(path),
  post:   <T>(path: string, body: unknown) => notifRequest<T>(path, { method: 'POST',  body }),
  put:    <T>(path: string, body: unknown) => notifRequest<T>(path, { method: 'PUT',   body }),
  patch:  <T>(path: string, body: unknown) => notifRequest<T>(path, { method: 'PATCH', body }),
  unwrap,
};
