import { serverApi } from '@/lib/server-api-client';
import type {
  TenantSummary,
  TenantDetail,
  UserSummary,
  ProductEntitlementSummary,
  EntitlementStatus,
  PagedResponse,
  TenantType,
} from '@/types/control-center';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Mock tenant data ───────────────────────────────────────────────────────────
// TODO: replace with GET /identity/api/admin/tenants when backend endpoint is ready.

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

// ── Mock user data ─────────────────────────────────────────────────────────────
// TODO: replace with GET /identity/api/admin/users when backend endpoint is ready.

const MOCK_USERS: UserSummary[] = [
  // HARTWELL — LawFirm (Active)
  { id: 'u-001', firstName: 'Margaret',  lastName: 'Hartwell',  email: 'margaret@hartwell.law',      role: 'TenantAdmin',      status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000001', tenantCode: 'HARTWELL',    lastLoginAtUtc: '2025-03-28T09:15:00Z' },
  { id: 'u-002', firstName: 'James',     lastName: 'Whitmore',  email: 'j.whitmore@hartwell.law',    role: 'Attorney',         status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000001', tenantCode: 'HARTWELL',    lastLoginAtUtc: '2025-03-27T14:30:00Z' },
  { id: 'u-003', firstName: 'Olivia',    lastName: 'Chen',      email: 'o.chen@hartwell.law',        role: 'CaseManager',      status: 'Invited',  tenantId: '11111111-0000-0000-0000-000000000001', tenantCode: 'HARTWELL' },

  // MERIDIAN — Provider (Active)
  { id: 'u-004', firstName: 'Samuel',    lastName: 'Okafor',    email: 'dr.okafor@meridiancare.com', role: 'TenantAdmin',      status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000002', tenantCode: 'MERIDIAN',    lastLoginAtUtc: '2025-03-28T08:00:00Z' },
  { id: 'u-005', firstName: 'Priya',     lastName: 'Nair',      email: 'p.nair@meridiancare.com',    role: 'CareCoordinator',  status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000002', tenantCode: 'MERIDIAN',    lastLoginAtUtc: '2025-03-26T11:45:00Z' },
  { id: 'u-006', firstName: 'Derek',     lastName: 'Fontaine',  email: 'd.fontaine@meridiancare.com',role: 'BillingManager',   status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000002', tenantCode: 'MERIDIAN',    lastLoginAtUtc: '2025-03-25T16:00:00Z' },
  { id: 'u-007', firstName: 'Amara',     lastName: 'Diallo',    email: 'a.diallo@meridiancare.com',  role: 'CareCoordinator',  status: 'Inactive', tenantId: '11111111-0000-0000-0000-000000000002', tenantCode: 'MERIDIAN',    lastLoginAtUtc: '2024-12-10T09:00:00Z' },

  // PINNACLE — LawFirm (Active)
  { id: 'u-008', firstName: 'Reginald',  lastName: 'Moss',      email: 'r.moss@pinnaclelegal.com',   role: 'TenantAdmin',      status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000003', tenantCode: 'PINNACLE',    lastLoginAtUtc: '2025-03-28T10:00:00Z' },
  { id: 'u-009', firstName: 'Claire',    lastName: 'Hutchings', email: 'c.hutchings@pinnaclelegal.com', role: 'Attorney',      status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000003', tenantCode: 'PINNACLE',    lastLoginAtUtc: '2025-03-27T13:00:00Z' },

  // BLUEHAVEN — Provider (Inactive)
  { id: 'u-010', firstName: 'Tanya',     lastName: 'Bridges',   email: 'tanya@bluehavenrecovery.org',role: 'TenantAdmin',      status: 'Inactive', tenantId: '11111111-0000-0000-0000-000000000004', tenantCode: 'BLUEHAVEN',   lastLoginAtUtc: '2024-09-01T08:00:00Z' },

  // LEGALSYNQ — Corporate (Active)
  { id: 'u-011', firstName: 'Admin',     lastName: 'User',      email: 'admin@legalsynq.com',        role: 'PlatformAdmin',    status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000005', tenantCode: 'LEGALSYNQ',   lastLoginAtUtc: '2025-03-29T07:00:00Z' },
  { id: 'u-012', firstName: 'Nina',      lastName: 'Patel',     email: 'n.patel@legalsynq.com',      role: 'PlatformAdmin',    status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000005', tenantCode: 'LEGALSYNQ',   lastLoginAtUtc: '2025-03-28T17:30:00Z' },

  // THORNFIELD — LawFirm (Active)
  { id: 'u-013', firstName: 'Diana',     lastName: 'Yuen',      email: 'diana@thornfieldlaw.com',    role: 'TenantAdmin',      status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000006', tenantCode: 'THORNFIELD',  lastLoginAtUtc: '2025-03-28T08:45:00Z' },
  { id: 'u-014', firstName: 'Marcus',    lastName: 'Thornfield', email: 'm.thornfield@thornfieldlaw.com', role: 'Attorney',    status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000006', tenantCode: 'THORNFIELD',  lastLoginAtUtc: '2025-03-27T09:00:00Z' },
  { id: 'u-015', firstName: 'Stephanie', lastName: 'Kirk',      email: 's.kirk@thornfieldlaw.com',   role: 'CaseManager',      status: 'Invited',  tenantId: '11111111-0000-0000-0000-000000000006', tenantCode: 'THORNFIELD' },

  // NEXUSHEALTH — Provider (Active)
  { id: 'u-016', firstName: 'Carlos',    lastName: 'Reyes',     email: 'c.reyes@nexushealth.net',    role: 'TenantAdmin',      status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000007', tenantCode: 'NEXUSHEALTH', lastLoginAtUtc: '2025-03-29T06:30:00Z' },
  { id: 'u-017', firstName: 'Fatima',    lastName: 'Al-Hassan',  email: 'f.alhassan@nexushealth.net', role: 'CareCoordinator', status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000007', tenantCode: 'NEXUSHEALTH', lastLoginAtUtc: '2025-03-28T12:00:00Z' },
  { id: 'u-018', firstName: 'Trevor',    lastName: 'Owens',     email: 't.owens@nexushealth.net',    role: 'BillingManager',   status: 'Active',   tenantId: '11111111-0000-0000-0000-000000000007', tenantCode: 'NEXUSHEALTH', lastLoginAtUtc: '2025-03-27T15:00:00Z' },
  { id: 'u-019', firstName: 'Yuki',      lastName: 'Tanaka',    email: 'y.tanaka@nexushealth.net',   role: 'ReadOnly',         status: 'Invited',  tenantId: '11111111-0000-0000-0000-000000000007', tenantCode: 'NEXUSHEALTH' },

  // GRAYSTONE — Government (Suspended)
  { id: 'u-020', firstName: 'Patricia',  lastName: 'Langford',  email: 'p.langford@graystonegov.org',role: 'TenantAdmin',      status: 'Inactive', tenantId: '11111111-0000-0000-0000-000000000008', tenantCode: 'GRAYSTONE',   lastLoginAtUtc: '2024-09-20T08:00:00Z' },
  { id: 'u-021', firstName: 'Howard',    lastName: 'Bates',     email: 'h.bates@graystonegov.org',   role: 'ReadOnly',         status: 'Inactive', tenantId: '11111111-0000-0000-0000-000000000008', tenantCode: 'GRAYSTONE',   lastLoginAtUtc: '2024-09-15T10:00:00Z' },
];

// ── Mock tenant detail builder ────────────────────────────────────────────────

const ALL_PRODUCTS = [
  { productCode: 'SYNQFUND',    productName: 'SynqFund'    },
  { productCode: 'SYNQLIEN',    productName: 'SynqLien'    },
  { productCode: 'SYNQBILL',    productName: 'SynqBill'    },
  { productCode: 'SYNQRX',      productName: 'SynqRx'      },
  { productCode: 'SYNQPAYOUT',  productName: 'SynqPayout'  },
  { productCode: 'CARECONNECT', productName: 'CareConnect' },
];

const ENABLED_BY_TYPE: Record<TenantType, string[]> = {
  LawFirm:    ['SYNQFUND', 'SYNQLIEN', 'CARECONNECT'],
  Provider:   ['CARECONNECT', 'SYNQRX'],
  Corporate:  ['SYNQFUND', 'SYNQBILL', 'SYNQPAYOUT', 'SYNQRX', 'SYNQLIEN', 'CARECONNECT'],
  Government: ['CARECONNECT', 'SYNQBILL'],
  Other:      [],
};

const DETAIL_EXTRAS: Record<string, { email: string; updatedAtUtc: string; activeUserCount: number }> = {
  HARTWELL:    { email: 'admin@hartwell.law',           updatedAtUtc: '2024-11-20T09:00:00Z', activeUserCount: 12 },
  MERIDIAN:    { email: 'ops@meridiancare.com',         updatedAtUtc: '2025-01-05T14:30:00Z', activeUserCount: 28 },
  PINNACLE:    { email: 'contact@pinnaclelegal.com',    updatedAtUtc: '2024-12-01T10:00:00Z', activeUserCount: 7  },
  BLUEHAVEN:   { email: 'admin@bluehavenrecovery.org',  updatedAtUtc: '2024-09-15T08:00:00Z', activeUserCount: 1  },
  LEGALSYNQ:   { email: 'admin@legalsynq.com',          updatedAtUtc: '2025-03-01T00:00:00Z', activeUserCount: 3  },
  THORNFIELD:  { email: 'diana@thornfieldlaw.com',      updatedAtUtc: '2025-02-10T11:00:00Z', activeUserCount: 19 },
  NEXUSHEALTH: { email: 'operations@nexushealth.net',   updatedAtUtc: '2025-02-28T16:00:00Z', activeUserCount: 50 },
  GRAYSTONE:   { email: 'it@graystonegov.org',          updatedAtUtc: '2024-10-01T12:00:00Z', activeUserCount: 0  },
};

function buildProductEntitlements(type: TenantType, code: string): ProductEntitlementSummary[] {
  const enabledCodes = code === 'LEGALSYNQ'
    ? ALL_PRODUCTS.map(p => p.productCode)
    : ENABLED_BY_TYPE[type] ?? [];

  return ALL_PRODUCTS.map(p => {
    const enabled = enabledCodes.includes(p.productCode);
    const status: EntitlementStatus = enabled ? 'Enabled' : 'Disabled';
    return {
      productCode:  p.productCode,
      productName:  p.productName,
      enabled,
      status,
      enabledAtUtc: enabled ? '2024-01-15T00:00:00Z' : undefined,
    };
  });
}

function buildTenantDetail(summary: TenantSummary): TenantDetail {
  const extras = DETAIL_EXTRAS[summary.code] ?? {
    email:           undefined,
    updatedAtUtc:    summary.createdAtUtc,
    activeUserCount: Math.max(0, summary.userCount - 1),
  };
  return {
    ...summary,
    email:               extras.email,
    updatedAtUtc:        extras.updatedAtUtc,
    activeUserCount:     extras.activeUserCount,
    linkedOrgCount:      summary.orgCount,
    productEntitlements: buildProductEntitlements(summary.type, summary.code),
  };
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions only.

export const controlCenterServerApi = {

  tenants: {
    // TODO: replace with GET /identity/api/admin/tenants
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

    // TODO: replace with GET /identity/api/admin/tenants/{id}
    getById: (id: string): Promise<TenantDetail | null> => {
      const summary = MOCK_TENANTS.find(t => t.id === id);
      if (!summary) return Promise.resolve(null);
      return Promise.resolve(buildTenantDetail(summary));
    },
  },

  users: {
    // TODO: replace with GET /identity/api/admin/users
    list: (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      tenantId?: string;
    } = {}): Promise<PagedResponse<UserSummary>> => {
      const page     = params.page     ?? 1;
      const pageSize = params.pageSize ?? 20;
      const search   = (params.search ?? '').toLowerCase();

      let filtered = params.tenantId
        ? MOCK_USERS.filter(u => u.tenantId === params.tenantId)
        : MOCK_USERS;

      if (search) {
        filtered = filtered.filter(
          u =>
            u.firstName.toLowerCase().includes(search) ||
            u.lastName.toLowerCase().includes(search) ||
            u.email.toLowerCase().includes(search) ||
            u.role.toLowerCase().includes(search),
        );
      }

      const start = (page - 1) * pageSize;
      const items = filtered.slice(start, start + pageSize);

      return Promise.resolve({ items, totalCount: filtered.length, page, pageSize });
    },
  },
};

// Suppress unused import — serverApi is kept for future live-endpoint wiring.
void (serverApi as unknown);
void (toQs as unknown);
