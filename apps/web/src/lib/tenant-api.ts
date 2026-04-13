import { serverApi, ServerApiError } from '@/lib/server-api-client';
import { apiClient, ApiError } from '@/lib/api-client';
import type {
  TenantUser,
  TenantUserDetail,
  TenantGroup,
  AccessDebugResponse,
  AssignableRolesResponse,
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

  getProducts: () =>
    serverApi.get<{ code: string; name: string; isActive: boolean }[]>('/identity/api/admin/products'),
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
};
