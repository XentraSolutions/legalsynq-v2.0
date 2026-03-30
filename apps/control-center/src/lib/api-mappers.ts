/**
 * api-mappers.ts — Backend → Frontend response normalization layer.
 *
 * The Identity/Platform services may return either camelCase or snake_case
 * field names depending on the endpoint and serialiser configuration.
 * These mappers normalise every raw API response to the strict TypeScript
 * types defined in types/control-center.ts.
 *
 * Conventions:
 *   - Every mapper accepts `unknown` input and returns a typed value.
 *   - Fields are read with snake_case priority, camelCase fallback.
 *   - Missing / null / wrong-type fields are replaced with safe defaults.
 *   - In development, console.warn fires once per malformed field so
 *     backend issues surface during integration without crashing the UI.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */

import type {
  TenantSummary,
  TenantDetail,
  TenantStatus,
  TenantType,
  ProductEntitlementSummary,
  ProductCode,
  EntitlementStatus,
  UserSummary,
  UserDetail,
  UserStatus,
  Permission,
  RoleSummary,
  RoleDetail,
  AuditLogEntry,
  ActorType,
  PlatformSetting,
  MonitoringSummary,
  MonitoringStatus,
  AlertSeverity,
  SystemHealthSummary,
  IntegrationStatus,
  SystemAlert,
  SupportCase,
  SupportNote,
  SupportCaseDetail,
  SupportCaseStatus,
  SupportCasePriority,
  PagedResponse,
  OrganizationTypeItem,
  RelationshipTypeItem,
  OrgRelationship,
  OrgRelationshipStatus,
  ProductOrgTypeRule,
  ProductRelTypeRule,
  LegacyCoverageReport,
  PlatformReadinessSummary,
  CareConnectIntegrityReport,
  ScopedRoleAssignment,
} from '@/types/control-center';

// ── Low-level helpers ─────────────────────────────────────────────────────────

/**
 * asObj — safely casts `unknown` to a plain-object record.
 * Returns {} on null / non-object input.
 */
function asObj(v: unknown): Record<string, unknown> {
  if (v !== null && typeof v === 'object' && !Array.isArray(v)) {
    return v as Record<string, unknown>;
  }
  return {};
}

/**
 * asArr — safely casts `unknown` to an array.
 * Returns [] on null / non-array input.
 */
function asArr(v: unknown): unknown[] {
  return Array.isArray(v) ? v : [];
}

/**
 * str — reads a string field; snake_case first, then camelCase.
 * Falls back to `fallback` and optionally logs a warning in dev.
 */
function str(
  raw:       Record<string, unknown>,
  snake:     string,
  camel:     string,
  fallback:  string,
  warnLabel?: string,
): string {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && val.length > 0) return val;
  if (warnLabel && process.env.NODE_ENV !== 'production') {
    const got = JSON.stringify(val ?? null);
    console.warn(`[api-mappers] ${warnLabel}: expected string at "${snake}"/"${camel}", got ${got}. Using fallback "${fallback}".`);
  }
  return fallback;
}

/**
 * optStr — reads an optional string field; returns undefined when absent.
 */
function optStr(
  raw:   Record<string, unknown>,
  snake: string,
  camel: string,
): string | undefined {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && val.length > 0) return val;
  return undefined;
}

/**
 * num — reads a number field; snake_case first, then camelCase.
 * Falls back to `fallback`.
 */
function num(
  raw:      Record<string, unknown>,
  snake:    string,
  camel:    string,
  fallback: number,
  warnLabel?: string,
): number {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'number' && isFinite(val)) return val;
  if (warnLabel && process.env.NODE_ENV !== 'production') {
    console.warn(`[api-mappers] ${warnLabel}: expected number at "${snake}"/"${camel}", got ${JSON.stringify(val ?? null)}. Using ${fallback}.`);
  }
  return fallback;
}

/**
 * bool — reads a boolean field; snake_case first, then camelCase.
 * Coerces 0/1/"true"/"false" loosely. Falls back to `fallback`.
 */
function bool(
  raw:      Record<string, unknown>,
  snake:    string,
  camel:    string,
  fallback: boolean,
): boolean {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'boolean') return val;
  if (val === 1 || val === '1' || val === 'true')  return true;
  if (val === 0 || val === '0' || val === 'false') return false;
  return fallback;
}

/**
 * oneOf — reads a field and validates it against an allowed set.
 * Falls back to `fallback` if the value is absent or not in the set.
 */
