import { apiClient } from '@/shared/api/client';

import type { FundingApplicationDetail } from './types';

const BASE_PATH = '/fund/api/applications';

export const ApplicationsApi = {
  async get(applicationId: string): Promise<FundingApplicationDetail> {
    const response = await apiClient.get<FundingApplicationDetail>(`${BASE_PATH}/${applicationId}`);
    return response.data;
  },
};
