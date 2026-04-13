import { serverApi, ServerApiError } from '@/lib/server-api-client';
import type {
  TenantUser,
  TenantUserDetail,
  TenantGroup,
  AccessDebugResponse,
  AssignableRolesResponse,
  GroupMember,
  GroupProductAccess,
  GroupRoleAssignment,
  PermissionItem,
  AdminUsersResponse,
} from '@/types/tenant';

export { ServerApiError };

export const tenantServerApi = {
  getUsers: () =>
    serverApi.get<TenantUser[]>('/identity/api/users'),

  getUserDetail: (userId: string) =>
    serverApi.get<TenantUserDetail>(`/identity/api/admin/users/${userId}`),

  getAccessDebug: (userId: string) =>
    serverApi.get<AccessDebugResponse>(`/identity/api/admin/users/${userId}/access-debug`),

  getAssignableRoles: (userId: string) =>
    serverApi.get<AssignableRolesResponse>(`/identity/api/admin/users/${userId}/assignable-roles`),

  getRoles: () =>
    serverApi.get<{ id: string; name: string }[]>('/identity/api/admin/roles'),

  getGroups: (tenantId: string) =>
    serverApi.get<TenantGroup[]>(`/identity/api/tenants/${tenantId}/groups`),

  getGroup: (tenantId: string, groupId: string) =>
    serverApi.get<TenantGroup>(`/identity/api/tenants/${tenantId}/groups/${groupId}`),

  getGroupMembers: (tenantId: string, groupId: string) =>
    serverApi.get<GroupMember[]>(`/identity/api/tenants/${tenantId}/groups/${groupId}/members`),

  getGroupProducts: (tenantId: string, groupId: string) =>
    serverApi.get<GroupProductAccess[]>(`/identity/api/tenants/${tenantId}/groups/${groupId}/products`),

  getGroupRoles: (tenantId: string, groupId: string) =>
    serverApi.get<GroupRoleAssignment[]>(`/identity/api/tenants/${tenantId}/groups/${groupId}/roles`),

  getProducts: () =>
    serverApi.get<{ code: string; name: string; isActive: boolean }[]>('/identity/api/admin/products'),

  getAdminUsers: (page = 1, pageSize = 200) =>
    serverApi.get<AdminUsersResponse>(`/identity/api/admin/users?page=${page}&pageSize=${pageSize}`),

  getPermissions: () =>
    serverApi.get<{ items: PermissionItem[]; totalCount: number }>('/identity/api/admin/permissions'),

  getRolePermissions: (roleId: string) =>
    serverApi.get<{ roleId: string; roleName: string; permissions: { id: string; code: string; name: string; productCode: string }[] }>(
      `/identity/api/admin/roles/${roleId}/permissions`
    ),
};
