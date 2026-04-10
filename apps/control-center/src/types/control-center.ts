// ── Control Center domain types ────────────────────────────────────────────────
// These mirror the expected Identity service response shapes for admin endpoints.
// Keep in sync with Identity.Application DTOs as backend endpoints are confirmed.

// ── Tenants ───────────────────────────────────────────────────────────────────

export type TenantType   = 'LawFirm' | 'Provider' | 'Funder' | 'LienOwner' | 'Corporate' | 'Government' | 'Other';
export type TenantStatus = 'Active' | 'Inactive' | 'Suspended';
export type ProvisioningStatus = 'Pending' | 'InProgress' | 'Provisioned' | 'Verifying' | 'Active' | 'Failed';
export type ProvisioningFailureStage = 'None' | 'DnsProvisioning' | 'DnsVerification' | 'HttpVerification';

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
  subdomain?:         string;
  provisioningStatus?: ProvisioningStatus;
}

/**
 * Full tenant record returned by GET /identity/api/admin/tenants/{id}.
 * Extends TenantSummary with enriched fields not present in the list view.
 */
export interface TenantDetail extends TenantSummary {
  email?:                     string;
  updatedAtUtc:               string;
  activeUserCount:            number;
  linkedOrgCount?:            number;
  sessionTimeoutMinutes?:     number;
  logoDocumentId?:            string;
  productEntitlements:        ProductEntitlementSummary[];
  lastProvisioningAttemptUtc?: string;
  provisioningFailureReason?: string;
  provisioningFailureStage?:  ProvisioningFailureStage;
  hostname?:                  string;
  verificationAttemptCount?:       number;
  lastVerificationAttemptUtc?:     string;
  nextVerificationRetryAtUtc?:     string;
  isVerificationRetryExhausted?:   boolean;
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
  primaryOrg?:     string;
  groupCount?:     number;
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
  lockedAtUtc?:      string;
  lastLoginAtUtc?:   string;
  sessionVersion?:   number;
  avatarDocumentId?: string;
  inviteSentAtUtc?:  string;
  memberships?:      OrgMembershipSummary[];
  groups?:           UserGroupSummary[];
  roles?:            UserRoleSummary[];
}

/**
 * UIX-003-03: Security summary for a user — returned by GET /security.
 */
export interface UserSecurity {
  userId:          string;
  email:           string;
  isLocked:        boolean;
  lockedAtUtc:     string | null;
  lastLoginAtUtc:  string | null;
  sessionVersion:  number;
  isActive:        boolean;
  hasPendingInvite: boolean;
  recentPasswordResets: PasswordResetSummary[];
}

export interface PasswordResetSummary {
  id:        string;
  status:    'PENDING' | 'USED' | 'EXPIRED' | 'REVOKED';
  createdAt: string;
  expiresAt: string;
  usedAt:    string | null;
}

export interface OrgSummary {
  id:          string;
  tenantId:    string;
  name:        string;
  displayName: string;
  orgType:     string;
  isActive:    boolean;
}

export interface OrgMembershipSummary {
  membershipId:   string;
  organizationId: string;
  orgName:        string;
  memberRole:     string;
  isPrimary:      boolean;
  joinedAtUtc:    string;
}

export interface UserGroupSummary {
  groupId:     string;
  groupName:   string;
  joinedAtUtc: string;
}

export interface UserRoleSummary {
  roleId:       string;
  roleName:     string;
  assignmentId: string;
}

// ── Access Groups (LS-COR-AUT-004 / LS-COR-AUT-005) ─────────────────────────

export type AccessGroupStatus = 'Active' | 'Archived';
export type AccessGroupScopeType = 'Tenant' | 'Product' | 'Organization';
export type AccessGroupMembershipStatus = 'Active' | 'Removed';
export type GroupProductAccessStatus = 'Granted' | 'Revoked';
export type GroupRoleAssignmentStatus = 'Active' | 'Removed';

export interface AccessGroupSummary {
  id:              string;
  tenantId:        string;
  name:            string;
  description?:    string;
  status:          AccessGroupStatus;
  scopeType:       AccessGroupScopeType;
  productCode?:    string;
  organizationId?: string;
  createdAtUtc:    string;
  updatedAtUtc:    string;
}

export interface AccessGroupMember {
  id:               string;
  tenantId:         string;
  groupId:          string;
  userId:           string;
  membershipStatus: AccessGroupMembershipStatus;
  addedAtUtc:       string;
  removedAtUtc?:    string;
}

export interface GroupProductAccess {
  id:           string;
  tenantId:     string;
  groupId:      string;
  productCode:  string;
  accessStatus: GroupProductAccessStatus;
  grantedAtUtc: string;
  revokedAtUtc?: string;
}

