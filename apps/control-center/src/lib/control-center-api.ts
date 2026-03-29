/**
 * control-center-api.ts — Control Center server-side API client.
 *
 * All methods call the real backend via the API gateway (apiFetch).
 * Every response is normalised through api-mappers.ts before being
 * returned, so the UI always receives strict-typed frontend shapes
 * regardless of whether the backend uses camelCase or snake_case.
 *
 * Server-only: Server Components, Server Actions, Route Handlers.
 * Never import into Client Components.
 *
 * Identity admin endpoints:  /identity/api/admin/...
 * Platform monitoring:       /platform/monitoring/...
 *
 * Error handling:
 *   - HTTP 401 is handled by apiFetch (redirects to /login)
 *   - HTTP 403/404/5xx throw ApiError — callers catch and display
 *     fetchError banners (already wired on all pages)
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add API caching layer (Next.js fetch cache tags)
 */

import { apiClient }                   from '@/lib/api-client';
import {
  mapTenantSummary,
  mapTenantDetail,
  mapEntitlementResponse,
  mapUserSummary,
  mapUserDetail,
  mapRoleSummary,
  mapRoleDetail,
  mapAuditLog,
  mapSetting,
  mapMonitoring,
  mapSupportCase,
  mapSupportCaseDetail,
  mapSupportNote,
  mapPagedResponse,
}                                       from '@/lib/api-mappers';
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
}                                       from '@/types/control-center';

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
     * Response is normalised via mapTenantSummary + mapPagedResponse.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: async (params: {
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
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/tenants${qs}`,
      );
      return mapPagedResponse(raw, mapTenantSummary);
    },

    /**
     * GET /identity/api/admin/tenants/{id}
     *
     * Returns full TenantDetail including product entitlements, or null if
     * the tenant does not exist. Response is normalised via mapTenantDetail.
     */
    getById: async (id: string): Promise<TenantDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/tenants/${encodeURIComponent(id)}`,
        );
        return mapTenantDetail(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /identity/api/admin/tenants/{id}/entitlements/{productCode}
     *
     * Enables or disables a product entitlement for a tenant.
     * Response is normalised via mapEntitlementResponse.
     *
     * TODO: integrate with Identity service entitlement endpoint
     */
    updateEntitlement: async (
      tenantId:    string,
      productCode: ProductCode,
      enabled:     boolean,
    ): Promise<ProductEntitlementSummary> => {
      const raw = await apiClient.post<unknown>(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/entitlements/${encodeURIComponent(productCode)}`,
        { enabled },
      );
      return mapEntitlementResponse(raw);
    },
  },

  // ── Users ─────────────────────────────────────────────────────────────────

  users: {
    /**
     * GET /identity/api/admin/users
     *
     * Returns a paged list of tenant users. Optionally scoped to a single
     * tenant via tenantId query param.
     * Response is normalised via mapUserSummary + mapPagedResponse.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: async (params: {
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
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users${qs}`,
      );
      return mapPagedResponse(raw, mapUserSummary);
    },

    /**
     * GET /identity/api/admin/users/{id}
     *
     * Returns full UserDetail, or null if not found.
     * Response is normalised via mapUserDetail.
     */
    getById: async (id: string): Promise<UserDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/users/${encodeURIComponent(id)}`,
        );
        return mapUserDetail(raw);
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
     * Returns the full list of platform roles.
     * Response is normalised via mapRoleSummary.
     */
    list: async (): Promise<RoleSummary[]> => {
      const raw = await apiClient.get<unknown>('/identity/api/admin/roles');
      if (Array.isArray(raw)) return raw.map(mapRoleSummary);
      // Backend may wrap in a paged envelope
      const paged = mapPagedResponse(raw, mapRoleSummary);
      return paged.items;
    },

    /**
     * GET /identity/api/admin/roles/{id}
     *
     * Returns full RoleDetail including resolved permissions, or null if not found.
     * Response is normalised via mapRoleDetail.
     */
    getById: async (id: string): Promise<RoleDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/roles/${encodeURIComponent(id)}`,
        );
        return mapRoleDetail(raw);
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
     * Response is normalised via mapAuditLog + mapPagedResponse.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: async (params: {
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
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/audit${qs}`,
      );
      const paged = mapPagedResponse(raw, mapAuditLog);
      return { items: paged.items, totalCount: paged.totalCount };
    },
  },

  // ── Platform Settings ─────────────────────────────────────────────────────

  settings: {
    /**
     * GET /identity/api/admin/settings
     *
     * Returns all platform configuration settings.
     * Response is normalised via mapSetting.
     *
     * TODO: integrate with Identity service settings endpoint
     */
    list: async (): Promise<PlatformSetting[]> => {
      const raw = await apiClient.get<unknown>('/identity/api/admin/settings');
      if (Array.isArray(raw)) return raw.map(mapSetting);
      const paged = mapPagedResponse(raw, mapSetting);
      return paged.items;
    },

    /**
     * PATCH /identity/api/admin/settings/{key}
     *
     * Updates a single setting value by key.
     * Response is normalised via mapSetting.
     *
     * TODO: integrate with Identity service settings endpoint
     */
    update: async (key: string, value: string | number | boolean): Promise<PlatformSetting> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/settings/${encodeURIComponent(key)}`,
        { value },
      );
      return mapSetting(raw);
    },
  },

  // ── Monitoring ────────────────────────────────────────────────────────────

  monitoring: {
    /**
     * GET /platform/monitoring/summary
     *
     * Returns system health summary, integration statuses, and active alerts.
     * Response is normalised via mapMonitoring.
     *
     * TODO: integrate with Platform monitoring endpoint
     */
    getSummary: async (): Promise<MonitoringSummary> => {
      const raw = await apiClient.get<unknown>('/platform/monitoring/summary');
      return mapMonitoring(raw);
    },
  },

  // ── Support ───────────────────────────────────────────────────────────────

  support: {
    /**
     * GET /identity/api/admin/support
     *
     * Returns a paged list of support cases, optionally filtered and scoped.
     * Response is normalised via mapSupportCase + mapPagedResponse.
     *
     * TODO: integrate with support case endpoint
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     */
    list: async (params: {
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
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/support${qs}`,
      );
      const paged = mapPagedResponse(raw, mapSupportCase);
      return { items: paged.items, totalCount: paged.totalCount };
    },

    /**
     * GET /identity/api/admin/support/{id}
     *
     * Returns full SupportCaseDetail including notes, or null if not found.
     * Response is normalised via mapSupportCaseDetail.
     *
     * TODO: integrate with support case endpoint
     */
    getById: async (id: string): Promise<SupportCaseDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/support/${encodeURIComponent(id)}`,
        );
        return mapSupportCaseDetail(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /identity/api/admin/support
     *
     * Creates a new support case.
     * Response is normalised via mapSupportCaseDetail.
     *
     * TODO: integrate with support case endpoint
     */
    create: async (data: {
      title:      string;
      tenantId:   string;
      tenantName: string;
      userId?:    string;
      userName?:  string;
      category:   string;
      priority:   SupportCase['priority'];
    }): Promise<SupportCaseDetail> => {
      const raw = await apiClient.post<unknown>('/identity/api/admin/support', data);
      return mapSupportCaseDetail(raw);
    },

    /**
     * POST /identity/api/admin/support/{caseId}/notes
     *
     * Adds a note to an existing support case.
     * Response is normalised via mapSupportNote.
     *
     * TODO: integrate with support case endpoint
     */
    addNote: async (caseId: string, message: string): Promise<SupportNote> => {
      const raw = await apiClient.post<unknown>(
        `/identity/api/admin/support/${encodeURIComponent(caseId)}/notes`,
        { message },
      );
      return mapSupportNote(raw);
    },

    /**
     * PATCH /identity/api/admin/support/{caseId}/status
     *
     * Updates the status of a support case.
     * Response is normalised via mapSupportCase.
     *
     * TODO: integrate with support case endpoint
     */
    updateStatus: async (caseId: string, status: SupportCaseStatus): Promise<SupportCase> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/support/${encodeURIComponent(caseId)}/status`,
        { status },
      );
      return mapSupportCase(raw);
    },
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
