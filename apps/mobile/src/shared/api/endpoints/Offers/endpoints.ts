import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type { Offer, OfferActionRequest, OfferQueryParams } from './types';

export const offerKeys = {
  all: ['offers'] as const,
  list: (params: OfferQueryParams) => [...offerKeys.all, 'list', params] as const,
  detail: (id: string) => [...offerKeys.all, 'detail', id] as const,
};

export const OffersApi = {
  async listOffers(params: OfferQueryParams): Promise<PagedResult<Offer>> {
    const response = await apiClient.get<PagedResult<Offer>>('/liens/api/liens/offers', { params });
    return response.data;
  },

  async getOffer(id: string): Promise<Offer> {
    const response = await apiClient.get<Offer>(`/liens/api/liens/offers/${id}`);
    return response.data;
  },

  async acceptOffer(id: string, body: OfferActionRequest = {}): Promise<Offer> {
    const response = await apiClient.patch<Offer>(`/liens/api/liens/offers/${id}/accept`, body);
    return response.data;
  },

  async declineOffer(id: string, body: OfferActionRequest = {}): Promise<Offer> {
    const response = await apiClient.patch<Offer>(`/liens/api/liens/offers/${id}/decline`, body);
    return response.data;
  },
};