function oneOf<T extends string>(
  raw:      Record<string, unknown>,
  snake:    string,
  camel:    string,
  allowed:  readonly T[],
  fallback: T,
  warnLabel?: string,
): T {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && (allowed as readonly string[]).includes(val)) {
    return val as T;
  }
  if (warnLabel && val !== undefined && process.env.NODE_ENV !== 'production') {
    console.warn(`[api-mappers] ${warnLabel}: unexpected value "${String(val)}" at "${snake}"/"${camel}". Expected one of [${allowed.join(', ')}]. Using "${fallback}".`);
  }
  return fallback;
}

// ── Tenant mappers ────────────────────────────────────────────────────────────

const TENANT_TYPES:   readonly TenantType[]   = ['LawFirm', 'Provider', 'Corporate', 'Government', 'Other'];
const TENANT_STATUSES: readonly TenantStatus[] = ['Active', 'Inactive', 'Suspended'];

/**
 * mapTenantSummary — normalises a raw backend tenant list item.
 *
 * Handles:
 *   display_name / displayName → displayName
 *   primary_contact_name / primaryContactName → primaryContactName
 *   is_active / isActive → isActive
 *   user_count / userCount → userCount
 *   org_count / orgCount → orgCount
 *   created_at / createdAt / createdAtUtc → createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapTenantSummary(raw: unknown): TenantSummary {
  const r = asObj(raw);
  const id = str(r, 'id', 'id', '', 'mapTenantSummary.id');
  return {
    id,
    code:               str(r, 'code',                 'code',               '',        'mapTenantSummary.code'),
    displayName:        str(r, 'display_name',          'displayName',         '',        'mapTenantSummary.displayName'),
    type:               oneOf(r, 'type',                'type',               TENANT_TYPES,   'Other', 'mapTenantSummary.type'),
    status:             oneOf(r, 'status',              'status',             TENANT_STATUSES, 'Inactive', 'mapTenantSummary.status'),
    primaryContactName: str(r, 'primary_contact_name',  'primaryContactName', '',        'mapTenantSummary.primaryContactName'),
    isActive:           bool(r, 'is_active',            'isActive',           false),
    userCount:          num(r,  'user_count',            'userCount',          0),
    orgCount:           num(r,  'org_count',             'orgCount',           0),
    createdAtUtc:       str(r, 'created_at',            'createdAtUtc',       new Date().toISOString()),
  };
}

/**
 * mapEntitlement — normalises a single product entitlement item.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
function mapEntitlement(raw: unknown): ProductEntitlementSummary {
  const r = asObj(raw);
  const PRODUCT_CODES: readonly ProductCode[] = [
    'SynqFund', 'SynqLien', 'SynqBill', 'SynqRx', 'SynqPayout', 'CareConnect',
  ];
  const ENTITLEMENT_STATUSES: readonly EntitlementStatus[] = ['Active', 'Disabled'];
  const enabled = bool(r, 'enabled', 'enabled', false);
  return {
    productCode:  oneOf(r, 'product_code',  'productCode',  PRODUCT_CODES,        'SynqFund', 'mapEntitlement.productCode'),
    productName:  str(r,  'product_name',   'productName',  '',                   'mapEntitlement.productName'),
    enabled,
    status:       oneOf(r, 'status',        'status',       ENTITLEMENT_STATUSES, enabled ? 'Active' : 'Disabled'),
    enabledAtUtc: optStr(r, 'enabled_at',   'enabledAtUtc'),
  };
}

/**
 * mapTenantDetail — normalises a raw backend tenant detail response.
 * Extends mapTenantSummary with the extra fields on TenantDetail.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapTenantDetail(raw: unknown): TenantDetail {
  const r    = asObj(raw);
  const base = mapTenantSummary(raw);
  return {
    ...base,
    email:           optStr(r, 'email', 'email'),
    updatedAtUtc:    str(r, 'updated_at',        'updatedAtUtc',    new Date().toISOString()),
    activeUserCount: num(r, 'active_user_count',  'activeUserCount', 0),
    linkedOrgCount:  r['linked_org_count']  !== undefined
                       ? num(r, 'linked_org_count',  'linkedOrgCount',  0)
                       : r['linkedOrgCount'] !== undefined
                         ? num(r, 'linked_org_count', 'linkedOrgCount', 0)
                         : undefined,
    productEntitlements: asArr(
      r['product_entitlements'] ?? r['productEntitlements'],
    ).map(mapEntitlement),
  };
}

/**
 * mapEntitlementResponse — normalises a single entitlement response
 * (from the toggle endpoint).
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapEntitlementResponse(raw: unknown): ProductEntitlementSummary {
  return mapEntitlement(raw);
}

// ── User mappers ──────────────────────────────────────────────────────────────

const USER_STATUSES: readonly UserStatus[] = ['Active', 'Inactive', 'Invited'];

/**
 * mapUserSummary — normalises a raw backend user list item.
 *
 * Handles:
 *   first_name / firstName → firstName
 *   last_name / lastName → lastName
 *   tenant_id / tenantId → tenantId
 *   tenant_code / tenantCode → tenantCode
 *   last_login_at / lastLoginAt / lastLoginAtUtc → lastLoginAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapUserSummary(raw: unknown): UserSummary {
  const r = asObj(raw);
  return {
    id:              str(r, 'id',            'id',           '',       'mapUserSummary.id'),
    firstName:       str(r, 'first_name',    'firstName',    '',       'mapUserSummary.firstName'),
    lastName:        str(r, 'last_name',     'lastName',     '',       'mapUserSummary.lastName'),
    email:           str(r, 'email',         'email',        '',       'mapUserSummary.email'),
    role:            str(r, 'role',          'role',         'User'),
    status:          oneOf(r, 'status',      'status',       USER_STATUSES, 'Inactive', 'mapUserSummary.status'),
    tenantId:        str(r, 'tenant_id',     'tenantId',     '',       'mapUserSummary.tenantId'),
    tenantCode:      str(r, 'tenant_code',   'tenantCode',   '',       'mapUserSummary.tenantCode'),
    lastLoginAtUtc:  optStr(r, 'last_login_at', 'lastLoginAtUtc')
                       ?? optStr(r, 'last_login_at_utc', 'lastLoginAtUtc'),
  };
}

/**
 * mapUserDetail — normalises a raw backend user detail response.
 *
 * Handles:
 *   tenant_display_name / tenantDisplayName → tenantDisplayName
 *   created_at / createdAtUtc → createdAtUtc
 *   updated_at / updatedAtUtc → updatedAtUtc
 *   is_locked / isLocked → isLocked
 *   invite_sent_at / inviteSentAtUtc → inviteSentAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapUserDetail(raw: unknown): UserDetail {
  const r    = asObj(raw);
  const base = mapUserSummary(raw);
  return {
    ...base,
    tenantDisplayName: str(r, 'tenant_display_name', 'tenantDisplayName', base.tenantCode || base.tenantId),
    createdAtUtc:      str(r, 'created_at',           'createdAtUtc',      new Date().toISOString()),
    updatedAtUtc:      str(r, 'updated_at',           'updatedAtUtc',      new Date().toISOString()),
    isLocked:          bool(r, 'is_locked',           'isLocked',          false),
    inviteSentAtUtc:   optStr(r, 'invite_sent_at',    'inviteSentAtUtc'),
  };
}

// ── Role mappers ──────────────────────────────────────────────────────────────

/**
 * mapPermission — normalises a single permission object.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
function mapPermission(raw: unknown): Permission {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',          'id',          ''),
    key:         str(r, 'key',         'key',         ''),
    description: str(r, 'description', 'description', ''),
  };
}

/**
 * mapRoleSummary — normalises a raw backend role list item.
 *
 * Handles:
 *   user_count / userCount → userCount
 *   permissions (array of strings or Permission objects)
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapRoleSummary(raw: unknown): RoleSummary {
  const r = asObj(raw);

  // permissions may be string[] (keys) or Permission[] (objects)
  const rawPerms = asArr(r['permissions']);
  const permissions: string[] = rawPerms.map(p => {
    if (typeof p === 'string') return p;
    const po = asObj(p);
    return str(po, 'key', 'key', '');
  }).filter(Boolean);

  return {
    id:          str(r, 'id',          'id',          '',    'mapRoleSummary.id'),
    name:        str(r, 'name',        'name',        '',    'mapRoleSummary.name'),
    description: str(r, 'description', 'description', ''),
    userCount:   num(r, 'user_count',  'userCount',   0),
    permissions,
  };
}

/**
 * mapRoleDetail — normalises a raw backend role detail response.
 *
 * Handles:
 *   created_at / createdAtUtc → createdAtUtc
 *   updated_at / updatedAtUtc → updatedAtUtc
 *   resolved_permissions / resolvedPermissions → resolvedPermissions
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapRoleDetail(raw: unknown): RoleDetail {
  const r    = asObj(raw);
  const base = mapRoleSummary(raw);
  return {
    ...base,
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
    updatedAtUtc: str(r, 'updated_at',  'updatedAtUtc', new Date().toISOString()),
    resolvedPermissions: asArr(
      r['resolved_permissions'] ?? r['resolvedPermissions'],
    ).map(mapPermission),
  };
}

// ── Audit log mapper ──────────────────────────────────────────────────────────

const ACTOR_TYPES: readonly ActorType[] = ['Admin', 'System'];

/**
 * mapAuditLog — normalises a raw backend audit log entry.
 *
 * Handles:
 *   actor_name / actorName → actorName
 *   actor_type / actorType → actorType
 *   entity_type / entityType → entityType
 *   entity_id / entityId → entityId
 *   created_at / createdAt / createdAtUtc → createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapAuditLog(raw: unknown): AuditLogEntry {
  const r = asObj(raw);
  const rawMeta = r['metadata'] ?? r['meta'];
  const metadata: Record<string, unknown> | undefined =
    rawMeta !== null && typeof rawMeta === 'object' && !Array.isArray(rawMeta)
      ? (rawMeta as Record<string, unknown>)
      : undefined;
  return {
    id:          str(r, 'id',          'id',          '',      'mapAuditLog.id'),
    actorName:   str(r, 'actor_name',  'actorName',   '',      'mapAuditLog.actorName'),
    actorType:   oneOf(r, 'actor_type', 'actorType',  ACTOR_TYPES, 'Admin', 'mapAuditLog.actorType'),
    action:      str(r, 'action',      'action',      ''),
    entityType:  str(r, 'entity_type', 'entityType',  ''),
    entityId:    str(r, 'entity_id',   'entityId',    ''),
    metadata,
    createdAtUtc: str(r, 'created_at', 'createdAtUtc', new Date().toISOString()),
  };
}

// ── Settings mapper ───────────────────────────────────────────────────────────

const SETTING_TYPES: readonly PlatformSetting['type'][] = ['boolean', 'string', 'number'];

/**
 * mapSetting — normalises a raw backend platform setting.
 *
 * Handles:
 *   type coercion for value (string → boolean/number as needed)
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSetting(raw: unknown): PlatformSetting {
  const r = asObj(raw);
  const key      = str(r, 'key',   'key',   '',      'mapSetting.key');
  const rawType  = oneOf(r, 'type', 'type',  SETTING_TYPES, 'string', 'mapSetting.type');
  const rawValue = r['value'];

  // Coerce value to the declared type
  let value: string | number | boolean;
  if (rawType === 'boolean') {
    value = bool(r, 'value', 'value', false);
  } else if (rawType === 'number') {
    value = num(r, 'value', 'value', 0);
  } else {
    value = typeof rawValue === 'string' ? rawValue : String(rawValue ?? '');
  }

  return {
    key,
    label:       str(r, 'label',       'label',       key),
    value,
    type:        rawType,
    description: optStr(r, 'description', 'description'),
    editable:    bool(r, 'editable',    'editable',    false),
  };
}

// ── Monitoring mapper ─────────────────────────────────────────────────────────

const MONITORING_STATUSES: readonly MonitoringStatus[] = ['Healthy', 'Degraded', 'Down'];
const ALERT_SEVERITIES:    readonly AlertSeverity[]    = ['Info', 'Warning', 'Critical'];

/**
 * mapSystemHealth — normalises a raw system health object.
 */
