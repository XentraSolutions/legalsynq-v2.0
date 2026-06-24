import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type {
  CreateLienRequest,
  Lien,
  LienQueryParams,
  MakeOfferRequest,
  Offer,
  StatusHistoryEntry,
  UpdateLienRequest,
  UpdateOfferRequest,
} from './types';

export const lienKeys = {
  all: ['liens'] as const,
  list: (params: LienQueryParams) => [...lienKeys.all, 'list', params] as const,
  detail: (id: string) => [...lienKeys.all, 'detail', id] as const,
};

export const LiensApi = {
  async listLiens(params: LienQueryParams): Promise<PagedResult<Lien>> {
    const response = await apiClient.get<PagedResult<Lien>>('/liens', { params });
    return response.data;
  },

  async getLien(id: string): Promise<Lien> {
    const response = await apiClient.get<Lien>(`/liens/${id}`);
    return response.data;
  },

  async createLien(body: CreateLienRequest): Promise<Lien> {
    const response = await apiClient.post<Lien>('/liens', body);
    return response.data;
  },

  async updateLien(id: string, body: UpdateLienRequest): Promise<Lien> {
    const response = await apiClient.put<Lien>(`/liens/${id}`, body);
    return response.data;
  },

  async getLienStatusHistory(id: string): Promise<StatusHistoryEntry[]> {
    const response = await apiClient.get<StatusHistoryEntry[]>(`/liens/${id}/status-history`);
    return response.data;
  },

  async getLienOffers(lienId: string): Promise<Offer[]> {
    const response = await apiClient.get<Offer[]>(`/liens/${lienId}/offers`);
    return response.data;
  },

  async makeOffer(lienId: string, body: MakeOfferRequest): Promise<Offer> {
    const response = await apiClient.post<Offer>(`/liens/${lienId}/offers`, body);
    return response.data;
  },

  async updateOffer(
    lienId: string,
    offerId: string,
    body: UpdateOfferRequest
  ): Promise<Offer> {
    const response = await apiClient.patch<Offer>(`/liens/${lienId}/offers/${offerId}`, body);
    return response.data;
  },

  async withdrawOffer(lienId: string, offerId: string): Promise<void> {
    await apiClient.delete(`/liens/${lienId}/offers/${offerId}`);
  },
};
