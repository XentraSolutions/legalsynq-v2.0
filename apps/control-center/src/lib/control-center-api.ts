/**
 * control-center-api.ts — Control Center server-side API client.
 *
 * All methods call the real backend via the API gateway (apiFetch).
 * This module is server-only: Server Components, Server Actions, and
 * Route Handlers. Never import into Client Components.
 *
 * Identity admin endpoints:  /identity/api/admin/...
 * Platform monitoring:       /platform/monitoring/...
 *
 * Error handling:
 *   - HTTP 401 is handled by apiFetch (redirects to /login)
 *   - HTTP 403/404/5xx throw ApiError — callers should catch and
 *     display fetchError banners (already in place on all pages)
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add API caching layer (Next.js fetch cache tags)
 */

import { apiClient } from '@/lib/api-client';
import type {
  TenantSummary,
  TenantDetail,
  UserSummary,
  UserDetail,
  RoleSummary,
  RoleDetail,
  ProductEntitlementSummary,
  ProductCode,
  AuditLogEntry,
  PlatformSetting,
  MonitoringSummary,
  SupportCase,
  SupportCaseDetail,
  SupportCaseStatus,
  SupportNote,
  PagedResponse,
} from '@/types/control-center';

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Build a URL query string from a params object, omitting any undefined /
 * null / empty-string values.
 */
function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions only.

