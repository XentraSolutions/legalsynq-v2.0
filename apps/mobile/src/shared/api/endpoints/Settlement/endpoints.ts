import { apiClient } from '@/shared/api/client';

import type { LienReduction, LienSettlement, SettlementPaymentDetail } from './types';

const BASE_PATH = '/liens/api/liens/settlement';

export const SettlementApi = {
  async listReductionsByCase(caseId: string): Promise<LienReduction[]> {
    const response = await apiClient.get<LienReduction[]>(`${BASE_PATH}/reductions/case/${caseId}`);
    return response.data;
  },

  async listByCase(caseId: string): Promise<LienSettlement[]> {
    const response = await apiClient.get<LienSettlement[]>(`${BASE_PATH}/case/${caseId}`);
    return response.data;
  },

  async listPaymentsByCase(caseId: string): Promise<SettlementPaymentDetail[]> {
    const response = await apiClient.get<SettlementPaymentDetail[]>(
      `${BASE_PATH}/payments/case/${caseId}`
    );
    return response.data;
  },
};