function mapSystemHealth(raw: unknown): SystemHealthSummary {
  const r = asObj(raw);
  return {
    status:           oneOf(r, 'status',           'status',           MONITORING_STATUSES, 'Healthy'),
    lastCheckedAtUtc: str(r, 'last_checked_at',    'lastCheckedAtUtc', new Date().toISOString()),
  };
}

/**
 * mapIntegration — normalises a single integration status row.
 */
function mapIntegration(raw: unknown): IntegrationStatus {
  const r         = asObj(raw);
  const latencyRaw = r['latency_ms'] ?? r['latencyMs'];
  return {
    name:             str(r, 'name',            'name',            ''),
    status:           oneOf(r, 'status',        'status',          MONITORING_STATUSES, 'Healthy'),
    latencyMs:        typeof latencyRaw === 'number' && isFinite(latencyRaw) ? latencyRaw : undefined,
    lastCheckedAtUtc: str(r, 'last_checked_at', 'lastCheckedAtUtc', new Date().toISOString()),
  };
}

/**
 * mapAlert — normalises a single system alert.
 */
function mapAlert(raw: unknown): SystemAlert {
  const r = asObj(raw);
  return {
    id:           str(r, 'id',          'id',           ''),
    message:      str(r, 'message',     'message',      ''),
    severity:     oneOf(r, 'severity',  'severity',     ALERT_SEVERITIES, 'Info'),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
  };
}

