import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5000';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface NotifRecipient {
  email?:   string;
  phone?:   string;
  address?: string;
}

export interface NotifSummary {
  id:                string;
  tenantId:          string;
  channel:           string;
  status:            string;
  recipientJson:     string;
  providerUsed:      string | null;
  lastErrorMessage:  string | null;
  failureCategory:   string | null;
  metadataJson:      string | null;
  createdAt:         string;
  updatedAt:         string;
}

export interface NotifListResponse {
  data: NotifSummary[];
  meta: { total: number; limit: number; offset: number };
}

export interface NotifStats {
  total:    number;
  byStatus: Record<string, number>;
  byChannel: Record<string, number>;
  last24h:  { total: number; sent: number; failed: number; blocked: number };
  last7d:   { total: number; sent: number; failed: number; blocked: number };
}

// ── Core request ─────────────────────────────────────────────────────────────

/**
 * Tenant-scoped server-side fetch helper for the Notifications service.
 *
 * Attaches both the session Bearer token (for gateway auth) and the
 * x-tenant-id header (for notifications service tenant isolation).
 *
 * Call from Server Components only — never from Client Components.
 */
async function notifRequest<T>(path: string, tenantId: string): Promise<T> {
  const cookieStore = cookies();
  const token = cookieStore.get('platform_session')?.value;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'x-tenant-id': tenantId,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${GATEWAY_URL}/notifications${path}`, {
    method: 'GET',
    headers,
    cache: 'no-store',
  });

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const err = await res.json() as Record<string, unknown>;
      message = (err.message as string) ?? (err.title as string) ?? message;
    } catch { /* ignore non-JSON */ }
    throw new Error(message);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Public client ─────────────────────────────────────────────────────────────

/**
 * Notifications API client scoped to a specific tenant.
 * All methods require the caller to supply the tenantId from their session.
 */
export const notificationsServerApi = {
  /**
   * GET /v1/notifications — paginated list of notifications for this tenant.
   * Accepts status, channel filters and limit/offset pagination.
   */
  list(tenantId: string, params: {
    status?:  string;
    channel?: string;
    limit?:   number;
    offset?:  number;
  } = {}): Promise<NotifListResponse> {
    const qs = new URLSearchParams();
    if (params.status)             qs.set('status',  params.status);
    if (params.channel)            qs.set('channel', params.channel);
    if (params.limit  !== undefined) qs.set('limit',  String(params.limit));
    if (params.offset !== undefined) qs.set('offset', String(params.offset));
    const q = qs.toString();
    return notifRequest<NotifListResponse>(`/v1/notifications${q ? `?${q}` : ''}`, tenantId);
  },

  /**
   * GET /v1/notifications/stats — delivery statistics for this tenant.
   */
  stats(tenantId: string): Promise<{ data: NotifStats }> {
    return notifRequest<{ data: NotifStats }>('/v1/notifications/stats', tenantId);
  },
};

// ── Helpers ───────────────────────────────────────────────────────────────────

export function parseRecipient(recipientJson: string): string {
  try {
    const r = JSON.parse(recipientJson) as NotifRecipient;
    return r.email ?? r.phone ?? r.address ?? '—';
  } catch {
    return '—';
  }
}

export type NotifStatus  = 'accepted' | 'processing' | 'sent' | 'failed' | 'blocked';
export type NotifChannel = 'email' | 'sms' | 'push' | 'in-app';

export const NOTIF_STATUS_OPTIONS:  Array<{ value: string; label: string }> = [
  { value: '',           label: 'All statuses'  },
  { value: 'accepted',   label: 'Accepted'      },
  { value: 'processing', label: 'Processing'    },
  { value: 'sent',       label: 'Sent'          },
  { value: 'failed',     label: 'Failed'        },
  { value: 'blocked',    label: 'Blocked'       },
];

export const NOTIF_CHANNEL_OPTIONS: Array<{ value: string; label: string }> = [
  { value: '',       label: 'All channels' },
  { value: 'email',  label: 'Email'        },
  { value: 'sms',    label: 'SMS'          },
  { value: 'push',   label: 'Push'         },
  { value: 'in-app', label: 'In-App'       },
];
