import { serverApi } from '@/lib/server-api-client';
import type {
  TenantSummary,
  TenantDetail,
  UserSummary,
  UserDetail,
  Permission,
  RoleSummary,
  RoleDetail,
  ProductEntitlementSummary,
  ProductCode,
  EntitlementStatus,
  AuditLogEntry,
  PlatformSetting,
  IntegrationStatus,
  SystemHealthSummary,
  SystemAlert,
  MonitoringSummary,
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

// ── Mock user detail extras ───────────────────────────────────────────────────

/**
 * Per-user detail fields that are not present in the summary.
 * Keyed by user id (u-001 … u-021).
 */
const USER_DETAIL_EXTRAS: Record<string, {
  createdAtUtc:     string;
  updatedAtUtc:     string;
  isLocked?:        boolean;
  inviteSentAtUtc?: string;
}> = {
  'u-001': { createdAtUtc: '2024-02-15T08:30:00Z', updatedAtUtc: '2025-03-01T10:00:00Z' },
  'u-002': { createdAtUtc: '2024-02-20T09:00:00Z', updatedAtUtc: '2025-01-15T14:00:00Z' },
  'u-003': { createdAtUtc: '2025-03-10T11:00:00Z', updatedAtUtc: '2025-03-10T11:00:00Z', inviteSentAtUtc: '2025-03-10T11:05:00Z' },
  'u-004': { createdAtUtc: '2024-03-01T10:00:00Z', updatedAtUtc: '2025-02-20T08:00:00Z' },
  'u-005': { createdAtUtc: '2024-03-15T12:00:00Z', updatedAtUtc: '2025-02-01T09:00:00Z' },
  'u-006': { createdAtUtc: '2024-04-01T08:00:00Z', updatedAtUtc: '2024-12-10T10:00:00Z' },
  'u-007': { createdAtUtc: '2024-04-10T09:00:00Z', updatedAtUtc: '2024-12-10T10:00:00Z', isLocked: true },
  'u-008': { createdAtUtc: '2024-04-10T14:15:00Z', updatedAtUtc: '2025-03-05T08:00:00Z' },
  'u-009': { createdAtUtc: '2024-05-01T09:00:00Z', updatedAtUtc: '2025-01-20T11:00:00Z' },
  'u-010': { createdAtUtc: '2024-05-20T09:00:00Z', updatedAtUtc: '2024-09-15T08:00:00Z', isLocked: true },
  'u-011': { createdAtUtc: '2024-01-01T00:00:00Z', updatedAtUtc: '2025-03-01T00:00:00Z' },
  'u-012': { createdAtUtc: '2024-01-05T08:00:00Z', updatedAtUtc: '2025-02-15T10:00:00Z' },
  'u-013': { createdAtUtc: '2024-06-05T11:30:00Z', updatedAtUtc: '2025-02-10T11:00:00Z' },
  'u-014': { createdAtUtc: '2024-06-10T09:00:00Z', updatedAtUtc: '2025-01-05T09:00:00Z' },
  'u-015': { createdAtUtc: '2025-02-20T14:00:00Z', updatedAtUtc: '2025-02-20T14:00:00Z', inviteSentAtUtc: '2025-02-20T14:05:00Z' },
  'u-016': { createdAtUtc: '2024-06-18T08:45:00Z', updatedAtUtc: '2025-02-28T16:00:00Z' },
  'u-017': { createdAtUtc: '2024-07-01T09:00:00Z', updatedAtUtc: '2025-02-20T12:00:00Z' },
  'u-018': { createdAtUtc: '2024-07-15T10:00:00Z', updatedAtUtc: '2025-01-10T15:00:00Z' },
  'u-019': { createdAtUtc: '2025-03-15T11:00:00Z', updatedAtUtc: '2025-03-15T11:00:00Z', inviteSentAtUtc: '2025-03-15T11:05:00Z' },
  'u-020': { createdAtUtc: '2024-07-02T13:00:00Z', updatedAtUtc: '2024-10-01T12:00:00Z', isLocked: true },
  'u-021': { createdAtUtc: '2024-07-10T09:00:00Z', updatedAtUtc: '2024-09-30T10:00:00Z' },
};

function buildUserDetail(summary: UserSummary): UserDetail {
  const extras = USER_DETAIL_EXTRAS[summary.id] ?? {
    createdAtUtc: '2024-01-01T00:00:00Z',
    updatedAtUtc: '2024-01-01T00:00:00Z',
  };
  const tenant = MOCK_TENANTS.find(t => t.id === summary.tenantId);
  return {
    ...summary,
    tenantDisplayName: tenant?.displayName ?? summary.tenantCode,
    createdAtUtc:      extras.createdAtUtc,
    updatedAtUtc:      extras.updatedAtUtc,
    isLocked:          extras.isLocked ?? false,
    inviteSentAtUtc:   extras.inviteSentAtUtc,
  };
}

// ── Mock tenant detail builder ────────────────────────────────────────────────

const ALL_PRODUCTS: { productCode: ProductCode; productName: string }[] = [
  { productCode: 'SynqFund',    productName: 'SynqFund'    },
  { productCode: 'SynqLien',    productName: 'SynqLien'    },
  { productCode: 'SynqBill',    productName: 'SynqBill'    },
  { productCode: 'SynqRx',      productName: 'SynqRx'      },
  { productCode: 'SynqPayout',  productName: 'SynqPayout'  },
  { productCode: 'CareConnect', productName: 'CareConnect' },
];

const ENABLED_BY_TYPE: Record<TenantType, ProductCode[]> = {
  LawFirm:    ['SynqFund', 'SynqLien', 'CareConnect'],
  Provider:   ['CareConnect', 'SynqRx'],
  Corporate:  ['SynqFund', 'SynqBill', 'SynqPayout', 'SynqRx', 'SynqLien', 'CareConnect'],
  Government: ['CareConnect', 'SynqBill'],
  Other:      [],
};

/**
 * In-memory entitlement overrides.
 * Keyed by tenantId → productCode → enabled.
 * Resets on server restart; replaced by DB calls once backend is live.
 */
const ENTITLEMENT_OVERRIDES = new Map<string, Map<ProductCode, boolean>>();

function getEntitlementOverrides(tenantId: string): Map<ProductCode, boolean> {
  if (!ENTITLEMENT_OVERRIDES.has(tenantId)) {
    ENTITLEMENT_OVERRIDES.set(tenantId, new Map());
  }
  return ENTITLEMENT_OVERRIDES.get(tenantId)!;
}

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

function buildProductEntitlements(
  type:     TenantType,
  code:     string,
  tenantId: string,
): ProductEntitlementSummary[] {
  const defaults: ProductCode[] = code === 'LEGALSYNQ'
    ? ALL_PRODUCTS.map(p => p.productCode)
    : ENABLED_BY_TYPE[type] ?? [];

  const overrides = getEntitlementOverrides(tenantId);

  return ALL_PRODUCTS.map(p => {
    const enabled = overrides.has(p.productCode)
      ? overrides.get(p.productCode)!
      : defaults.includes(p.productCode);
    const status: EntitlementStatus = enabled ? 'Active' : 'Disabled';
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
    productEntitlements: buildProductEntitlements(summary.type, summary.code, summary.id),
  };
}

// ── Mock roles & permissions ──────────────────────────────────────────────────
// TODO: replace with GET /identity/api/admin/roles when backend endpoint is ready.

const MOCK_PERMISSIONS: Permission[] = [
  // Platform
  { id: 'perm-p1', key: 'platform.view',            description: 'View platform dashboard and summary metrics' },
  { id: 'perm-p2', key: 'platform.settings.read',   description: 'View platform configuration settings' },
  { id: 'perm-p3', key: 'platform.settings.write',  description: 'Modify platform configuration settings' },
  // Tenants
  { id: 'perm-t1', key: 'tenants.read',             description: 'View tenant list and tenant detail' },
  { id: 'perm-t2', key: 'tenants.create',           description: 'Create new tenant accounts' },
  { id: 'perm-t3', key: 'tenants.update',           description: 'Edit tenant details and configuration' },
  { id: 'perm-t4', key: 'tenants.activate',         description: 'Activate or deactivate tenant accounts' },
  { id: 'perm-t5', key: 'tenants.suspend',          description: 'Suspend tenant accounts' },
  // Users
  { id: 'perm-u1', key: 'users.read',               description: 'View user list and user detail' },
  { id: 'perm-u2', key: 'users.create',             description: 'Invite new users to tenants' },
  { id: 'perm-u3', key: 'users.update',             description: 'Edit user profile information' },
  { id: 'perm-u4', key: 'users.lock',               description: 'Lock or unlock user accounts' },
  { id: 'perm-u5', key: 'users.reset-password',     description: 'Send password reset emails to users' },
  // Roles
  { id: 'perm-r1', key: 'roles.read',               description: 'View role definitions and permission lists' },
  { id: 'perm-r2', key: 'roles.write',              description: 'Modify role definitions and assignments' },
  // Audit
  { id: 'perm-a1', key: 'audit.read',               description: 'View audit logs and event history' },
  // Monitoring
  { id: 'perm-m1', key: 'monitoring.read',          description: 'View service health and system status' },
  // Support
  { id: 'perm-s1', key: 'support.tools',            description: 'Access support tools and diagnostic utilities' },
];

const PERM_MAP = new Map<string, Permission>(
  MOCK_PERMISSIONS.map(p => [p.key, p]),
);

function resolvePermissions(keys: string[]): Permission[] {
  return keys.flatMap(k => {
    const p = PERM_MAP.get(k);
    return p ? [p] : [];
  });
}

const MOCK_ROLES: RoleSummary[] = [
  {
    id:          'role-super',
    name:        'SuperAdmin',
    description: 'Unrestricted access to all Control Center features and settings. Reserved for platform engineering.',
    userCount:   0,
    permissions: MOCK_PERMISSIONS.map(p => p.key),
  },
  {
    id:          'role-platform',
    name:        'PlatformAdmin',
    description: 'Full operational access: manage tenants, users, entitlements, and view all platform data.',
    userCount:   2,
    permissions: [
      'platform.view', 'platform.settings.read',
      'tenants.read', 'tenants.activate', 'tenants.suspend',
      'users.read', 'users.create', 'users.update', 'users.lock', 'users.reset-password',
      'roles.read',
      'audit.read',
      'monitoring.read',
    ],
  },
  {
    id:          'role-support',
    name:        'SupportAdmin',
    description: 'Read access to tenant and user data with limited remediation actions for support workflows.',
    userCount:   1,
    permissions: [
      'platform.view',
      'tenants.read',
      'users.read', 'users.lock', 'users.reset-password',
      'audit.read',
      'support.tools',
    ],
  },
  {
    id:          'role-ops',
    name:        'OperationsAdmin',
    description: 'Operational access to tenant lifecycle management, monitoring, and audit log review.',
    userCount:   0,
    permissions: [
      'platform.view', 'platform.settings.read',
      'tenants.read', 'tenants.activate', 'tenants.suspend',
      'users.read',
      'monitoring.read',
      'audit.read',
    ],
  },
  {
    id:          'role-readonly',
    name:        'ReadOnly',
    description: 'View-only access to all Control Center sections. Cannot make any changes.',
    userCount:   2,
    permissions: [
      'platform.view',
      'tenants.read',
      'users.read',
      'roles.read',
      'audit.read',
      'monitoring.read',
    ],
  },
];

const ROLE_TIMESTAMPS: Record<string, { createdAtUtc: string; updatedAtUtc: string }> = {
  'role-super':    { createdAtUtc: '2024-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z' },
  'role-platform': { createdAtUtc: '2024-01-01T00:00:00Z', updatedAtUtc: '2025-02-15T10:00:00Z' },
  'role-support':  { createdAtUtc: '2024-01-01T00:00:00Z', updatedAtUtc: '2025-01-20T09:00:00Z' },
  'role-ops':      { createdAtUtc: '2024-01-01T00:00:00Z', updatedAtUtc: '2025-01-20T09:00:00Z' },
  'role-readonly': { createdAtUtc: '2024-01-01T00:00:00Z', updatedAtUtc: '2024-11-01T08:00:00Z' },
};

function buildRoleDetail(summary: RoleSummary): RoleDetail {
  const ts = ROLE_TIMESTAMPS[summary.id] ?? {
    createdAtUtc: '2024-01-01T00:00:00Z',
    updatedAtUtc: '2024-01-01T00:00:00Z',
  };
  return {
    ...summary,
    createdAtUtc:        ts.createdAtUtc,
    updatedAtUtc:        ts.updatedAtUtc,
    resolvedPermissions: resolvePermissions(summary.permissions),
  };
}

// ── Mock audit log data ───────────────────────────────────────────────────────
// TODO: replace with GET /identity/api/admin/audit

const MOCK_AUDIT_LOGS = ([
  // ── User actions ────────────────────────────────────────────────────────────
  {
    id: 'al-001', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'user.invite',            entityType: 'User',        entityId: 'o.chen@hartwell.law',
    metadata: { tenantCode: 'HARTWELL', role: 'CaseManager' },
    createdAtUtc: '2025-03-10T11:05:00Z',
  },
  {
    id: 'al-002', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'user.deactivate',        entityType: 'User',        entityId: 'a.diallo@meridiancare.com',
    metadata: { tenantCode: 'MERIDIAN', reason: 'extended-leave' },
    createdAtUtc: '2024-12-10T10:00:00Z',
  },
  {
    id: 'al-003', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'user.lock',              entityType: 'User',        entityId: 'tanya@bluehavenrecovery.org',
    metadata: { tenantCode: 'BLUEHAVEN', reason: 'policy-violation' },
    createdAtUtc: '2024-09-15T08:30:00Z',
  },
  {
    id: 'al-004', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'user.invite',            entityType: 'User',        entityId: 's.kirk@thornfieldlaw.com',
    metadata: { tenantCode: 'THORNFIELD', role: 'CaseManager' },
    createdAtUtc: '2025-02-20T14:05:00Z',
  },
  {
    id: 'al-005', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'user.lock',              entityType: 'User',        entityId: 'p.langford@graystonegov.org',
    metadata: { tenantCode: 'GRAYSTONE', reason: 'account-suspended' },
    createdAtUtc: '2024-10-01T12:10:00Z',
  },
  {
    id: 'al-006', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'user.invite',            entityType: 'User',        entityId: 'y.tanaka@nexushealth.net',
    metadata: { tenantCode: 'NEXUSHEALTH', role: 'ReadOnly' },
    createdAtUtc: '2025-03-15T11:05:00Z',
  },
  {
    id: 'al-007', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'user.unlock',            entityType: 'User',        entityId: 'j.whitmore@hartwell.law',
    metadata: { tenantCode: 'HARTWELL' },
    createdAtUtc: '2025-01-15T14:05:00Z',
  },
  {
    id: 'al-008', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'user.password_reset',    entityType: 'User',        entityId: 'r.moss@pinnaclelegal.com',
    metadata: { tenantCode: 'PINNACLE', method: 'email-link' },
    createdAtUtc: '2025-03-05T08:15:00Z',
  },

  // ── Tenant updates ──────────────────────────────────────────────────────────
  {
    id: 'al-009', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'tenant.create',          entityType: 'Tenant',      entityId: 'HARTWELL',
    metadata: { tenantType: 'LawFirm' },
    createdAtUtc: '2024-02-15T08:30:00Z',
  },
  {
    id: 'al-010', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'tenant.create',          entityType: 'Tenant',      entityId: 'NEXUSHEALTH',
    metadata: { tenantType: 'Provider' },
    createdAtUtc: '2024-06-18T08:45:00Z',
  },
  {
    id: 'al-011', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'tenant.suspend',         entityType: 'Tenant',      entityId: 'GRAYSTONE',
    metadata: { previousStatus: 'Active', reason: 'non-payment' },
    createdAtUtc: '2024-10-01T12:00:00Z',
  },
  {
    id: 'al-012', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'tenant.deactivate',      entityType: 'Tenant',      entityId: 'BLUEHAVEN',
    metadata: { previousStatus: 'Active' },
    createdAtUtc: '2024-09-01T09:00:00Z',
  },
  {
    id: 'al-013', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'tenant.create',          entityType: 'Tenant',      entityId: 'THORNFIELD',
    metadata: { tenantType: 'LawFirm' },
    createdAtUtc: '2024-06-05T11:30:00Z',
  },
  {
    id: 'al-014', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'tenant.update',          entityType: 'Tenant',      entityId: 'MERIDIAN',
    metadata: { field: 'primaryContactEmail', previous: 'old@meridiancare.com', next: 'ops@meridiancare.com' },
    createdAtUtc: '2025-01-05T14:30:00Z',
  },

  // ── Entitlement changes ─────────────────────────────────────────────────────
  {
    id: 'al-015', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'entitlement.enable',     entityType: 'Entitlement', entityId: 'HARTWELL:SynqFund',
    metadata: { tenantCode: 'HARTWELL', product: 'SynqFund' },
    createdAtUtc: '2024-02-16T09:00:00Z',
  },
  {
    id: 'al-016', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'entitlement.enable',     entityType: 'Entitlement', entityId: 'MERIDIAN:CareConnect',
    metadata: { tenantCode: 'MERIDIAN', product: 'CareConnect' },
    createdAtUtc: '2024-03-02T10:15:00Z',
  },
  {
    id: 'al-017', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'entitlement.disable',    entityType: 'Entitlement', entityId: 'BLUEHAVEN:CareConnect',
    metadata: { tenantCode: 'BLUEHAVEN', product: 'CareConnect', reason: 'subscription-lapsed' },
    createdAtUtc: '2024-09-02T10:00:00Z',
  },
  {
    id: 'al-018', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'entitlement.enable',     entityType: 'Entitlement', entityId: 'THORNFIELD:SynqLien',
    metadata: { tenantCode: 'THORNFIELD', product: 'SynqLien' },
    createdAtUtc: '2024-06-06T08:00:00Z',
  },
  {
    id: 'al-019', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'entitlement.enable',     entityType: 'Entitlement', entityId: 'NEXUSHEALTH:SynqRx',
    metadata: { tenantCode: 'NEXUSHEALTH', product: 'SynqRx' },
    createdAtUtc: '2024-07-01T11:00:00Z',
  },
  {
    id: 'al-020', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'entitlement.disable',    entityType: 'Entitlement', entityId: 'GRAYSTONE:SynqBill',
    metadata: { tenantCode: 'GRAYSTONE', product: 'SynqBill', reason: 'account-suspended' },
    createdAtUtc: '2024-10-02T08:00:00Z',
  },

  // ── Role changes ────────────────────────────────────────────────────────────
  {
    id: 'al-021', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'role.assign',            entityType: 'Role',        entityId: 'PlatformAdmin',
    metadata: { assignedTo: 'n.patel@legalsynq.com' },
    createdAtUtc: '2024-01-05T08:10:00Z',
  },
  {
    id: 'al-022', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'role.assign',            entityType: 'Role',        entityId: 'SupportAdmin',
    metadata: { assignedTo: 'support@legalsynq.com' },
    createdAtUtc: '2024-03-15T10:00:00Z',
  },
  {
    id: 'al-023', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'role.revoke',            entityType: 'Role',        entityId: 'ReadOnly',
    metadata: { revokedFrom: 'temp@legalsynq.com', reason: 'contract-ended' },
    createdAtUtc: '2024-11-30T17:00:00Z',
  },

  // ── System events ────────────────────────────────────────────────────────────
  {
    id: 'al-024', actorName: 'identity-service',       actorType: 'System',
    action: 'system.migration',       entityType: 'System',      entityId: 'identity-db',
    metadata: { migration: '20260328200000_AddMultiOrgProductRoleModel', result: 'applied' },
    createdAtUtc: '2026-03-28T20:00:10Z',
  },
  {
    id: 'al-025', actorName: 'identity-service',       actorType: 'System',
    action: 'system.health_check',    entityType: 'System',      entityId: 'identity-service',
    metadata: { status: 'healthy', durationMs: 12 },
    createdAtUtc: '2025-03-29T06:00:00Z',
  },
  {
    id: 'al-026', actorName: 'identity-service',       actorType: 'System',
    action: 'user.session_expired',   entityType: 'User',        entityId: 'p.langford@graystonegov.org',
    metadata: { tenantCode: 'GRAYSTONE', reason: 'jwt-ttl' },
    createdAtUtc: '2024-09-20T18:00:00Z',
  },
  {
    id: 'al-027', actorName: 'admin@legalsynq.com',    actorType: 'Admin',
    action: 'tenant.activate',        entityType: 'Tenant',      entityId: 'PINNACLE',
    metadata: { previousStatus: 'Inactive' },
    createdAtUtc: '2024-04-10T14:30:00Z',
  },
  {
    id: 'al-028', actorName: 'n.patel@legalsynq.com',  actorType: 'Admin',
    action: 'user.deactivate',        entityType: 'User',        entityId: 'h.bates@graystonegov.org',
    metadata: { tenantCode: 'GRAYSTONE', reason: 'account-suspended' },
    createdAtUtc: '2024-09-30T10:05:00Z',
  },
] as AuditLogEntry[]).sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)); // newest first