/**
 * mapMonitoring — normalises a raw backend monitoring summary response.
 *
 * Handles:
 *   system.last_checked_at / lastCheckedAtUtc
 *   integrations[].latency_ms / latencyMs
 *   alerts[].created_at / createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapMonitoring(raw: unknown): MonitoringSummary {
  const r = asObj(raw);
  return {
    system:       mapSystemHealth(r['system'] ?? {}),
    integrations: asArr(r['integrations']).map(mapIntegration),
    alerts:       asArr(r['alerts']).map(mapAlert),
  };
}

// ── Support mappers ───────────────────────────────────────────────────────────

const SUPPORT_STATUSES:   readonly SupportCaseStatus[]   = ['Open', 'Investigating', 'Resolved', 'Closed'];
const SUPPORT_PRIORITIES: readonly SupportCasePriority[] = ['Low', 'Medium', 'High'];

/**
 * mapSupportCase — normalises a raw backend support case (list item).
 *
 * Handles:
 *   tenant_id / tenantId → tenantId
 *   tenant_name / tenantName → tenantName
 *   user_id / userId → userId
 *   user_name / userName → userName
 *   created_at / createdAtUtc → createdAtUtc
 *   updated_at / updatedAtUtc → updatedAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportCase(raw: unknown): SupportCase {
  const r = asObj(raw);
  return {
    id:           str(r, 'id',          'id',          '',      'mapSupportCase.id'),
    title:        str(r, 'title',       'title',       ''),
    tenantId:     str(r, 'tenant_id',   'tenantId',    ''),
    tenantName:   str(r, 'tenant_name', 'tenantName',  ''),
    userId:       optStr(r, 'user_id',  'userId'),
    userName:     optStr(r, 'user_name','userName'),
    status:       oneOf(r, 'status',    'status',      SUPPORT_STATUSES,   'Open',   'mapSupportCase.status'),
    category:     str(r, 'category',    'category',    ''),
    priority:     oneOf(r, 'priority',  'priority',    SUPPORT_PRIORITIES, 'Medium', 'mapSupportCase.priority'),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
    updatedAtUtc: str(r, 'updated_at',  'updatedAtUtc', new Date().toISOString()),
  };
}

/**
 * mapSupportNote — normalises a single support case note.
 *
 * Handles:
 *   case_id / caseId → caseId
 *   created_by / createdBy → createdBy
 *   created_at / createdAtUtc → createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportNote(raw: unknown): SupportNote {
  const r = asObj(raw);
  return {
    id:           str(r, 'id',         'id',          ''),
    caseId:       str(r, 'case_id',    'caseId',      ''),
    message:      str(r, 'message',    'message',     ''),
    createdBy:    str(r, 'created_by', 'createdBy',   ''),
    createdAtUtc: str(r, 'created_at', 'createdAtUtc', new Date().toISOString()),
  };
}

/**
 * mapSupportCaseDetail — normalises a full support case detail response.
 * Extends mapSupportCase with the notes array.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportCaseDetail(raw: unknown): SupportCaseDetail {
  const r    = asObj(raw);
  const base = mapSupportCase(raw);
  return {
    ...base,
    notes: asArr(r['notes']).map(mapSupportNote),
  };
}

// ── Organization Type mapper (Phase E) ───────────────────────────────────────

/**
 * mapOrganizationTypeItem — normalises a raw backend OrganizationType catalog entry.
 * Handles: is_active/isActive, created_at/createdAtUtc
 */
