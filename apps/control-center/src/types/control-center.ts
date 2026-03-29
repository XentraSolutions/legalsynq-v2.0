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
 * Extends UserSummary with audit timestamps.
 */
export interface UserDetail extends UserSummary {
  createdAtUtc: string;
  updatedAtUtc: string;
}

// ── Roles & Permissions ───────────────────────────────────────────────────────

export interface RoleSummary {
  id:           string;
  code:         string;
  name:         string;
  productCode:  string;
  productName:  string;
  capabilities: string[];
  isActive:     boolean;
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
