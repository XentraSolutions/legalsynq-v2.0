import { serverApi } from '@/lib/server-api-client';
import type {
  TenantSummary,
  TenantDetail,
  TenantUserSummary,
  PagedResponse,
} from '@/types/control-center';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Mock data ─────────────────────────────────────────────────────────────────
// TODO: replace with GET /identity/api/admin/tenants when backend endpoint is ready.
// To wire the live endpoint, swap the list() stub below for:
//   serverApi.get<PagedResponse<TenantSummary>>(
//     `/identity/api/admin/tenants${toQs(params as Record<string, unknown>)}`,
//   )

const MOCK_TENANTS: TenantSummary[] = [
  {
    id: '11111111-0000-0000-0000-000000000001',
    code: 'HARTWELL',
    displayName: 'Hartwell & Associates',
    type: 'LawFirm',
    status: 'Active',
    primaryContactName: 'Margaret Hartwell',
    isActive: true,
    userCount: 14,
    orgCount: 2,
    createdAtUtc: '2024-02-15T08:30:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000002',
    code: 'MERIDIAN',
    displayName: 'Meridian Care Partners',
    type: 'Provider',
    status: 'Active',
    primaryContactName: 'Dr. Samuel Okafor',
    isActive: true,
    userCount: 32,
    orgCount: 5,
    createdAtUtc: '2024-03-01T10:00:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000003',
    code: 'PINNACLE',
    displayName: 'Pinnacle Legal Group',
    type: 'LawFirm',
    status: 'Active',
    primaryContactName: 'Reginald Moss',
    isActive: true,
    userCount: 8,
    orgCount: 1,
    createdAtUtc: '2024-04-10T14:15:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000004',
    code: 'BLUEHAVEN',
    displayName: 'Blue Haven Recovery Services',
    type: 'Provider',
    status: 'Inactive',
    primaryContactName: 'Tanya Bridges',
    isActive: false,
    userCount: 4,
    orgCount: 1,
    createdAtUtc: '2024-05-20T09:00:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000005',
    code: 'LEGALSYNQ',
    displayName: 'LegalSynq Platform',
    type: 'Corporate',
    status: 'Active',
    primaryContactName: 'Admin User',
    isActive: true,
    userCount: 3,
    orgCount: 1,
    createdAtUtc: '2024-01-01T00:00:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000006',
    code: 'THORNFIELD',
    displayName: 'Thornfield & Yuen LLP',
    type: 'LawFirm',
    status: 'Active',
    primaryContactName: 'Diana Yuen',
    isActive: true,
    userCount: 21,
    orgCount: 3,
    createdAtUtc: '2024-06-05T11:30:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000007',
    code: 'NEXUSHEALTH',
    displayName: 'Nexus Health Network',
    type: 'Provider',
    status: 'Active',
    primaryContactName: 'Carlos Reyes',
    isActive: true,
    userCount: 57,
    orgCount: 9,
    createdAtUtc: '2024-06-18T08:45:00Z',
  },
  {
    id: '11111111-0000-0000-0000-000000000008',
    code: 'GRAYSTONE',
    displayName: 'Graystone Municipal Services',
    type: 'Government',
    status: 'Suspended',
    primaryContactName: 'Patricia Langford',
    isActive: false,
    userCount: 6,
    orgCount: 1,
    createdAtUtc: '2024-07-02T13:00:00Z',
  },
];

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions only.
// Reads the platform_session cookie and calls the gateway directly (no extra hop).

export const controlCenterServerApi = {

  tenants: {
    // TODO: replace with GET /identity/api/admin/tenants when backend endpoint is ready.
    list: (params: { page?: number; pageSize?: number; search?: string } = {}) => {
      const page     = params.page     ?? 1;
      const pageSize = params.pageSize ?? 20;
      const search   = (params.search ?? '').toLowerCase();

      const filtered = search
        ? MOCK_TENANTS.filter(
            t =>
              t.displayName.toLowerCase().includes(search) ||
              t.code.toLowerCase().includes(search) ||
              t.primaryContactName.toLowerCase().includes(search),
          )
        : MOCK_TENANTS;

      const start = (page - 1) * pageSize;
      const items = filtered.slice(start, start + pageSize);

      return Promise.resolve<PagedResponse<TenantSummary>>({
        items,
        totalCount: filtered.length,
        page,
        pageSize,
      });
    },

    // TODO: replace with GET /identity/api/admin/tenants/:id
    getById: (id: string) =>
      serverApi.get<TenantDetail>(`/identity/api/admin/tenants/${id}`),
  },

  users: {
    // TODO: replace with GET /identity/api/admin/users
    list: (params: { tenantId?: string; page?: number; pageSize?: number; search?: string } = {}) =>
      serverApi.get<PagedResponse<TenantUserSummary>>(
        `/identity/api/admin/users${toQs(params as Record<string, unknown>)}`,
      ),
  },
};