export function mapOrganizationTypeItem(raw: unknown): OrganizationTypeItem {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',           'id',           '', 'mapOrganizationTypeItem.id'),
    code:        str(r, 'code',         'code',         ''),
    name:        str(r, 'name',         'name',         ''),
    description: str(r, 'description',  'description',  ''),
    isActive:    bool(r, 'is_active',   'isActive',     true),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
  };
}

// ── Relationship Type mapper (Phase E) ────────────────────────────────────────

/**
 * mapRelationshipTypeItem — normalises a raw backend RelationshipType catalog entry.
 */
export function mapRelationshipTypeItem(raw: unknown): RelationshipTypeItem {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',           'id',           '', 'mapRelationshipTypeItem.id'),
    code:        str(r, 'code',         'code',         ''),
    name:        str(r, 'name',         'name',         ''),
    description: str(r, 'description',  'description',  ''),
    isActive:    bool(r, 'is_active',   'isActive',     true),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
  };
}

// ── Organization Relationship mapper (Phase E) ────────────────────────────────

const ORG_REL_STATUSES: readonly OrgRelationshipStatus[] = ['Active', 'Inactive', 'Pending'];

/**
 * mapOrgRelationship — normalises a raw backend OrganizationRelationship entry.
 * Handles snake_case/camelCase for all FK and timestamp fields.
 */
