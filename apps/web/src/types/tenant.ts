export interface TenantUser {
  id: string;
  tenantId: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  roles: string[];
  organizationId?: string;
  orgType?: string;
  productRoles?: string[];
}

export interface TenantUserDetail {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  roles: { roleId: string; roleName: string; assignmentId: string }[];
  status: string;
  tenantId: string;
  tenantCode: string;
  tenantDisplayName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  isLocked: boolean;
  lockedAtUtc?: string;
  lastLoginAtUtc?: string;
  sessionVersion: number;
  avatarDocumentId?: string;
  memberships: {
    membershipId: string;
    organizationId: string;
    orgName: string;
    memberRole: string;
    isPrimary: boolean;
    joinedAtUtc: string;
  }[];
  groups: { groupId: string; groupName: string; joinedAtUtc: string }[];
  groupCount: number;
}

export interface TenantGroup {
  id: string;
  tenantId: string;
  name: string;
  description?: string;
  status: string;
  scopeType: string;
  productCode?: string;
  organizationId?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface GroupMember {
  id: string;
  tenantId: string;
  groupId: string;
  userId: string;
  membershipStatus: string;
  addedAtUtc: string;
  removedAtUtc?: string;
}

export interface GroupProductAccess {
  id: string;
  tenantId: string;
  groupId: string;
  productCode: string;
  accessStatus: string;
  grantedAtUtc: string;
  revokedAtUtc?: string;
}

export interface GroupRoleAssignment {
  id: string;
  tenantId: string;
  groupId: string;
  roleCode: string;
  productCode?: string;
  organizationId?: string;
  assignmentStatus: string;
  assignedAtUtc: string;
  removedAtUtc?: string;
}

export interface AccessDebugProductSource {
  productCode: string;
  source: string;
  groupId?: string;
  groupName?: string;
}

export interface AccessDebugRoleSource {
  roleCode: string;
  productCode?: string;
  source: string;
  groupId?: string;
  groupName?: string;
}

export interface AccessDebugPermissionSource {
  permissionCode: string;
  productCode?: string;
  source: string;
  viaRoleCode: string;
  groupId?: string;
  groupName?: string;
}

export interface AccessDebugGroup {
  groupId: string;
  groupName: string;
  status: string;
  scopeType: string;
  productCode?: string;
}

export interface AccessDebugResponse {
  userId: string;
  tenantId: string;
  accessVersion?: number;
  products: AccessDebugProductSource[];
  roles: AccessDebugRoleSource[];
  systemRoles: { roleName: string; scopeType: string }[];
  groups: AccessDebugGroup[];
  entitlements: { productCode: string; status: string }[];
  productRolesFlat: string[];
  tenantRoles: string[];
  permissions: string[];
  permissionSources: AccessDebugPermissionSource[];
  policies?: {
    permission: string;
    linkedPolicies: {
      policyCode: string;
      policyName: string;
      priority: number;
      rulesCount: number;
      rules: { field: string; op: string; value: string; conditionType: string; logicalGroup: string }[];
    }[];
  }[];
}

export interface AssignableRoleItem {
  id: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  isProductRole: boolean;
  productCode?: string;
  productName?: string;
  allowedOrgTypes?: string[];
  assignable: boolean;
  disabledReason?: string;
  isAssigned: boolean;
}

export interface AssignableRolesResponse {
  items: AssignableRoleItem[];
  userOrgType: string;
  tenantEnabledProducts: number;
}
