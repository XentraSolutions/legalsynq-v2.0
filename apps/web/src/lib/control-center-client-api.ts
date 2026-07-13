import { apiClient } from '@/lib/api-client';
import type { TenantUserSummary } from '@/types/control-center';

export const controlCenterClientApi = {
  tenants: {
    activate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/tenants/${id}/activate`, {}),

    deactivate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/tenants/${id}/deactivate`, {}),
  },

  users: {
    create: (body: {
      tenantId:  string;
      email:     string;
      password:  string;
      firstName: string;
      lastName:  string;
      roleIds?:  string[];
    }) => apiClient.post<TenantUserSummary>('/identity/api/users', body),

    deactivate: (id: string) =>
      apiClient.post<void>(`/identity/api/admin/users/${id}/deactivate`, {}),
  },

  products: {
    setTenantEntitlement: (tenantId: string, productCode: string, enabled: boolean) =>
      apiClient.post<void>(
        `/tenant/api/v1/admin/tenants/${tenantId}/entitlements/${productCode}`,
        { enabled },
      ),
  },
};