export function mapOrgRelationship(raw: unknown): OrgRelationship {
  const r = asObj(raw);
  return {
    id:                   str(r, 'id',                      'id',                   '', 'mapOrgRelationship.id'),
    sourceOrganizationId: str(r, 'source_organization_id',  'sourceOrganizationId', ''),
    targetOrganizationId: str(r, 'target_organization_id',  'targetOrganizationId', ''),
    relationshipTypeId:   str(r, 'relationship_type_id',    'relationshipTypeId',   ''),
    relationshipTypeCode: str(r, 'relationship_type_code',  'relationshipTypeCode', ''),
    status:               oneOf(r, 'status', 'status', ORG_REL_STATUSES, 'Inactive', 'mapOrgRelationship.status'),
    effectiveFromUtc:     optStr(r, 'effective_from', 'effectiveFromUtc'),
    effectiveToUtc:       optStr(r, 'effective_to',   'effectiveToUtc'),
    createdAtUtc:         str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
    updatedAtUtc:         str(r, 'updated_at',  'updatedAtUtc', new Date().toISOString()),
  };
}

// ── Product–OrgType Rule mapper (Phase E) ────────────────────────────────────

/**
 * mapProductOrgTypeRule — normalises a raw backend ProductOrganizationTypeRule entry.
 */
export function mapProductOrgTypeRule(raw: unknown): ProductOrgTypeRule {
  const r = asObj(raw);
  return {
    id:                   str(r, 'id',                     'id',                   '', 'mapProductOrgTypeRule.id'),
    productId:            str(r, 'product_id',             'productId',            ''),
    productCode:          str(r, 'product_code',           'productCode',          ''),
    productRoleId:        str(r, 'product_role_id',        'productRoleId',        ''),
    productRoleCode:      str(r, 'product_role_code',      'productRoleCode',      ''),
    productRoleName:      str(r, 'product_role_name',      'productRoleName',      ''),
    organizationTypeId:   str(r, 'organization_type_id',   'organizationTypeId',   ''),
    organizationTypeCode: str(r, 'organization_type_code', 'organizationTypeCode', ''),
    organizationTypeName: str(r, 'organization_type_name', 'organizationTypeName', ''),
    isActive:             bool(r, 'is_active',             'isActive',             true),
    createdAtUtc:         str(r, 'created_at',             'createdAtUtc',         new Date().toISOString()),
  };
}

// ── Product–RelType Rule mapper (Phase E) ─────────────────────────────────────

/**
 * mapProductRelTypeRule — normalises a raw backend ProductRelationshipTypeRule entry.
 */
export function mapProductRelTypeRule(raw: unknown): ProductRelTypeRule {
  const r = asObj(raw);
  return {
    id:                   str(r, 'id',                     'id',                   '', 'mapProductRelTypeRule.id'),
    productId:            str(r, 'product_id',             'productId',            ''),
    productCode:          str(r, 'product_code',           'productCode',          ''),
    relationshipTypeId:   str(r, 'relationship_type_id',   'relationshipTypeId',   ''),
    relationshipTypeCode: str(r, 'relationship_type_code', 'relationshipTypeCode', ''),
    relationshipTypeName: str(r, 'relationship_type_name', 'relationshipTypeName', ''),
    isActive:             bool(r, 'is_active',             'isActive',             true),
    createdAtUtc:         str(r, 'created_at',             'createdAtUtc',         new Date().toISOString()),
  };
}

// ── Legacy Coverage mapper (Step 4) ───────────────────────────────────────────

/**
 * mapLegacyCoverageReport — normalises a raw backend legacy-coverage response.
 * Returned by GET /identity/api/admin/legacy-coverage.
 *
 * Phase G update: roleAssignments now uses the retired dual-write shape.
 * Legacy fields (usersWithLegacyRoles, usersWithGapCount, dualWriteCoveragePct)
 * are no longer emitted by the backend; Phase G fields are read instead.
 */
