'server-only';

import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5010';
const NOTIF_BASE  = '/api/v1/notifications';

export class NotifWebError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'NotifWebError';
  }
}

async function notifRequest<T>(path: string, options: { method?: string; body?: unknown } = {}): Promise<T> {
  const cookieStore = cookies();
  const token    = cookieStore.get('platform_session')?.value;
  const tenantId = cookieStore.get('cc_tenant_context')?.value;

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token)    headers['Authorization']  = `Bearer ${token}`;
  if (tenantId) headers['x-tenant-id']    = tenantId;

  const res = await fetch(`${GATEWAY_URL}${NOTIF_BASE}${path}`, {
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

function unwrap<T>(r: T[] | { items: T[] }): T[] {
  return Array.isArray(r) ? r : ((r as { items: T[] }).items ?? []);
}

export const notifWebApi = {
  get:   <T>(path: string)              => notifRequest<T>(path),
  post:  <T>(path: string, body: unknown) => notifRequest<T>(path, { method: 'POST',  body }),
  put:   <T>(path: string, body: unknown) => notifRequest<T>(path, { method: 'PUT',   body }),
  patch: <T>(path: string, body: unknown) => notifRequest<T>(path, { method: 'PATCH', body }),
  unwrap,
};