export const controlCenterServerApi = {

  // ── Tenants ──────────────────────────────────────────────────────────────

  tenants: {
    /**
     * GET /identity/api/admin/tenants
     *
     * Returns a paged list of tenants, optionally filtered by search text
     * and/or scoped to a single tenant (tenantId param).
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      tenantId?: string;
    } = {}): Promise<PagedResponse<TenantSummary>> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
        search:   params.search,
        tenantId: params.tenantId,
      });
      return apiClient.get<PagedResponse<TenantSummary>>(
        `/identity/api/admin/tenants${qs}`,
      );
    },

    /**
     * GET /identity/api/admin/tenants/{id}
     *
     * Returns full TenantDetail including product entitlements, or null if
     * the tenant does not exist (ApiError 404 is caught and mapped to null).
     */
    getById: async (id: string): Promise<TenantDetail | null> => {
      try {
        return await apiClient.get<TenantDetail>(
          `/identity/api/admin/tenants/${encodeURIComponent(id)}`,
        );
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /identity/api/admin/tenants/{id}/entitlements/{productCode}
     *
     * Enables or disables a product entitlement for a tenant.
     *
     * TODO: integrate with Identity service entitlement endpoint
     */
    updateEntitlement: (
      tenantId:    string,
      productCode: ProductCode,
      enabled:     boolean,
    ): Promise<ProductEntitlementSummary> =>
      apiClient.post<ProductEntitlementSummary>(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/entitlements/${encodeURIComponent(productCode)}`,
        { enabled },
      ),
  },

  // ── Users ─────────────────────────────────────────────────────────────────

  users: {
    /**
     * GET /identity/api/admin/users
     *
     * Returns a paged list of tenant users. Optionally scoped to a single
     * tenant via tenantId query param.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      tenantId?: string;
    } = {}): Promise<PagedResponse<UserSummary>> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
        search:   params.search,
        tenantId: params.tenantId,
      });
      return apiClient.get<PagedResponse<UserSummary>>(
        `/identity/api/admin/users${qs}`,
      );
    },

    /**
     * GET /identity/api/admin/users/{id}
     *
     * Returns full UserDetail, or null if not found.
     */
    getById: async (id: string): Promise<UserDetail | null> => {
      try {
        return await apiClient.get<UserDetail>(
          `/identity/api/admin/users/${encodeURIComponent(id)}`,
        );
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Roles ─────────────────────────────────────────────────────────────────

  roles: {
    /**
     * GET /identity/api/admin/roles
     *
     * Returns the full list of platform roles with permission keys.
     */
    list: (): Promise<RoleSummary[]> =>
      apiClient.get<RoleSummary[]>('/identity/api/admin/roles'),

    /**
     * GET /identity/api/admin/roles/{id}
     *
     * Returns full RoleDetail including resolved permissions, or null if not found.
     */
    getById: async (id: string): Promise<RoleDetail | null> => {
      try {
        return await apiClient.get<RoleDetail>(
          `/identity/api/admin/roles/${encodeURIComponent(id)}`,
        );
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Audit Logs ────────────────────────────────────────────────────────────

  audit: {
    /**
     * GET /identity/api/admin/audit
     *
     * Returns a paged, filtered list of audit log entries.
     * Accepts tenantId to scope results to a single tenant's events.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: (params: {
      page?:       number;
      pageSize?:   number;
      search?:     string;
      entityType?: string;
      actor?:      string;
      tenantId?:   string;
    } = {}): Promise<{ items: AuditLogEntry[]; totalCount: number }> => {
      const qs = toQs({
        page:       params.page     ?? 1,
        pageSize:   params.pageSize ?? 15,
        search:     params.search,
        entityType: params.entityType,
        actor:      params.actor,
        tenantId:   params.tenantId,
      });
      return apiClient.get<{ items: AuditLogEntry[]; totalCount: number }>(
        `/identity/api/admin/audit${qs}`,
      );
    },
  },

  // ── Platform Settings ─────────────────────────────────────────────────────

  settings: {
    /**
     * GET /identity/api/admin/settings
     *
     * Returns all platform configuration settings.
     *
     * TODO: integrate with Identity service settings endpoint
     */
    list: (): Promise<PlatformSetting[]> =>
      apiClient.get<PlatformSetting[]>('/identity/api/admin/settings'),

    /**
     * PATCH /identity/api/admin/settings/{key}
     *
     * Updates a single setting value by key.
     * Returns the updated PlatformSetting.
     *
     * TODO: integrate with Identity service settings endpoint
     */
    update: (key: string, value: string | number | boolean): Promise<PlatformSetting> =>
      apiClient.patch<PlatformSetting>(
        `/identity/api/admin/settings/${encodeURIComponent(key)}`,
        { value },
      ),
  },

  // ── Monitoring ────────────────────────────────────────────────────────────

  monitoring: {
    /**
     * GET /platform/monitoring/summary
     *
     * Returns system health summary, integration statuses, and active alerts.
     *
     * TODO: integrate with Platform monitoring endpoint
     */
    getSummary: (): Promise<MonitoringSummary> =>
      apiClient.get<MonitoringSummary>('/platform/monitoring/summary'),
  },

  // ── Support ───────────────────────────────────────────────────────────────

  support: {
    /**
     * GET /identity/api/admin/support
     *
     * Returns a paged list of support cases, optionally filtered and scoped.
     *
     * TODO: integrate with support case endpoint
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      status?:   string;
      priority?: string;
      tenantId?: string;
    } = {}): Promise<{ items: SupportCase[]; totalCount: number }> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 10,
        search:   params.search,
        status:   params.status,
        priority: params.priority,
        tenantId: params.tenantId,
      });
      return apiClient.get<{ items: SupportCase[]; totalCount: number }>(
        `/identity/api/admin/support${qs}`,
      );
    },

    /**
     * GET /identity/api/admin/support/{id}
     *
     * Returns full SupportCaseDetail including notes, or null if not found.
     *
     * TODO: integrate with support case endpoint
     */
    getById: async (id: string): Promise<SupportCaseDetail | null> => {
      try {
        return await apiClient.get<SupportCaseDetail>(
          `/identity/api/admin/support/${encodeURIComponent(id)}`,
        );
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /identity/api/admin/support
     *
     * Creates a new support case.
     *
     * TODO: integrate with support case endpoint
     */
    create: (data: {
      title:      string;
      tenantId:   string;
      tenantName: string;
      userId?:    string;
      userName?:  string;
      category:   string;
      priority:   SupportCase['priority'];
    }): Promise<SupportCaseDetail> =>
      apiClient.post<SupportCaseDetail>('/identity/api/admin/support', data),

    /**
     * POST /identity/api/admin/support/{caseId}/notes
     *
     * Adds a note to an existing support case.
     *
     * TODO: integrate with support case endpoint
     */
    addNote: (caseId: string, message: string): Promise<SupportNote> =>
      apiClient.post<SupportNote>(
        `/identity/api/admin/support/${encodeURIComponent(caseId)}/notes`,
        { message },
      ),

    /**
     * PATCH /identity/api/admin/support/{caseId}/status
     *
     * Updates the status of a support case.
     *
     * TODO: integrate with support case endpoint
     */
    updateStatus: (caseId: string, status: SupportCaseStatus): Promise<SupportCase> =>
      apiClient.patch<SupportCase>(
        `/identity/api/admin/support/${encodeURIComponent(caseId)}/status`,
        { status },
      ),
  },

};

// ── Internal helpers ──────────────────────────────────────────────────────────

/**
 * isNotFound — returns true if the error is an ApiError with status 404.
 * Used to map 404 responses to null returns on getById methods.
 */
function isNotFound(err: unknown): boolean {
  return (
    typeof err === 'object' &&
    err !== null &&
    'status' in err &&
    (err as { status: number }).status === 404
  );
}