// ── Mock monitoring data ───────────────────────────────────────────────────────
// TODO: replace with GET /platform/monitoring/summary

const MOCK_MONITORING_SUMMARY: MonitoringSummary = {
  system: {
    status:           'Healthy',
    lastCheckedAtUtc: '2026-03-29T03:50:00Z',
  },

  integrations: [
    {
      name:             'Identity Service',
      status:           'Healthy',
      latencyMs:        42,
      lastCheckedAtUtc: '2026-03-29T03:50:00Z',
    },
    {
      name:             'Payments Gateway',
      status:           'Degraded',
      latencyMs:        1240,
      lastCheckedAtUtc: '2026-03-29T03:50:00Z',
    },
    {
      name:             'Notifications',
      status:           'Healthy',
      latencyMs:        88,
      lastCheckedAtUtc: '2026-03-29T03:50:00Z',
    },
    {
      name:             'CareConnect API',
      status:           'Healthy',
      latencyMs:        115,
      lastCheckedAtUtc: '2026-03-29T03:50:00Z',
    },
    {
      name:             'Document Storage',
      status:           'Down',
      lastCheckedAtUtc: '2026-03-29T03:48:00Z',
    },
  ],

  alerts: [
    {
      id:           'alert-001',
      message:      'Payments Gateway latency above 1 000 ms threshold — investigating upstream provider.',
      severity:     'Warning',
      createdAtUtc: '2026-03-29T03:45:00Z',
    },
    {
      id:           'alert-002',
      message:      'Document Storage health check failed — service unreachable since 03:48 UTC.',
      severity:     'Critical',
      createdAtUtc: '2026-03-29T03:48:00Z',
    },
    {
      id:           'alert-003',
      message:      'Scheduled maintenance window confirmed for 2026-04-01 02:00–04:00 UTC.',
      severity:     'Info',
      createdAtUtc: '2026-03-28T18:00:00Z',
    },
    {
      id:           'alert-004',
      message:      'Identity Service certificate renewal completed successfully.',
      severity:     'Info',
      createdAtUtc: '2026-03-27T10:00:00Z',
    },
    {
      id:           'alert-005',
      message:      'Unusual login volume detected from GRAYSTONE tenant — security review initiated.',
      severity:     'Warning',
      createdAtUtc: '2026-03-26T22:15:00Z',
    },
  ],
};

