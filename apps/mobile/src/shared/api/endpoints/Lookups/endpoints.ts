import { apiClient } from '@/shared/api/client';

import type { LookupCategory, LookupValue } from './types';

const BASE_PATH = '/liens/api/liens/lookups';

export const LookupsApi = {
  async getByCategory(category: LookupCategory): Promise<LookupValue[]> {
    const response = await apiClient.get<LookupValue[]>(`${BASE_PATH}/${category}`);
    return response.data;
  },
};