export function mapLegacyCoverageReport(raw: unknown): LegacyCoverageReport {
  const r = asObj(raw);

  const erRaw = asObj(r['eligibilityRules'] ?? r['eligibility_rules'] ?? {});
  const raRaw = asObj(r['roleAssignments']  ?? r['role_assignments']  ?? {});

  const uncoveredRaw = Array.isArray(erRaw['uncoveredRoles'] ?? erRaw['uncovered_roles'])
    ? (erRaw['uncoveredRoles'] ?? erRaw['uncovered_roles']) as unknown[]
    : [];

  return {
    generatedAtUtc: str(r, 'generated_at_utc', 'generatedAtUtc', new Date().toISOString()),

    eligibilityRules: {
      totalActiveProductRoles: num(erRaw, 'total_active_product_roles', 'totalActiveProductRoles', 0),
      withDbRuleOnly:          num(erRaw, 'with_db_rule_only',          'withDbRuleOnly',          0),
      withBothPaths:           num(erRaw, 'with_both_paths',            'withBothPaths',            0),
      legacyStringOnly:        num(erRaw, 'legacy_string_only',         'legacyStringOnly',         0),
      unrestricted:            num(erRaw, 'unrestricted',               'unrestricted',             0),
      dbCoveragePct:           num(erRaw, 'db_coverage_pct',            'dbCoveragePct',            100),
      uncoveredRoles: uncoveredRaw.map(u => {
        const ur = asObj(u);
        return {
          code:            str(ur, 'code',              'code',            ''),
          eligibleOrgType: str(ur, 'eligible_org_type', 'eligibleOrgType', ''),
        };
      }),
    },

    // Phase G shape — dual-write fields retired; SRA is sole role source.
    roleAssignments: {
      userRolesRetired:             bool(raRaw, 'user_roles_retired',               'userRolesRetired',             true),
      usersWithScopedRoles:         num(raRaw,  'users_with_scoped_roles',          'usersWithScopedRoles',         0),
      totalActiveScopedAssignments: num(raRaw,  'total_active_scoped_assignments',  'totalActiveScopedAssignments', 0),
    },
  };
}

// ── Platform Readiness mapper (Phase 8) ───────────────────────────────────────

/**
 * mapPlatformReadiness — normalises a raw platform-readiness response.
 * Returned by GET /identity/api/admin/platform-readiness.
 */
export function mapPlatformReadiness(raw: unknown): PlatformReadinessSummary {
  const r    = asObj(raw);
  const pgRaw = asObj(r['phaseGCompletion']       ?? r['phase_g_completion']       ?? {});
  const otRaw = asObj(r['orgTypeCoverage']         ?? r['org_type_coverage']        ?? {});
  const prRaw = asObj(r['productRoleEligibility']  ?? r['product_role_eligibility'] ?? {});
  const orRaw = asObj(r['orgRelationships']        ?? r['org_relationships']        ?? {});

  return {
    generatedAtUtc: str(r, 'generated_at_utc', 'generatedAtUtc', new Date().toISOString()),

    phaseGCompletion: {
      userRolesRetired:             bool(pgRaw, 'user_roles_retired',               'userRolesRetired',             true),
      soleRoleSourceIsSra:          bool(pgRaw, 'sole_role_source_is_sra',          'soleRoleSourceIsSra',          true),
      totalActiveScopedAssignments: num(pgRaw,  'total_active_scoped_assignments',  'totalActiveScopedAssignments', 0),
      globalScopedAssignments:      num(pgRaw,  'global_scoped_assignments',        'globalScopedAssignments',      0),
      usersWithScopedRole:          num(pgRaw,  'users_with_scoped_role',           'usersWithScopedRole',          0),
    },

    orgTypeCoverage: {
      totalActiveOrgs:            num(otRaw,  'total_active_orgs',             'totalActiveOrgs',            0),
      orgsWithOrganizationTypeId: num(otRaw,  'orgs_with_organization_type_id','orgsWithOrganizationTypeId', 0),
      orgsWithMissingTypeId:      num(otRaw,  'orgs_with_missing_type_id',     'orgsWithMissingTypeId',      0),
      orgsWithCodeMismatch:       num(otRaw,  'orgs_with_code_mismatch',       'orgsWithCodeMismatch',       0),
      consistent:                 bool(otRaw, 'consistent',                    'consistent',                 true),
      coveragePct:                num(otRaw,  'coverage_pct',                  'coveragePct',                100),
    },

    productRoleEligibility: {
      totalActiveProductRoles: num(prRaw, 'total_active_product_roles', 'totalActiveProductRoles', 0),
      withOrgTypeRule:         num(prRaw, 'with_org_type_rule',         'withOrgTypeRule',         0),
      unrestricted:            num(prRaw, 'unrestricted',               'unrestricted',            0),
      coveragePct:             num(prRaw, 'coverage_pct',               'coveragePct',             100),
    },

    orgRelationships: {
      total:  num(orRaw, 'total',  'total',  0),
      active: num(orRaw, 'active', 'active', 0),
    },

    // Phase I: scoped assignment counts by scope type
    scopedAssignmentsByScope: (() => {
      const sb = asObj(r['scopedAssignmentsByScope'] ?? r['scoped_assignments_by_scope'] ?? {});
      return {
        global:       num(sb, 'global',       'global',       0),
        organization: num(sb, 'organization',  'organization', 0),
        product:      num(sb, 'product',       'product',      0),
        relationship: num(sb, 'relationship',  'relationship', 0),
        tenant:       num(sb, 'tenant',        'tenant',       0),
      };
    })(),
  };
}

