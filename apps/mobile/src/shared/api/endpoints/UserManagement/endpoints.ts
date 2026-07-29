import { apiClient } from '@/shared/api/client';

import type { ManagedUser } from './types';

const BASE_PATH = '/identity/api/users';

export const UserManagementApi = {
  async list(): Promise<ManagedUser[]> {
    const response = await apiClient.get<ManagedUser[]>(BASE_PATH);
    return response.data;
  },

  async get(id: string): Promise<ManagedUser> {
    const response = await apiClient.get<ManagedUser>(`${BASE_PATH}/${id}`);
    return response.data;
  },
};
