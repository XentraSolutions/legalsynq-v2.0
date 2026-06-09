import { serverApi } from '@/lib/server-api-client';
import type {
  TenantSummary,
  TenantDetail,
  TenantUserSummary,
  RoleSummary,
  ProductEntitlementSummary,
  ProductCatalogItem,
  AuditLogEntry,
  SystemHealthSummary,
  PagedResponse,
} from '@/types/control-center';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

function normalizeTenantType(raw: string): TenantSummary['type'] {
  switch (raw) {
    case 'LAW_FIRM':   return 'LawFirm';
    case 'PROVIDER':   return 'Provider';
    case 'CORPORATE':
    case 'INTERNAL':   return 'Corporate';
    case 'GOVERNMENT': return 'Government';
    default:           return 'Other';
  }
}

function normalizeTenantStatus(raw: string, isActive: boolean): TenantSummary['status'] {
  if (raw === 'Suspended') return 'Suspended';
  return isActive ? 'Active' : 'Inactive';
}

export const controlCenterServerApi = {
  tenants: {
    list: async (params: { page?: number; pageSize?: number; search?: string } = {}) => {
      const result = await serverApi.get<PagedResponse<{
        id: string;
        code: string;
        displayName: string;
        type: string;
        status: string;
        primaryContactName: string;
        isActive: boolean;
        userCount: number;
        orgCount: number;
        createdAtUtc: string;
      }>>(`/identity/api/admin/tenants${toQs(params as Record<string, unknown>)}`);

      return {
        ...result,
        items: result.items.map((tenant) => ({
          ...tenant,
          type: normalizeTenantType(tenant.type),
          status: normalizeTenantStatus(tenant.status, tenant.isActive),
        })),
      };
    },

    getById: async (id: string) => {
      const tenant = await serverApi.get<{
        id: string;
        code: string;
        displayName: string;
        type: string;
        status: string;
        primaryContactName: string;
        email?: string;
        isActive: boolean;
        userCount: number;
        activeUserCount?: number;
        orgCount: number;
        linkedOrgCount?: number;
        createdAtUtc: string;
        updatedAtUtc: string;
        sessionTimeoutMinutes: number;
        logoDocumentId?: string;
        logoWhiteDocumentId?: string;
        productEntitlements: TenantDetail['productEntitlements'];
      }>(`/identity/api/admin/tenants/${id}`);

      return {
        ...tenant,
        type: normalizeTenantType(tenant.type),
        status: normalizeTenantStatus(tenant.status, tenant.isActive),
      } satisfies TenantDetail;
    },
  },

  users: {
    list: (params: { tenantId?: string; page?: number; pageSize?: number; search?: string } = {}) =>
      serverApi.get<PagedResponse<TenantUserSummary>>(
        `/identity/api/admin/users${toQs(params as Record<string, unknown>)}`,
      ),
  },

  roles: {
    list: () =>
      serverApi.get<RoleSummary[]>('/identity/api/admin/roles'),
  },

  products: {
    listCatalog: () =>
      serverApi.get<ProductCatalogItem[]>('/identity/api/admin/products'),

    listEntitlements: (params: { tenantId?: string } = {}) =>
      serverApi.get<ProductEntitlementSummary[]>(
        `/identity/api/admin/product-entitlements${toQs(params as Record<string, unknown>)}`,
      ),
  },

  auditLogs: {
    list: (params: {
      tenantId?: string;
      actorId?:  string;
      action?:   string;
      from?:     string;
      to?:       string;
      page?:     number;
      pageSize?: number;
    } = {}) =>
      serverApi.get<PagedResponse<AuditLogEntry>>(
        `/identity/api/admin/audit-logs${toQs(params as Record<string, unknown>)}`,
      ),
  },

  monitoring: {
    health: () =>
      Promise.all([
        fetchServiceHealth('identity',    '/identity/health'),
        fetchServiceHealth('fund',        '/fund/health'),
        fetchServiceHealth('careconnect', '/careconnect/health'),
        fetchServiceHealth('gateway',     '/health'),
      ]),
  },
};

async function fetchServiceHealth(
  serviceName: string,
  path: string,
): Promise<SystemHealthSummary> {
  try {
    const data = await serverApi.get<{ status: string; version?: string; environment?: string }>(path);
    return {
      serviceName,
      status:       data.status === 'ok' ? 'ok' : 'degraded',
      version:      data.version,
      environment:  data.environment,
      checkedAtUtc: new Date().toISOString(),
    };
  } catch {
    return {
      serviceName,
      status:       'down',
      checkedAtUtc: new Date().toISOString(),
    };
  }
}
