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
  mapCanonicalAuditEvent,
  mapSetting,
  mapMonitoring,
  mapSupportCase,
  mapSupportCaseDetail,
  mapSupportNote,
  mapPagedResponse,
  mapOrganizationTypeItem,
  mapRelationshipTypeItem,
  mapOrgRelationship,
  mapProductOrgTypeRule,
  mapProductRelTypeRule,
  mapLegacyCoverageReport,
  mapPlatformReadiness,
  mapCareConnectIntegrity,
  mapScopedRoleAssignment,
  mapAuditExport,
  mapIntegrityCheckpoint,
  mapLegalHold,
  mapGroupSummary,
  mapGroupDetail,
  mapPermissionCatalogItem,
  unwrapApiResponse,
  unwrapApiResponseList,
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
  CanonicalAuditEvent,
  PlatformSetting,
  MonitoringSummary,
  SupportCase,
  SupportCaseDetail,
  SupportCaseStatus,
  SupportNote,
  PagedResponse,
  OrganizationTypeItem,
  RelationshipTypeItem,
  OrgRelationship,
  ProductOrgTypeRule,
  ProductRelTypeRule,
  LegacyCoverageReport,
  PlatformReadinessSummary,
  CareConnectIntegrityReport,
  ScopedRoleAssignment,
  AuditExport,
  IntegrityCheckpoint,
  LegalHold,
  AuditIngestPayload,
  GroupSummary,
  GroupDetail,
  PermissionCatalogItem,
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
      revalidateTag(CACHE_TAGS.tenants);
      return result;
    },

    /**
     * PATCH /identity/api/admin/tenants/{id}/session-settings
     *
     * Updates the per-tenant idle session timeout.
     * Pass null to reset to the platform default (30 minutes).
     */
    updateSessionSettings: async (
      tenantId: string,
      sessionTimeoutMinutes: number | null,
    ): Promise<void> => {
      await apiClient.patch<unknown>(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/session-settings`,
        { sessionTimeoutMinutes },
      );
      revalidateTag(CACHE_TAGS.tenants);
    },

    /**
     * POST /identity/api/admin/tenants
     *
     * Creates a new tenant with a default admin user. Returns the new tenant's
     * ID/code/name plus the one-time temporary password for the admin user.
     *
     * Revalidates: cc:tenants so the list refreshes immediately.
     */
    create: async (body: {
      name:           string;
      code:           string;
      orgType:        string;
      adminEmail:     string;
      adminFirstName: string;
      adminLastName:  string;
    }): Promise<{
      tenantId:          string;
      displayName:       string;
      code:              string;
      status:            string;
      adminUserId:       string;
      adminEmail:        string;
      temporaryPassword: string;
    }> => {
      const raw = await apiClient.post<{
        tenantId:          string;
        displayName:       string;
        code:              string;
        status:            string;
        adminUserId:       string;
        adminEmail:        string;
        temporaryPassword: string;
      }>('/identity/api/admin/tenants', body);
      revalidateTag(CACHE_TAGS.tenants);
      return raw;
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

    /**
     * POST /identity/api/admin/users/{id}/activate
     * Activates an inactive user. Revalidates cc:users cache.
     */
    activate: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/activate`,
        {},
      );
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/deactivate
     * Deactivates an active user. Revalidates cc:users cache.
     */
    deactivate: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/deactivate`,
        {},
      );
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/invite
     * Sends an invitation to a new user. Revalidates cc:users cache.
     */
    invite: async (payload: {
      email:          string;
      firstName:      string;
      lastName:       string;
      tenantId:       string;
      organizationId?: string;
      memberRole?:    string;
    }): Promise<void> => {
      await apiClient.post<unknown>('/identity/api/admin/users/invite', payload);
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/resend-invite
     * Resends a pending invitation. Revalidates cc:users cache.
     */
    resendInvite: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/resend-invite`,
        {},
      );
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/memberships
     * Assigns the user to an organization. Revalidates cc:users cache.
     */
    assignMembership: async (id: string, payload: {
      organizationId: string;
      memberRole?:    string;
    }): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/memberships`,
        payload,
      );
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/memberships/{membershipId}/set-primary
     * Marks an org membership as the user's primary org. Revalidates cc:users cache.
     */
    setPrimaryMembership: async (id: string, membershipId: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/memberships/${encodeURIComponent(membershipId)}/set-primary`,
        {},
      );
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * DELETE /identity/api/admin/users/{id}/memberships/{membershipId}
     * Removes an org membership from the user. Revalidates cc:users cache.
     */
    removeMembership: async (id: string, membershipId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/memberships/${encodeURIComponent(membershipId)}`,
      );
      revalidateTag(CACHE_TAGS.users);
    },
  },

  // ── Groups ────────────────────────────────────────────────────────────────

  groups: {
    /**
     * GET /identity/api/admin/groups?tenantId=&page=&pageSize=
     * Lists groups for a tenant. Cache: 60 s, tag cc:users.
     */
    list: async (params: {
      tenantId?: string;
      page?:     number;
      pageSize?: number;
    } = {}): Promise<PagedResponse<GroupSummary>> => {
      const qs = toQs({
        tenantId: params.tenantId,
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
      });
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/groups${qs}`,
        60,
        [CACHE_TAGS.users],
      );
      return mapPagedResponse(raw, mapGroupSummary);
    },

    /**
     * GET /identity/api/admin/groups/{id}
     * Returns full GroupDetail including members, or null if not found.
     */
    getById: async (id: string): Promise<GroupDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/groups/${encodeURIComponent(id)}`,
          30,
          [CACHE_TAGS.users],
        );
        return mapGroupDetail(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /identity/api/admin/groups
     * Creates a new tenant group. Revalidates cc:users cache.
     */
    create: async (payload: {
      tenantId:     string;
      name:         string;
      description?: string;
    }): Promise<void> => {
      await apiClient.post<unknown>('/identity/api/admin/groups', payload);
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/groups/{id}/members
     * Adds a user to a group. Revalidates cc:users cache.
     */
    addMember: async (groupId: string, userId: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/groups/${encodeURIComponent(groupId)}/members`,
        { userId },
      );
      revalidateTag(CACHE_TAGS.users);
    },

    /**
     * DELETE /identity/api/admin/groups/{id}/members/{membershipId}
     * Removes a member from a group. Revalidates cc:users cache.
     */
    removeMember: async (groupId: string, membershipId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/admin/groups/${encodeURIComponent(groupId)}/members/${encodeURIComponent(membershipId)}`,
      );
      revalidateTag(CACHE_TAGS.users);
    },
  },

  // ── Permissions ───────────────────────────────────────────────────────────

  permissions: {
    /**
     * GET /identity/api/admin/permissions
     * Returns the full platform permission catalog. Cache: 300 s.
     */
    list: async (): Promise<PermissionCatalogItem[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/permissions',
        300,
        [CACHE_TAGS.users],
      );
      return Array.isArray(raw) ? raw.map(mapPermissionCatalogItem) : [];
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

  // ── Canonical Audit (Platform Audit Event Service) ────────────────────────

  auditCanonical: {
    /**
     * GET /audit-service/audit/events
     *
     * Queries the canonical Platform Audit Event Service (port 5007) via the
     * API gateway route /audit-service/...
     *
     * Supports rich filtering: tenantId, eventType, category, severity, actorId,
     * targetType, targetId, correlationId, dateFrom, dateTo, page, pageSize.
     *
     * Cache: 10 s  Tag: cc:audit-canonical
     *
     * AUDIT_READ_MODE env controls which source the audit-logs page uses:
     *   legacy    → only GET /identity/api/admin/audit   (default)
     *   canonical → only GET /audit-service/audit/events
     *   hybrid    → canonical first, fall back to legacy on error
     */
    list: async (params: {
      page?:          number;
      pageSize?:      number;
      tenantId?:      string;
      eventType?:     string;
      category?:      string;
      severity?:      string;
      actorId?:       string;
      targetType?:    string;
      targetId?:      string;
      correlationId?: string;
      dateFrom?:      string;
      dateTo?:        string;
      search?:        string;
    } = {}): Promise<{ items: CanonicalAuditEvent[]; totalCount: number }> => {
      const qs = toQs({
        page:          params.page        ?? 1,
        pageSize:      params.pageSize    ?? 15,
        tenantId:      params.tenantId,
        eventType:     params.eventType,
        category:      params.category,
        severity:      params.severity,
        actorId:       params.actorId,
        targetType:    params.targetType,
        targetId:      params.targetId,
        correlationId: params.correlationId,
        dateFrom:      params.dateFrom,
        dateTo:        params.dateTo,
        search:        params.search,
      });
      const raw = await apiClient.get<unknown>(
        `/audit-service/audit/events${qs}`,
        10,
        [CACHE_TAGS.auditCanonical],
      );
      const paged = mapPagedResponse(raw, mapCanonicalAuditEvent);
      return { items: paged.items, totalCount: paged.totalCount };
    },

    /**
     * GET /audit-service/audit/events/{auditId}
     *
     * Fetches a single canonical audit event by its stable auditId.
     * Cache: 30 s  Tag: cc:audit-canonical
     */
    getById: async (auditId: string): Promise<CanonicalAuditEvent | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/audit-service/audit/events/${encodeURIComponent(auditId)}`,
          30,
          [CACHE_TAGS.auditCanonical],
        );
        if (!raw) return null;
        return mapCanonicalAuditEvent(unwrapApiResponse(raw));
      } catch {
        return null;
      }
    },
  },

  // ── SynqAudit — Exports ───────────────────────────────────────────────────

  auditExports: {
    /**
     * POST /audit-service/audit/exports
     *
     * Submits an asynchronous export job. Returns the export status object.
     * Cache: no-store (mutations)
     */
    create: async (params: {
      format:                 'Json' | 'Csv' | 'Ndjson';
      tenantId?:              string;
      eventType?:             string;
      category?:              string;
      severity?:              string;
      correlationId?:         string;
      dateFrom?:              string;
      dateTo?:                string;
      includeStateSnapshots?: boolean;
      includeTags?:           boolean;
    }): Promise<AuditExport> => {
      const raw = await apiClient.post<unknown>(
        '/audit-service/audit/exports',
        { ...params },
      );
      return mapAuditExport(raw);
    },

    /**
     * GET /audit-service/audit/exports/{exportId}
     *
     * Polls the status of a previously submitted export job.
     * Cache: 5 s  Tag: cc:audit-exports
     */
    getById: async (exportId: string): Promise<AuditExport | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/audit-service/audit/exports/${encodeURIComponent(exportId)}`,
          5,
          [CACHE_TAGS.auditExports],
        );
        return mapAuditExport(raw);
      } catch {
        return null;
      }
    },
  },

  // ── SynqAudit — Integrity ─────────────────────────────────────────────────

  auditIntegrity: {
    /**
     * GET /audit-service/audit/integrity/checkpoints
     *
     * Lists persisted integrity hash checkpoints.
     * Cache: 30 s  Tag: cc:audit-integrity
     */
    list: async (): Promise<IntegrityCheckpoint[]> => {
      const raw = await apiClient.get<unknown>(
        '/audit-service/audit/integrity/checkpoints',
        30,
        [CACHE_TAGS.auditIntegrity],
      );
      return unwrapApiResponseList(raw).map(mapIntegrityCheckpoint);
    },

    /**
     * POST /audit-service/audit/integrity/checkpoints/generate
     *
     * Generates a new integrity checkpoint on demand.
     */
    generate: async (params: {
      checkpointType?:    string;
      fromRecordedAtUtc?: string;
      toRecordedAtUtc?:   string;
    } = {}): Promise<IntegrityCheckpoint> => {
      const raw = await apiClient.post<unknown>(
        '/audit-service/audit/integrity/checkpoints/generate',
        params,
      );
      return mapIntegrityCheckpoint(raw);
    },
  },

  // ── SynqAudit — Legal Holds ───────────────────────────────────────────────

  auditLegalHolds: {
    /**
     * GET /audit-service/audit/legal-holds/record/{auditId}
     *
     * Lists all legal holds for a specific audit record.
     * Cache: 10 s  Tag: cc:audit-legal-holds
     */
    listForRecord: async (auditId: string): Promise<LegalHold[]> => {
      const raw = await apiClient.get<unknown>(
        `/audit-service/audit/legal-holds/record/${encodeURIComponent(auditId)}`,
        10,
        [CACHE_TAGS.auditLegalHolds],
      );
      return unwrapApiResponseList(raw).map(mapLegalHold);
    },

    /**
     * POST /audit-service/audit/legal-holds/{auditId}
     *
     * Places a legal hold on an audit record.
     */
    create: async (auditId: string, params: {
      legalAuthority: string;
      notes?:         string;
    }): Promise<LegalHold> => {
      const raw = await apiClient.post<unknown>(
        `/audit-service/audit/legal-holds/${encodeURIComponent(auditId)}`,
        params,
      );
      return mapLegalHold(raw);
    },

    /**
     * POST /audit-service/audit/legal-holds/{holdId}/release
     *
     * Releases an active legal hold.
     */
    release: async (holdId: string): Promise<LegalHold> => {
      const raw = await apiClient.post<unknown>(
        `/audit-service/audit/legal-holds/${encodeURIComponent(holdId)}/release`,
        {},
      );
      return mapLegalHold(raw);
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

  // ── Organization Types (Phase E) ────────────────────────────────────────────
  /**
   * GET /identity/api/admin/organization-types
   *
   * Returns all active OrganizationType catalog entries.
   * Cache tag: cc:org-types, TTL: 300 s (near-static reference data).
   */
  organizationTypes: {
    list: async (): Promise<OrganizationTypeItem[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/organization-types',
        300,
        [CACHE_TAGS.orgTypes],
      );
      return Array.isArray(raw) ? raw.map(mapOrganizationTypeItem) : [];
    },

    getById: async (id: string): Promise<OrganizationTypeItem | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/organization-types/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.orgTypes],
        );
        return mapOrganizationTypeItem(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Relationship Types (Phase E) ─────────────────────────────────────────────
  /**
   * GET /identity/api/admin/relationship-types
   *
   * Returns all active RelationshipType catalog entries.
   * Cache tag: cc:rel-types, TTL: 300 s (near-static reference data).
   */
  relationshipTypes: {
    list: async (): Promise<RelationshipTypeItem[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/relationship-types',
        300,
        [CACHE_TAGS.relTypes],
      );
      return Array.isArray(raw) ? raw.map(mapRelationshipTypeItem) : [];
    },

    getById: async (id: string): Promise<RelationshipTypeItem | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/relationship-types/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.relTypes],
        );
        return mapRelationshipTypeItem(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Organization Relationships (Phase E) ─────────────────────────────────────
  /**
   * GET /identity/api/admin/organization-relationships
   *
   * Returns all OrganizationRelationship records (optionally filtered).
   * Cache tag: cc:org-relationships, TTL: 60 s.
   */
  organizationRelationships: {
    list: async (params?: {
      sourceOrgId?:       string;
      targetOrgId?:       string;
      relationshipTypeId?: string;
      activeOnly?:        boolean;
      page?:              number;
      pageSize?:          number;
    }): Promise<PagedResponse<OrgRelationship>> => {
      const qs  = toQs(params ?? {});
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/organization-relationships${qs}`,
        60,
        [CACHE_TAGS.orgRelationships],
      );
      return mapPagedResponse(raw, mapOrgRelationship);
    },

    getById: async (id: string): Promise<OrgRelationship | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/organization-relationships/${encodeURIComponent(id)}`,
          60,
          [CACHE_TAGS.orgRelationships],
        );
        return mapOrgRelationship(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Product–OrgType Rules (Phase E) ──────────────────────────────────────────
  /**
   * GET /identity/api/admin/product-org-type-rules
   *
   * Returns all ProductOrganizationTypeRule entries.
   * Cache tag: cc:product-org-type-rules, TTL: 300 s.
   */
  productOrgTypeRules: {
    list: async (params?: {
      productId?:          string;
      organizationTypeId?: string;
      activeOnly?:         boolean;
    }): Promise<ProductOrgTypeRule[]> => {
      const qs  = toQs(params ?? {});
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/product-org-type-rules${qs}`,
        300,
        [CACHE_TAGS.productOrgTypeRules],
      );
      return Array.isArray(raw) ? raw.map(mapProductOrgTypeRule) : [];
    },
  },

  // ── Product–RelType Rules (Phase E) ──────────────────────────────────────────
  /**
   * GET /identity/api/admin/product-rel-type-rules
   *
   * Returns all ProductRelationshipTypeRule entries.
   * Cache tag: cc:product-rel-type-rules, TTL: 300 s.
   */
  productRelTypeRules: {
    list: async (params?: {
      productId?:         string;
      relationshipTypeId?: string;
      activeOnly?:        boolean;
    }): Promise<ProductRelTypeRule[]> => {
      const qs  = toQs(params ?? {});
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/product-rel-type-rules${qs}`,
        300,
        [CACHE_TAGS.productRelTypeRules],
      );
      return Array.isArray(raw) ? raw.map(mapProductRelTypeRule) : [];
    },
  },

  // ── Legacy Coverage (Phase G) ──────────────────────────────────────────────
  /**
   * GET /identity/api/admin/legacy-coverage
   *
   * Returns a point-in-time snapshot of eligibility-rule migration coverage.
   * Phase G: roleAssignments now reflects the SRA-only (retired dual-write) shape.
   *
   * Short TTL (10 s) — diagnostic/admin page, not a hot-path.
   * Cache tag: cc:legacy-coverage.
   */
  legacyCoverage: {
    get: async (): Promise<LegacyCoverageReport> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/legacy-coverage`,
        10,
        [CACHE_TAGS.legacyCoverage],
      );
      return mapLegacyCoverageReport(raw);
    },
  },

  // ── Platform Readiness (Phase 8) ──────────────────────────────────────────
  /**
   * GET /identity/api/admin/platform-readiness
   *
   * Returns a cross-domain readiness summary covering:
   *   • Phase G completion (UserRoles retired, SRA sole source)
   *   • OrgType consistency (OrganizationTypeId FK coverage)
   *   • ProductRole eligibility coverage (OrgTypeRule %)
   *   • Organization relationship statistics
   *
   * Short TTL (30 s) — diagnostic dashboard endpoint.
   * Cache tag: cc:platform-readiness.
   */
  platformReadiness: {
    get: async (): Promise<PlatformReadinessSummary> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/platform-readiness`,
        30,
        [CACHE_TAGS.platformReadiness],
      );
      return mapPlatformReadiness(raw);
    },
  },

  // ── CareConnect Integrity ────────────────────────────────────────────────────
  /**
   * GET /careconnect/api/admin/integrity
   *
   * Returns operational integrity counters for CareConnect entities.
   * The backend never throws — query failures produce -1 for that counter.
   *
   * Cache: 10 s  Tag: cc:careconnect-integrity
   *   Short TTL — integrity issues should surface quickly in the admin dashboard.
   */
  careConnectIntegrity: {
    get: async (): Promise<CareConnectIntegrityReport> => {
      const raw = await apiClient.get<unknown>(
        '/careconnect/api/admin/integrity',
        10,
        [CACHE_TAGS.ccIntegrity],
      );
      return mapCareConnectIntegrity(raw);
    },
  },

  // ── Scoped Role Assignments (per-user) ───────────────────────────────────────
  /**
   * GET /identity/api/admin/users/{id}/scoped-roles
   *
   * Returns all active ScopedRoleAssignments for a specific user.
   * There is no global list endpoint — scoped roles are always user-scoped.
   */
  scopedRoles: {
    getByUser: async (userId: string): Promise<ScopedRoleAssignment[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(userId)}/scoped-roles`,
        30,
        [CACHE_TAGS.users],
      );
      return Array.isArray(raw) ? raw.map(mapScopedRoleAssignment) : [];
    },
  },

  // ── SynqAudit — Event Ingest ──────────────────────────────────────────────

  auditIngest: {
    /**
     * POST /audit-service/audit/ingest
     *
     * Emits a canonical audit event directly to the Platform Audit Event Service.
     * Used by server-side actions (e.g. impersonation) that run outside the
     * Identity service and cannot use IAuditEventClient directly.
     *
     * Fire-and-observe: callers should not await the return value if they want
     * to avoid gating user-facing operations on the audit pipeline.
     */
    emit: async (payload: AuditIngestPayload): Promise<void> => {
      await apiClient.post<unknown>('/audit-service/audit/ingest', payload);
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
