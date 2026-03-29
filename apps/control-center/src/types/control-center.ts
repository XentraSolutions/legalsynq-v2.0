// ── Control Center domain types ────────────────────────────────────────────────
// These mirror the expected Identity service response shapes for admin endpoints.
// Keep in sync with Identity.Application DTOs as backend endpoints are confirmed.

// ── Tenants ───────────────────────────────────────────────────────────────────

export type TenantType   = 'LawFirm' | 'Provider' | 'Corporate' | 'Government' | 'Other';
export type TenantStatus = 'Active' | 'Inactive' | 'Suspended';

export interface TenantSummary {
  id:                 string;
  code:               string;
  displayName:        string;
  type:               TenantType;
  status:             TenantStatus;
  primaryContactName: string;
  isActive:           boolean;
  userCount:          number;
  orgCount:           number;
  createdAtUtc:       string;
}

/**
 * Full tenant record returned by GET /identity/api/admin/tenants/{id}.
 * Extends TenantSummary with enriched fields not present in the list view.
 */
export interface TenantDetail extends TenantSummary {
  email?:              string;
  updatedAtUtc:        string;
  activeUserCount:     number;
  linkedOrgCount?:     number;
  productEntitlements: ProductEntitlementSummary[];
}

// ── Product Entitlements ──────────────────────────────────────────────────────

/**
 * Canonical product identifiers used across the LegalSynq platform.
 * Must match values emitted by Identity service entitlement endpoints.
 */
export type ProductCode =
  | 'SynqFund'
  | 'SynqLien'
  | 'SynqBill'
  | 'SynqRx'
  | 'SynqPayout'
  | 'CareConnect';

/** Live status of a product entitlement for a tenant. */
export type EntitlementStatus = 'Active' | 'Disabled';

/**
 * A single product entitlement for a tenant.
 * Used inside TenantDetail.productEntitlements.
 */
export interface ProductEntitlementSummary {
  productCode:   ProductCode;
  productName:   string;
  enabled:       boolean;
  status:        EntitlementStatus;
  enabledAtUtc?: string;
}

// ── Users ─────────────────────────────────────────────────────────────────────

export type UserStatus = 'Active' | 'Inactive' | 'Invited';

/**
 * User record returned by GET /identity/api/admin/users.
 * Represents a single user within a tenant as seen from the platform admin view.
 */
export interface UserSummary {
  id:              string;
  firstName:       string;
  lastName:        string;
  email:           string;
  role:            string;
  status:          UserStatus;
  tenantId:        string;
  tenantCode:      string;
  lastLoginAtUtc?: string;
}

/**
 * Full user record returned by GET /identity/api/admin/users/{id}.
 * Extends UserSummary with audit timestamps and account state.
 *
 * tenantDisplayName is included so the detail page can link to the tenant
 * without requiring a second API call.
 */
export interface UserDetail extends UserSummary {
  tenantDisplayName: string;
  createdAtUtc:      string;
  updatedAtUtc:      string;
  isLocked?:         boolean;
  inviteSentAtUtc?:  string;
}

// ── Roles & Permissions ───────────────────────────────────────────────────────

/**
 * A single granular permission in the platform RBAC model.
 * Permissions are additive — roles are a named collection of permissions.
 */
export interface Permission {
  id:          string;
  key:         string;   // e.g. "tenants.activate"
  description: string;
}

/**
 * Role record returned by GET /identity/api/admin/roles.
 * Platform-level roles only — tenant-level roles are separate.
 */
export interface RoleSummary {
  id:          string;
  name:        string;
  description: string;
  userCount:   number;
  permissions: string[];   // permission keys
}

/**
 * Full role record returned by GET /identity/api/admin/roles/{id}.
 * Extends RoleSummary with audit timestamps and resolved permission objects.
 */
export interface RoleDetail extends RoleSummary {
  createdAtUtc:        string;
  updatedAtUtc:        string;
  resolvedPermissions: Permission[];
}

// ── Audit Logs ────────────────────────────────────────────────────────────────

/** Who originated an audited action. */
export type ActorType = 'Admin' | 'System';

/**
 * A single audit log entry.
 * Returned by GET /identity/api/admin/audit (paged).
 *
 * actorName  — display name of the actor (email for Admins, service name for System).
 * actorType  — distinguishes human admins from automated/system events.
 * entityType — the domain object affected: "Tenant", "User", "Role", "Entitlement".
 * entityId   — id or code of the affected record.
 * metadata   — arbitrary key/value context captured at event time.
 * createdAtUtc — ISO 8601 timestamp in UTC.
 */
export interface AuditLogEntry {
  id:           string;
  actorName:    string;
  actorType:    ActorType;
  action:       string;
  entityType:   string;
  entityId:     string;
  metadata?:    Record<string, unknown>;
  createdAtUtc: string;
}

// ── Platform Settings ─────────────────────────────────────────────────────────

export interface PlatformSetting {
  key:          string;
  label:        string;
  value:        string | number | boolean;
  type:         'boolean' | 'string' | 'number';
  description?: string;
  editable:     boolean;
}

// ── Monitoring ────────────────────────────────────────────────────────────────

export type MonitoringStatus = 'Healthy' | 'Degraded' | 'Down';
export type AlertSeverity    = 'Info' | 'Warning' | 'Critical';

export interface SystemHealthSummary {
  status:           MonitoringStatus;
  lastCheckedAtUtc: string;
}

export interface IntegrationStatus {
  name:             string;
  status:           MonitoringStatus;
  latencyMs?:       number;
  lastCheckedAtUtc: string;
}

export interface SystemAlert {
  id:           string;
  message:      string;
  severity:     AlertSeverity;
  createdAtUtc: string;
}

export interface MonitoringSummary {
  system:       SystemHealthSummary;
  integrations: IntegrationStatus[];
  alerts:       SystemAlert[];
}

// ── Support Tools ─────────────────────────────────────────────────────────────

export type SupportCaseStatus   = 'Open' | 'Investigating' | 'Resolved' | 'Closed';
export type SupportCasePriority = 'Low' | 'Medium' | 'High';

export interface SupportCase {
  id:           string;
  title:        string;
  tenantId:     string;
  tenantName:   string;
  userId?:      string;
  userName?:    string;
  status:       SupportCaseStatus;
  category:     string;
  priority:     SupportCasePriority;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SupportNote {
  id:           string;
  caseId:       string;
  message:      string;
  createdBy:    string;
  createdAtUtc: string;
}

export interface SupportCaseDetail extends SupportCase {
  notes: SupportNote[];
}

// ── Tenant Context / Impersonation ────────────────────────────────────────────

/**
 * TenantContext — identifies which tenant the platform admin is currently
 * "scoped to" for context-switching and impersonation flows.
 *
 * Stored in the cc_tenant_context cookie and consumed by pages/actions that
 * need to filter data by a specific tenant without a full login as that tenant.
 */
export interface TenantContext {
  tenantId:   string;
  tenantName: string;
  tenantCode: string;
}

/**
 * ImpersonationSession — records a live impersonation started by a platform
 * admin acting as a tenant.
 *
 * originalAdminId       — userId of the PlatformAdmin who started impersonation.
 * impersonatedTenantId  — id of the tenant whose context is active.
 * impersonatedTenantName — display name kept for UI banners.
 * startedAtUtc          — ISO 8601 timestamp in UTC.
 *
 * TODO: persist impersonation session in backend and emit audit log entry
 */
export interface ImpersonationSession {
  originalAdminId:        string;
  impersonatedTenantId:   string;
  impersonatedTenantName: string;
  startedAtUtc:           string;
}

// ── Shared ────────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  items:      T[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}
