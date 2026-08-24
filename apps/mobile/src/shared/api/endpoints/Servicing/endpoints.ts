import { apiClient } from '@/shared/api/client';

import type {
  CreateServicingItemRequest,
  ServicingItem,
  ServicingListResult,
  ServicingQueryParams,
  UpdateServicingItemRequest,
} from './types';

const BASE_PATH = '/liens/api/liens/servicing';

export const ServicingApi = {
  async list(params: ServicingQueryParams = {}): Promise<ServicingListResult> {
    const response = await apiClient.get<ServicingListResult>(BASE_PATH, { params });
    return response.data;
  },

  async get(id: string): Promise<ServicingItem> {
    const response = await apiClient.get<ServicingItem>(`${BASE_PATH}/${id}`);
    return response.data;
  },

  async create(body: CreateServicingItemRequest): Promise<ServicingItem> {
    const response = await apiClient.post<ServicingItem>(BASE_PATH, body);
    return response.data;
  },

  async update(id: string, body: UpdateServicingItemRequest): Promise<ServicingItem> {
    const response = await apiClient.put<ServicingItem>(`${BASE_PATH}/${id}`, body);
    return response.data;
  },

  async updateStatus(id: string, status: string, resolution?: string): Promise<ServicingItem> {
    const response = await apiClient.put<ServicingItem>(`${BASE_PATH}/${id}/status`, {
      status,
      resolution,
    });
    return response.data;
  },
};
