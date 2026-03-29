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
 *
 * For the standalone platform-wide entitlements list (future),
 * the backend will likely extend this with tenantId / tenantCode.
 */
export interface ProductEntitlementSummary {
  productCode:   string;
  productName:   string;
  enabled:       boolean;
  status:        EntitlementStatus;
  enabledAtUtc?: string;
}

// ── Tenant Users ──────────────────────────────────────────────────────────────

export interface TenantUserSummary {
  id:           string;
  email:        string;
  firstName:    string;
  lastName:     string;
  isActive:     boolean;
  systemRoles:  string[];
  orgName?:     string;
  createdAtUtc: string;
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