export interface GroupRoleAssignment {
  id:               string;
  tenantId:         string;
  groupId:          string;
  roleCode:         string;
  productCode?:     string;
  organizationId?:  string;
  assignmentStatus: GroupRoleAssignmentStatus;
  assignedAtUtc:    string;
  removedAtUtc?:    string;
}

// ── Permissions catalog ────────────────────────────────────────────────────────

export interface PermissionCatalogItem {
  id:          string;
  code:        string;
  name:        string;
  description?: string;
  productId:   string;
  productName: string;
  isActive:    boolean;
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
  id:              string;
  name:            string;
  description:     string;
  isSystemRole:    boolean;
  isProductRole?:  boolean;
  productCode?:    string;
  productName?:    string;
  allowedOrgTypes?: string[];
  userCount:       number;
  capabilityCount: number;
  permissions:     string[];   // permission keys
}

export interface AssignableRole {
  id:              string;
  name:            string;
  description:     string;
  isSystemRole:    boolean;
  isProductRole:   boolean;
  productCode:     string | null;
  productName:     string | null;
  allowedOrgTypes: string[] | null;
  assignable:      boolean;
  disabledReason:  string | null;
  isAssigned:      boolean;
}

export interface AssignableRolesResponse {
  items:                 AssignableRole[];
  userOrgType:           string;
  tenantEnabledProducts: number;
}

/**
 * Full role record returned by GET /identity/api/admin/roles/{id}.
 * Extends RoleSummary with audit timestamps and resolved permission objects.
 */
export interface RoleDetail extends RoleSummary {
  createdAtUtc:        string;
  updatedAtUtc:        string;
  capabilityCount:     number;
  resolvedPermissions: Permission[];
}

// ── UIX-005: Role Capability Assignment ────────────────────────────────────────

/**
 * A capability assigned to a role.
 * Returned by GET /identity/api/admin/roles/{id}/permissions.
 * Extends PermissionCatalogItem with assignment metadata.
 */
export interface RoleCapabilityItem extends PermissionCatalogItem {
  assignedAtUtc:    string;
  assignedByUserId: string | null;
}

/**
 * A permission source — which role or group grants a capability.
 */
export interface PermissionSource {
  type: 'role' | 'group';
  name: string;
}

/**
 * An effective permission for a user — the union of capabilities across
 * all their active role assignments, with attribution sources.
 * Returned by GET /identity/api/admin/users/{id}/permissions.
 */
export interface EffectivePermission extends PermissionCatalogItem {
  sources: PermissionSource[];
}

/**
 * Result shape from GET /identity/api/admin/users/{id}/permissions.
 */
