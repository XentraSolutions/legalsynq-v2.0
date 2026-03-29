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

export type EntitlementStatus = 'Enabled' | 'Disabled' | 'NotProvisioned';

/**
 * A single product entitlement for a tenant.
 * Used inside TenantDetail.productEntitlements.
 */
export interface ProductEntitlementSummary {
  productCode:   string;
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

export interface AuditLogEntry {
  id:            string;
  tenantId?:     string;
  actorId?:      string;
  actorEmail?:   string;
  action:        string;
  entityType:    string;
  entityId?:     string;
  detail?:       string;
  occurredAtUtc: string;
}

// ── Platform Settings ─────────────────────────────────────────────────────────

export interface PlatformSetting {
  key:          string;
  value:        string;
  description:  string;
  isSecret:     boolean;
  updatedAtUtc: string;
}

// ── Monitoring ────────────────────────────────────────────────────────────────

export interface SystemHealthSummary {
  serviceName:  string;
  status:       'ok' | 'degraded' | 'down' | 'unknown';
  version?:     string;
  environment?: string;
  checkedAtUtc: string;
}

// ── Shared ────────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  items:      T[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}