// ── CareConnect Integrity mapper ──────────────────────────────────────────────

/**
 * mapCareConnectIntegrity — normalises the raw GET /careconnect/api/admin/integrity response.
 *
 * The backend never throws — failing queries produce -1 for that counter.
 * The mapper preserves -1 values so the UI can distinguish "0 issues" from
 * "query failed" and render an appropriate warning.
 */
export function mapCareConnectIntegrity(raw: unknown): CareConnectIntegrityReport {
  const r    = asObj(raw);
  const refs = asObj(r['referrals']    ?? {});
  const apps = asObj(r['appointments'] ?? {});
  const prov = asObj(r['providers']    ?? {});
  const facs = asObj(r['facilities']   ?? {});

  return {
    generatedAtUtc: str(r, 'generated_at_utc', 'generatedAtUtc', new Date().toISOString()),
    clean:          bool(r, 'clean', 'clean', false),

    referrals: {
      withOrgPairButNullRelationship: num(refs, 'with_org_pair_but_null_relationship',
                                          'withOrgPairButNullRelationship', -1),
    },

    appointments: {
      missingRelationshipWhereReferralHasOne: num(apps,
        'missing_relationship_where_referral_has_one',
        'missingRelationshipWhereReferralHasOne', -1),
    },

    providers: {
      withoutOrganizationId: num(prov, 'without_organization_id', 'withoutOrganizationId', -1),
    },

    facilities: {
      withoutOrganizationId: num(facs, 'without_organization_id', 'withoutOrganizationId', -1),
    },
  };
}

// ── ScopedRoleAssignment mapper ───────────────────────────────────────────────

/**
 * mapScopedRoleAssignment — normalises a single ScopedRoleAssignment record.
 * Returned per-item by GET /identity/api/admin/users/{id}/scoped-roles.
 */
export function mapScopedRoleAssignment(raw: unknown): ScopedRoleAssignment {
  const r = asObj(raw);
  return {
    id:             str(r, 'id',              'id',             ''),
    userId:         str(r, 'user_id',         'userId',         ''),
    roleId:         str(r, 'role_id',         'roleId',         ''),
    roleName:       str(r, 'role_name',       'roleName',       ''),
    scopeType:      str(r, 'scope_type',      'scopeType',      'Global'),
    scopeEntityId:  r['scope_entity_id'] as string | undefined
                    ?? r['scopeEntityId'] as string | undefined,
    isActive:       bool(r, 'is_active',      'isActive',       true),
    createdAtUtc:   str(r, 'created_at_utc',  'createdAtUtc',   new Date().toISOString()),
  };
}

// ── PagedResponse mapper ──────────────────────────────────────────────────────

/**
 * mapPagedResponse<T> — normalises a paged list response, applying `mapItem`
 * to each element in the `items` array.
 *
 * Handles:
 *   total_count / totalCount → totalCount
 *   page_size / pageSize → pageSize
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapPagedResponse<T>(
  raw:     unknown,
  mapItem: (item: unknown) => T,
): PagedResponse<T> {
  const r = asObj(raw);
  return {
    items:      asArr(r['items']).map(mapItem),
    totalCount: num(r, 'total_count', 'totalCount', 0),
    page:       num(r, 'page',        'page',        1),
    pageSize:   num(r, 'page_size',   'pageSize',    20),
  };
}
