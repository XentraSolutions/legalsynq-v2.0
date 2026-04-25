import { serverApi } from '@/lib/server-api-client';

// ── Types ─────────────────────────────────────────────────────────────────────

export type TicketStatus   = 'Open' | 'Pending' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';
export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type TicketSource   = 'Portal' | 'Email' | 'Chat' | 'Phone' | 'Monitoring' | 'External';

export interface TicketSummary {
  id:              string;
  tenantId:        string;
  productCode?:    string;
  ticketNumber:    string;
  title:           string;
  description?:    string;
  status:          TicketStatus;
  priority:        TicketPriority;
  category?:       string;
  source:          TicketSource;
  requesterName?:  string;
  requesterEmail?: string;
  assignedUserId?: string;
  assignedQueueId?: string;
  dueAt?:          string;
  resolvedAt?:     string;
  closedAt?:       string;
  createdAt:       string;
  updatedAt:       string;
}

export interface TicketPagedResponse {
  items:    TicketSummary[];
  page:     number;
  pageSize: number;
  total:    number;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions ONLY.
// Reads the platform_session cookie and calls the gateway directly.
// DO NOT import this in Client Components — use the BFF proxy at /api/support/* instead.

export const supportServerApi = {
  tickets: {
    list: (params: {
      page?:     number;
      pageSize?: number;
      status?:   string;
      priority?: string;
      search?:   string;
    } = {}) =>
      serverApi.get<TicketPagedResponse>(
        `/support/api/tickets${toQs({ page: params.page ?? 1, pageSize: params.pageSize ?? 25, ...params })}`,
      ),

    getById: (id: string) =>
      serverApi.get<TicketSummary>(`/support/api/tickets/${encodeURIComponent(id)}`),
  },
};
