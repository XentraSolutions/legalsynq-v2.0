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
 * ── Cache strategy summary ───────────────────────────────────────────────────
 *
 *   Endpoint               Tag                  TTL    Rationale
 *   ─────────────────────  ───────────────────  ─────  ─────────────────────────────────
 *   tenants.list           cc:tenants           60 s   Tenant roster changes rarely
 *   tenants.getById        cc:tenants           60 s   Same lifecycle as list
 *   users.list             cc:users             30 s   User state changes more often
 *   users.getById          cc:users             30 s   Same lifecycle as list
 *   roles.list             cc:roles             300 s  Roles are near-static
 *   roles.getById          cc:roles             300 s  Same lifecycle as list
 *   audit.list             cc:audit             10 s   Near-real-time log view
 *   settings.list          cc:settings          300 s  Settings rarely change
 *   monitoring.getSummary  cc:monitoring        5 s    Live health feed
 *   support.list           cc:support           10 s   Case status changes frequently
 *   support.getById        cc:support           10 s   Same lifecycle as list
 *
 * ── Revalidation after mutations ─────────────────────────────────────────────
 *
 *   Mutation                          Invalidates
 *   ────────────────────────────────  ─────────────────
 *   tenants.updateEntitlement         cc:tenants
 *   settings.update                   cc:settings
 *   support.create                    cc:support
 *   support.addNote                   cc:support
 *   support.updateStatus              cc:support
 *
 * Error handling:
 *   - HTTP 401 is handled by apiFetch (redirects to /login)
 *   - HTTP 403/404/5xx throw ApiError — callers catch and display
 *     fetchError banners (already wired on all pages)
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */

