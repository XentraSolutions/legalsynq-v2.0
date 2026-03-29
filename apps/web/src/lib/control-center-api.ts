import { serverApi } from '@/lib/server-api-client';
import { apiClient } from '@/lib/api-client';
import type {
  TenantSummary,
  TenantDetail,
  TenantUserSummary,
  RoleSummary,
  ProductEntitlementSummary,
  AuditLogEntry,
  SystemHealthSummary,
  PagedResponse,
} from '@/types/control-center';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions.
// Reads the platform_session cookie and calls the gateway directly (no extra hop).
// DO NOT import this in Client Components.

export const controlCenterServerApi = {

  tenants: {
    // TODO: confirm endpoint — GET /identity/api/admin/tenants not yet verified
    list: (params: { page?: number; pageSize?: number; search?: string } = {}) =>
      serverApi.get<PagedResponse<TenantSummary>>(
        `/identity/api/admin/tenants${toQs(params as Record<string, unknown>)}`,
      ),

    // TODO: confirm endpoint — GET /identity/api/admin/tenants/:id not yet verified
    getById: (id: string) =>
      serverApi.get<TenantDetail>(`/identity/api/admin/tenants/${id}`),
  },

  users: {
    // TODO: confirm endpoint — GET /identity/api/admin/users not yet verified
    list: (params: { tenantId?: string; page?: number; pageSize?: number; search?: string } = {}) =>
      serverApi.get<PagedResponse<TenantUserSummary>>(
        `/identity/api/admin/users${toQs(params as Record<string, unknown>)}`,
      ),
  },

  roles: {
    // TODO: confirm endpoint — GET /identity/api/admin/roles not yet verified
    list: () =>
      serverApi.get<RoleSummary[]>('/identity/api/admin/roles'),
  },

  products: {
    // TODO: confirm endpoint — GET /identity/api/admin/product-entitlements not yet verified
    listEntitlements: (params: { tenantId?: string } = {}) =>
      serverApi.get<ProductEntitlementSummary[]>(
        `/identity/api/admin/product-entitlements${toQs(params as Record<string, unknown>)}`,
      ),
  },

  auditLogs: {
    // TODO: no audit backend exists yet — endpoint is a forward-looking stub
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
    // Each service exposes GET /health and GET /info — these are gateway-proxied
    health: () =>
      Promise.all([
        fetchServiceHealth('identity',    '/identity/health'),
        fetchServiceHealth('fund',        '/fund/health'),
        fetchServiceHealth('careconnect', '/careconnect/health'),
        fetchServiceHealth('gateway',     '/health'),
      ]),
  },
};

// ── Client-side API ───────────────────────────────────────────────────────────
// Use in Client Components (forms, interactive UI).
// Calls /api/identity/* which routes through the BFF proxy → gateway → identity:5001.

export const controlCenterApi = {

  tenants: {
    // TODO: confirm endpoint — POST /identity/api/admin/tenants/:id/activate not yet verified
    activate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/tenants/${id}/activate`, {}),

    // TODO: confirm endpoint
    deactivate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/tenants/${id}/deactivate`, {}),
  },

  users: {
    // TODO: confirm endpoint — POST /identity/api/users (existing, but admin context unverified)
    create: (body: {
      tenantId:  string;
      email:     string;
      password:  string;
      firstName: string;
      lastName:  string;
      roleIds?:  string[];
    }) => apiClient.post<TenantUserSummary>('/identity/api/users', body),

    // TODO: confirm endpoint
    deactivate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/users/${id}/deactivate`, {}),
  },

  products: {
    // TODO: confirm endpoint
    enableForTenant: (tenantId: string, productId: string) =>
      apiClient.post<void>(`/identity/api/admin/product-entitlements`, { tenantId, productId }),

    // TODO: confirm endpoint
    disableForTenant: (tenantId: string, productId: string) =>
      apiClient.delete<void>(
        `/identity/api/admin/product-entitlements/${tenantId}/${productId}`,
      ),
  },
};

// ── Internal helpers ──────────────────────────────────────────────────────────

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
