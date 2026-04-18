import { apiClient, ApiError } from '@/lib/api-client';
import type {
  TenantUser,
  TenantUserDetail,
  TenantGroup,
  AccessDebugResponse,
  SimulationRequest,
  SimulationResult,
} from '@/types/tenant';

export { ApiError };

export interface CreateUserBody {
  tenantId:  string;
  email:     string;
  password:  string;
  firstName: string;
  lastName:  string;
  roleIds?:  string[];
}

export const tenantClientApi = {
  createUser: (body: CreateUserBody) =>
    apiClient.post<TenantUser>('/identity/api/users', body),

  getRoles: () =>
    apiClient.get<{ id: string; name: string }[]>('/identity/api/admin/roles'),

  getUserDetail: (userId: string) =>
    apiClient.get<TenantUserDetail>(`/identity/api/admin/users/${userId}`),

  activateUser: (userId: string) =>
    apiClient.post<void>(`/identity/api/admin/users/${userId}/activate`, {}),

  deactivateUser: (userId: string) =>
    apiClient.patch<void>(`/identity/api/admin/users/${userId}/deactivate`, {}),

  updatePhone: (userId: string, phone: string | null) =>
    apiClient.patch<{ phone: string | null }>(`/identity/api/admin/users/${userId}/phone`, { phone }),

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