import { revalidateTag }               from 'next/cache';
import { apiClient, CACHE_TAGS }       from '@/lib/api-client';
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
     * Cache: 60 s  Tag: cc:tenants
     *   Tenant roster changes rarely; 60 s balances freshness vs load.
     *   On-demand invalidated by tenants.updateEntitlement mutation.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
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
        60,
        [CACHE_TAGS.tenants],
      );
      return mapPagedResponse(raw, mapTenantSummary);
    },

    /**
     * GET /identity/api/admin/tenants/{id}
     *
     * Returns full TenantDetail including product entitlements, or null if
     * the tenant does not exist. Response is normalised via mapTenantDetail.
     *
     * Cache: 60 s  Tag: cc:tenants
     *   Same cache lifecycle as tenants.list.
     *   On-demand invalidated by tenants.updateEntitlement mutation.
     */
    getById: async (id: string): Promise<TenantDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/tenants/${encodeURIComponent(id)}`,
          60,
          [CACHE_TAGS.tenants],
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
     * Revalidates: cc:tenants — so the next tenants.list / getById call
     * bypasses the cache and fetches fresh data.
     *
     * TODO: integrate with Identity service entitlement endpoint
     * TODO: add Redis or edge caching
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
      const result = mapEntitlementResponse(raw);
      // Purge tenant cache so entitlement state is immediately fresh
      revalidateTag(CACHE_TAGS.tenants);
      return result;
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
     * Cache: 30 s  Tag: cc:users
     *   User records change more often than tenant records (invites,
     *   status updates). 30 s keeps the UI reasonably live.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
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
        30,
        [CACHE_TAGS.users],
      );
      return mapPagedResponse(raw, mapUserSummary);
    },

    /**
     * GET /identity/api/admin/users/{id}
     *
     * Returns full UserDetail, or null if not found.
     * Response is normalised via mapUserDetail.
     *
     * Cache: 30 s  Tag: cc:users
     *   Same lifecycle as users.list.
     */
    getById: async (id: string): Promise<UserDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/users/${encodeURIComponent(id)}`,
          30,
          [CACHE_TAGS.users],
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
     *
     * Cache: 300 s  Tag: cc:roles
     *   Roles are near-static configuration data; 5 min is safe.
     *
     * TODO: add Redis or edge caching
     */
    list: async (): Promise<RoleSummary[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/roles',
        300,
        [CACHE_TAGS.roles],
      );
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
     *
     * Cache: 300 s  Tag: cc:roles
     *   Same lifecycle as roles.list.
     */
    getById: async (id: string): Promise<RoleDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/roles/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.roles],
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
     * Cache: 10 s  Tag: cc:audit
     *   Admins expect near-real-time audit visibility. 10 s prevents
     *   hammering the DB on every keystroke in the search box while
     *   still showing recent entries within one page refresh.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
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
        10,
        [CACHE_TAGS.audit],
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
     * Cache: 300 s  Tag: cc:settings
     *   Settings rarely change; 5 min prevents re-fetching on every
     *   admin page render. On-demand invalidated after settings.update.
     *
     * TODO: integrate with Identity service settings endpoint
     * TODO: add Redis or edge caching
     */
    list: async (): Promise<PlatformSetting[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/settings',
        300,
        [CACHE_TAGS.settings],
      );
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
     * Revalidates: cc:settings — so the next settings.list call
     * sees the updated value without waiting for the 300 s TTL.
     *
     * TODO: integrate with Identity service settings endpoint
     * TODO: add Redis or edge caching
     */
    update: async (key: string, value: string | number | boolean): Promise<PlatformSetting> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/settings/${encodeURIComponent(key)}`,
        { value },
      );
      const result = mapSetting(raw);
      // Purge settings cache so UI shows the new value immediately
      revalidateTag(CACHE_TAGS.settings);
      return result;
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
     * Cache: 5 s  Tag: cc:monitoring
     *   Monitoring is a live feed. 5 s gives the Next.js Data Cache just
     *   enough time to coalesce concurrent requests from multiple SSR
     *   renders (request deduplication) without staling health data.
     *
     * TODO: integrate with Platform monitoring endpoint
     * TODO: add Redis or edge caching
     * TODO: add stale-while-revalidate strategy
     */
    getSummary: async (): Promise<MonitoringSummary> => {
      const raw = await apiClient.get<unknown>(
        '/platform/monitoring/summary',
        5,
        [CACHE_TAGS.monitoring],
      );
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
     * Cache: 10 s  Tag: cc:support
     *   Support case status can change while an admin is viewing the list.
     *   10 s balances freshness vs load; on-demand invalidated by all
     *   support mutations.
     *
     * TODO: integrate with support case endpoint
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
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
        10,
        [CACHE_TAGS.support],
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
     * Cache: 10 s  Tag: cc:support
     *   Same lifecycle as support.list.
     *
     * TODO: integrate with support case endpoint
     */
    getById: async (id: string): Promise<SupportCaseDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/support/${encodeURIComponent(id)}`,
          10,
          [CACHE_TAGS.support],
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
     * Revalidates: cc:support — the new case appears in support.list immediately.
     *
     * TODO: integrate with support case endpoint
     * TODO: add Redis or edge caching
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
      const result = mapSupportCaseDetail(raw);
      // Purge support cache so the new case is visible immediately
      revalidateTag(CACHE_TAGS.support);
      return result;
    },

    /**
     * POST /identity/api/admin/support/{caseId}/notes
     *
     * Adds a note to an existing support case.
     * Response is normalised via mapSupportNote.
     *
     * Revalidates: cc:support — note count and last-updated reflect immediately.
     *
     * TODO: integrate with support case endpoint
     * TODO: add Redis or edge caching
     */
    addNote: async (caseId: string, message: string): Promise<SupportNote> => {
      const raw = await apiClient.post<unknown>(
        `/identity/api/admin/support/${encodeURIComponent(caseId)}/notes`,
        { message },
      );
      const result = mapSupportNote(raw);
      // Purge so case detail reflects the new note immediately
      revalidateTag(CACHE_TAGS.support);
      return result;
    },

    /**
     * PATCH /identity/api/admin/support/{caseId}/status
     *
     * Updates the status of a support case.
     * Response is normalised via mapSupportCase.
     *
     * Revalidates: cc:support — new status visible in list and detail immediately.
     *
     * TODO: integrate with support case endpoint
     * TODO: add Redis or edge caching
     */
    updateStatus: async (caseId: string, status: SupportCaseStatus): Promise<SupportCase> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/support/${encodeURIComponent(caseId)}/status`,
        { status },
      );
      const result = mapSupportCase(raw);
      // Purge so updated status is visible in list and detail immediately
      revalidateTag(CACHE_TAGS.support);
      return result;
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
