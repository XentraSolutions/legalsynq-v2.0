import { serverApi, ServerApiError } from '@/lib/server-api-client';
import { apiClient, ApiError } from '@/lib/api-client';
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
  SimulationRequest,
  SimulationResult,
} from '@/types/tenant';

export { ServerApiError, ApiError };

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

export const tenantClientApi = {
  assignProduct: (tenantId: string, userId: string, productCode: string) =>
    apiClient.put<void>(`/identity/api/tenants/${tenantId}/users/${userId}/products/${productCode}`, {}),

  removeProduct: (tenantId: string, userId: string, productCode: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/users/${userId}/products/${productCode}`),

  assignRole: (userId: string, roleId: string) =>
    apiClient.post<void>(`/identity/api/admin/users/${userId}/roles`, { roleId }),

  removeRole: (userId: string, roleId: string) =>
    apiClient.delete<void>(`/identity/api/admin/users/${userId}/roles/${roleId}`),

  addToGroup: (tenantId: string, groupId: string, userId: string) =>
    apiClient.post<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/members`, { userId }),

  removeFromGroup: (tenantId: string, groupId: string, userId: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/members/${userId}`),

  createGroup: (tenantId: string, body: { name: string; description?: string }) =>
    apiClient.post<TenantGroup>(`/identity/api/tenants/${tenantId}/groups`, body),

  updateGroup: (tenantId: string, groupId: string, body: { name: string; description?: string }) =>
    apiClient.patch<TenantGroup>(`/identity/api/tenants/${tenantId}/groups/${groupId}`, body),

  archiveGroup: (tenantId: string, groupId: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}`),

  grantGroupProduct: (tenantId: string, groupId: string, productCode: string) =>
    apiClient.put<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/products/${productCode}`, {}),

  revokeGroupProduct: (tenantId: string, groupId: string, productCode: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/products/${productCode}`),

  assignGroupRole: (tenantId: string, groupId: string, roleCode: string, productCode?: string) =>
    apiClient.post<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/roles`, { roleCode, productCode }),

  removeGroupRole: (tenantId: string, groupId: string, assignmentId: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/roles/${assignmentId}`),

  getUserAccessDebug: (userId: string) =>
    apiClient.get<AccessDebugResponse>(`/identity/api/admin/users/${userId}/access-debug`),

  simulateAuthorization: (body: SimulationRequest) =>
    apiClient.post<SimulationResult>('/identity/api/admin/authorization/simulate', body),
};
