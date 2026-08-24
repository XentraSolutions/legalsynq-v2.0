import { apiClient } from '@/shared/api/client';

import type { UpdatePhoneRequest } from './types';

export const UserApi = {
  async updateAvatar(formData: FormData): Promise<void> {
    await apiClient.patch('/identity/profile/avatar', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  async updatePhone(body: UpdatePhoneRequest): Promise<void> {
    await apiClient.patch('/identity/profile/phone', body);
  },
};
