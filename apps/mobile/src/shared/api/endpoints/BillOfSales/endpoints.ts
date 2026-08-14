import { apiClient } from '@/shared/api/client';

import type { BillOfSale, BillOfSaleListResult, BillOfSaleQueryParams } from './types';

const BASE_PATH = '/liens/api/liens/bill-of-sales';

export const BillOfSalesApi = {
  async list(params: BillOfSaleQueryParams = {}): Promise<BillOfSaleListResult> {
    const response = await apiClient.get<BillOfSaleListResult>(BASE_PATH, { params });
    return response.data;
  },

  async get(id: string): Promise<BillOfSale> {
    const response = await apiClient.get<BillOfSale>(`${BASE_PATH}/${id}`);
    return response.data;
  },

  async getByNumber(billOfSaleNumber: string): Promise<BillOfSale> {
    const response = await apiClient.get<BillOfSale>(
      `${BASE_PATH}/by-number/${encodeURIComponent(billOfSaleNumber)}`
    );
    return response.data;
  },

  async listByLien(lienId: string): Promise<BillOfSale[]> {
    const response = await apiClient.get<BillOfSale[]>(
      `/liens/api/liens/liens/${lienId}/bill-of-sales`
    );
    return response.data;
  },

  async submit(id: string): Promise<BillOfSale> {
    const response = await apiClient.put<BillOfSale>(`${BASE_PATH}/${id}/submit`);
    return response.data;
  },

  async execute(id: string): Promise<BillOfSale> {
    const response = await apiClient.put<BillOfSale>(`${BASE_PATH}/${id}/execute`);
    return response.data;
  },

  async cancel(id: string): Promise<BillOfSale> {
    const response = await apiClient.put<BillOfSale>(`${BASE_PATH}/${id}/cancel`);
    return response.data;
  },
};