export interface EffectivePermissionsResult {
  items:      EffectivePermission[];
  totalCount: number;
  roleCount:  number;
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

/**
 * UIX-004: A normalised activity event for display in the UserActivityPanel.
 * Populated from either AuditLogEntry (identity local) or CanonicalAuditEvent.
 */
export interface UserActivityEvent {
  id:           string;
  label:        string;
  eventType:    string;
  category:     string;
  actorLabel:   string;
  actorType:    string;
  occurredAtUtc: string;
  description?: string;
  ipAddress?:   string;
}

/** Source mode for the audit log page. Set via AUDIT_READ_MODE env var. */
export type AuditReadMode = 'legacy' | 'canonical' | 'hybrid';

/** Canonical event from the Platform Audit Event Service query API. */
export interface CanonicalAuditEvent {
  id:             string;
  source:         string;
  sourceService?: string;
  eventType:      string;
  category:       string;
  severity:       string;
  tenantId?:      string;
  actorId?:       string;
  actorLabel?:    string;
  actorType?:     string;
  targetType?:    string;
  targetId?:      string;
  action?:        string;
  description:    string;
  before?:        string;
  after?:         string;
  outcome:        string;
  ipAddress?:     string;
  correlationId?: string;
  requestId?:     string;
  sessionId?:     string;
  metadata?:      string;
  tags?:          string[];
  occurredAtUtc:  string;
  ingestedAtUtc:  string;
  hash?:          string;
}

// ── SynqAudit — Event Ingest (server-side canonical emission) ─────────────────

/** Payload for POST /audit-service/audit/ingest — used by CC server actions. */
export interface AuditIngestPayload {
  eventType:     string;
  eventCategory: string;
  sourceSystem:  string;
  sourceService: string;
  visibility:    string;
  severity:      string;
  occurredAtUtc: string;
  scope: {
    scopeType: string;
    tenantId?: string;
  };
  actor: {
    id?:    string;
    type:   string;
    label?: string;
  };
  entity?: {
    type: string;
    id?:  string;
  };
  action?:        string;
  description?:   string;
  before?:        string;
  after?:         string;
  idempotencyKey?: string;
  tags?:          string[];
}

// ── SynqAudit — Exports ───────────────────────────────────────────────────────

export type AuditExportStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';
export type AuditExportFormat = 'Json' | 'Csv' | 'Ndjson';

export interface AuditExport {
  exportId:       string;
  status:         AuditExportStatus;
  format:         string;
  recordCount?:   number;
  downloadUrl?:   string;
  createdAtUtc:   string;
  completedAtUtc?: string;
  errorMessage?:  string;
}

// ── SynqAudit — Integrity ─────────────────────────────────────────────────────

export interface IntegrityCheckpoint {
  checkpointId:     string;
  checkpointType:   string;
  aggregateHash:    string;
  recordCount:      number;
  isValid?:         boolean;
  fromRecordedAtUtc: string;
  toRecordedAtUtc:  string;
  createdAtUtc:     string;
}

// ── SynqAudit — Legal Holds ───────────────────────────────────────────────────

export interface LegalHold {
  holdId:           string;
  auditId:          string;
  legalAuthority:   string;
  notes?:           string;
  heldByUserId?:    string;
  heldAtUtc:        string;
  isActive:         boolean;
  releasedAtUtc?:   string;
  releasedByUserId?: string;
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
  category?:        string;
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

/**
 * UserImpersonationSession — records a live user-level impersonation started
 * by a platform admin acting as a specific tenant user.
 *
 * adminId               — userId of the PlatformAdmin performing impersonation.
 * impersonatedUserId    — id of the user being impersonated.
 * impersonatedUserEmail — email address for banner display.
 * tenantId              — tenant the impersonated user belongs to.
 * tenantName            — tenant display name for banner (not in minimal spec
 *                         but always available at impersonation start time).
 * startedAtUtc          — ISO 8601 timestamp in UTC.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 * TODO: audit log impersonation events
 */
export interface UserImpersonationSession {
  adminId:               string;
  impersonatedUserId:    string;
  impersonatedUserEmail: string;
  tenantId:              string;
  tenantName:            string;
  startedAtUtc:          string;
}

// ── Organization Types (Phase E) ──────────────────────────────────────────────

/**
 * A catalog entry for an organization type in the Identity service.
 * Returned by GET /identity/api/admin/organization-types.
 */
export interface OrganizationTypeItem {
  id:          string;
  code:        string;
  name:        string;
  description: string;
  isActive:    boolean;
  createdAtUtc: string;
}

// ── Relationship Types (Phase E) ──────────────────────────────────────────────

/**
 * A catalog entry for a relationship type (e.g. Referral, Partnership).
 * Returned by GET /identity/api/admin/relationship-types.
 */
export interface RelationshipTypeItem {
  id:          string;
  code:        string;
  name:        string;
  description: string;
  isActive:    boolean;
  createdAtUtc: string;
}

// ── Organization Relationships (Phase E) ──────────────────────────────────────

/** Live status of an organization-to-organization relationship. */
export type OrgRelationshipStatus = 'Active' | 'Inactive' | 'Pending';

/**
 * A directed relationship between two organizations in the Identity graph.
 * Returned by GET /identity/api/admin/organization-relationships.
 */
export interface OrgRelationship {
  id:                   string;
  sourceOrganizationId: string;
  targetOrganizationId: string;
  relationshipTypeId:   string;
  relationshipTypeCode: string;
  status:               OrgRelationshipStatus;
  effectiveFromUtc?:    string;
  effectiveToUtc?:      string;
  createdAtUtc:         string;
  updatedAtUtc:         string;
}

// ── Product–OrgType Rules (Phase E) ──────────────────────────────────────────

/**
 * A rule that permits a given OrganizationType to access a Product.
 * Returned by GET /identity/api/admin/product-org-type-rules.
 */
export interface ProductOrgTypeRule {
  id:                  string;
  productId:           string;
  productCode:         string;
  /** Role code within the product (e.g. CARECONNECT_REFERRER). */
  productRoleId:       string;
  productRoleCode:     string;
  productRoleName:     string;
  organizationTypeId:  string;
  organizationTypeCode: string;
  organizationTypeName: string;
  isActive:            boolean;
  createdAtUtc:        string;
}

// ── Product–RelType Rules (Phase E) ──────────────────────────────────────────

/**
 * A rule that permits a given RelationshipType to be used for a Product.
 * Returned by GET /identity/api/admin/product-rel-type-rules.
 */
export interface ProductRelTypeRule {
  id:                  string;
  productId:           string;
  productCode:         string;
  relationshipTypeId:  string;
  relationshipTypeCode: string;
  relationshipTypeName: string;
  isActive:            boolean;
  createdAtUtc:        string;
}

// ── Legacy coverage (Step 4) ──────────────────────────────────────────────────

/** One uncovered ProductRole — has EligibleOrgType but no active OrgTypeRule. */
export interface UncoveredRole {
  code:            string;
  eligibleOrgType: string;
}

/** Breakdown of eligibility-rule migration paths. */
export interface EligibilityRulesCoverage {
  totalActiveProductRoles: number;
  withDbRuleOnly:          number;   // modern path: OrgTypeRule only (Phase F goal)
  withBothPaths:           number;   // Phase F: always 0 — EligibleOrgType column dropped
  legacyStringOnly:        number;   // Phase F: always 0 — EligibleOrgType column dropped
  unrestricted:            number;   // no restriction at all (intentional)
  dbCoveragePct:           number;
  uncoveredRoles:          UncoveredRole[];
}

/**
 * Phase G shape — UserRoles / UserRoleAssignments tables are retired.
 * Legacy dual-write fields (usersWithLegacyRoles, usersWithGapCount,
 * dualWriteCoveragePct) have been removed; SRA is the sole role source.
 */
export interface RoleAssignmentsCoverage {
  /** Phase G: UserRoles and UserRoleAssignments tables have been dropped. */
  userRolesRetired:             boolean;
  usersWithScopedRoles:         number;
  totalActiveScopedAssignments: number;
}

/**
 * Point-in-time legacy migration snapshot.
 * Returned by GET /identity/api/admin/legacy-coverage.
 */
export interface LegacyCoverageReport {
  generatedAtUtc:  string;
  eligibilityRules: EligibilityRulesCoverage;
  roleAssignments:  RoleAssignmentsCoverage;
}

// ── Platform Readiness (Phase 8) ──────────────────────────────────────────────

export interface PhaseGCompletion {
  userRolesRetired:             boolean;
  soleRoleSourceIsSra:          boolean;
  totalActiveScopedAssignments: number;
  globalScopedAssignments:      number;
  usersWithScopedRole:          number;
}

export interface OrgTypeCoverage {
  totalActiveOrgs:            number;
  orgsWithOrganizationTypeId: number;
  orgsWithMissingTypeId:      number;
  orgsWithCodeMismatch:       number;
  consistent:                 boolean;
  coveragePct:                number;
}

export interface ProductRoleEligibilityCoverage {
  totalActiveProductRoles: number;
  withOrgTypeRule:         number;
  unrestricted:            number;
  coveragePct:             number;
}

export interface OrgRelationshipCoverage {
  total:  number;
  active: number;
}

/**
 * Phase I: active ScopedRoleAssignments broken down by scope type.
 * Non-zero organization/product/relationship values confirm real non-global
 * scope enforcement is in use at runtime.
 */
export interface ScopedAssignmentsByScope {
  global:       number;
  organization: number;
  product:      number;
  relationship: number;
  tenant:       number;
}

/**
 * Full platform readiness summary.
 * Returned by GET /identity/api/admin/platform-readiness.
 */
export interface PlatformReadinessSummary {
  generatedAtUtc:          string;
  phaseGCompletion:        PhaseGCompletion;
  orgTypeCoverage:         OrgTypeCoverage;
  productRoleEligibility:  ProductRoleEligibilityCoverage;
  orgRelationships:        OrgRelationshipCoverage;
  /** Phase I: SRA counts by scope type. */
  scopedAssignmentsByScope: ScopedAssignmentsByScope;
}

// ── CareConnect Integrity Report ──────────────────────────────────────────────

/**
 * Integrity counters for the CareConnect service.
 * Returned by GET /careconnect/api/admin/integrity.
 *
 * Counts of -1 indicate a query failure for that counter (the backend never
 * throws — it sets -1 so the dashboard always renders).
 */
export interface CareConnectIntegrityReport {
  generatedAtUtc: string;
  /** True when all four counters are 0. */
  clean: boolean;

  referrals: {
    /** Referrals where both org IDs are set but OrganizationRelationshipId is null. */
    withOrgPairButNullRelationship: number;
  };

  appointments: {
    /** Appointments missing a relationship ID when the linked referral has one. */
    missingRelationshipWhereReferralHasOne: number;
  };

  providers: {
    /** Active providers without an Identity OrganizationId link. */
    withoutOrganizationId: number;
  };

  facilities: {
    /** Active facilities without an Identity OrganizationId link. */
    withoutOrganizationId: number;
  };
}

// ── Scoped Role Assignment (Phase G) ──────────────────────────────────────────

/**
 * A scoped role assignment for a user.
 * Returned by GET /identity/api/admin/users/{id}/scoped-roles.
 */
export interface ScopedRoleAssignment {
  id:             string;
  userId:         string;
  roleId:         string;
  roleName:       string;
  scopeType:      string;
  scopeEntityId?: string;
  isActive:       boolean;
  createdAtUtc:   string;
}

// ── Shared ────────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  items:      T[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}