// ── Mock platform settings ─────────────────────────────────────────────────────
// TODO: replace with GET/POST /identity/api/admin/settings

const MOCK_SETTINGS_STORE: PlatformSetting[] = [
  {
    key:         'allowTenantSelfSignup',
    label:       'Allow Tenant Self-Signup',
    value:       false,
    type:        'boolean',
    description: 'Permit new tenants to register themselves without a platform-admin invitation.',
    editable:    true,
  },
  {
    key:         'enableCareConnectMap',
    label:       'Enable CareConnect Map',
    value:       true,
    type:        'boolean',
    description: 'Display the provider-network map view inside the CareConnect product.',
    editable:    true,
  },
  {
    key:         'enableSynqPayoutBeta',
    label:       'Enable SynqPayout Beta',
    value:       false,
    type:        'boolean',
    description: 'Activate the SynqPayout disbursement feature for beta-enrolled tenants.',
    editable:    true,
  },
  {
    key:         'supportEmailAddress',
    label:       'Support Email Address',
    value:       'support@legalsynq.com',
    type:        'string',
    description: 'Destination address for all platform-generated support contact requests.',
    editable:    true,
  },
  {
    key:         'defaultSessionTimeoutMinutes',
    label:       'Default Session Timeout (Minutes)',
    value:       60,
    type:        'number',
    description: 'Idle-session duration in minutes before a user is automatically signed out.',
    editable:    true,
  },
];

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

    // TODO: replace with POST /identity/api/admin/tenants/{id}/entitlements
    updateEntitlement: (
      tenantId:    string,
      productCode: ProductCode,
      enabled:     boolean,
    ): Promise<ProductEntitlementSummary> => {
      const summary = MOCK_TENANTS.find(t => t.id === tenantId);
      if (!summary) return Promise.reject(new Error(`Tenant ${tenantId} not found`));

      getEntitlementOverrides(tenantId).set(productCode, enabled);

      const product = ALL_PRODUCTS.find(p => p.productCode === productCode);
      const status: EntitlementStatus = enabled ? 'Active' : 'Disabled';
      const result: ProductEntitlementSummary = {
        productCode,
        productName:  product?.productName ?? productCode,
        enabled,
        status,
        enabledAtUtc: enabled ? new Date().toISOString() : undefined,
      };
      return Promise.resolve(result);
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

    // TODO: replace with GET /identity/api/admin/users/{id}
    getById: (id: string): Promise<UserDetail | null> => {
      const summary = MOCK_USERS.find(u => u.id === id);
      if (!summary) return Promise.resolve(null);
      return Promise.resolve(buildUserDetail(summary));
    },
  },

  roles: {
    // TODO: replace with GET /identity/api/admin/roles
    list: (): Promise<RoleSummary[]> => {
      return Promise.resolve(MOCK_ROLES);
    },

    // TODO: replace with GET /identity/api/admin/roles/{id}
    getById: (id: string): Promise<RoleDetail | null> => {
      const summary = MOCK_ROLES.find(r => r.id === id);
      if (!summary) return Promise.resolve(null);
      return Promise.resolve(buildRoleDetail(summary));
    },
  },

  audit: {
    // TODO: replace with GET /identity/api/admin/audit
    list: (params: {
      page?:       number;
      pageSize?:   number;
      search?:     string;
      entityType?: string;
      actor?:      string;
    } = {}): Promise<{ items: AuditLogEntry[]; totalCount: number }> => {
      const page       = params.page     ?? 1;
      const pageSize   = params.pageSize ?? 15;
      const search     = (params.search     ?? '').toLowerCase().trim();
      const entityType = (params.entityType ?? '').toLowerCase().trim();
      const actor      = (params.actor      ?? '').toLowerCase().trim();

      let filtered = MOCK_AUDIT_LOGS;

      if (search) {
        filtered = filtered.filter(e =>
          e.action.toLowerCase().includes(search)     ||
          e.entityId.toLowerCase().includes(search)   ||
          e.actorName.toLowerCase().includes(search)  ||
          e.entityType.toLowerCase().includes(search),
        );
      }

      if (entityType) {
        filtered = filtered.filter(e =>
          e.entityType.toLowerCase() === entityType,
        );
      }

      if (actor) {
        filtered = filtered.filter(e =>
          e.actorName.toLowerCase().includes(actor),
        );
      }

      const totalCount = filtered.length;
      const start      = (page - 1) * pageSize;
      const items      = filtered.slice(start, start + pageSize);

      return Promise.resolve({ items, totalCount });
    },
  },

  settings: {
    // TODO: replace with GET/POST /identity/api/admin/settings
    list: (): Promise<PlatformSetting[]> =>
      Promise.resolve(MOCK_SETTINGS_STORE.map(s => ({ ...s }))),

    update: (key: string, value: string | number | boolean): Promise<PlatformSetting> => {
      const idx = MOCK_SETTINGS_STORE.findIndex(s => s.key === key);
      if (idx === -1) return Promise.reject(new Error(`Unknown setting key: ${key}`));
      const setting = MOCK_SETTINGS_STORE[idx];
      if (!setting.editable) return Promise.reject(new Error(`Setting '${key}' is read-only.`));
      MOCK_SETTINGS_STORE[idx] = { ...setting, value };
      return Promise.resolve({ ...MOCK_SETTINGS_STORE[idx] });
    },
  },

  monitoring: {
    // TODO: replace with GET /platform/monitoring/summary
    getSummary: (): Promise<MonitoringSummary> =>
      Promise.resolve({
        system:       { ...MOCK_MONITORING_SUMMARY.system },
        integrations: MOCK_MONITORING_SUMMARY.integrations.map(i => ({ ...i })),
        alerts:       MOCK_MONITORING_SUMMARY.alerts.map(a => ({ ...a })),
      }),
  },
};

// Suppress unused import — serverApi is kept for future live-endpoint wiring.
void (serverApi as unknown);
void (toQs as unknown);
