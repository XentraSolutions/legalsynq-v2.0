import { apiClient } from '@/lib/api-client';
import type {
  AddSellingPortfolioLiensRequestDto,
  AddSellingPortfolioBuyersRequestDto,
  CreateSellingPortfolioRequestDto,
  PaginatedResultDto,
  SendLienBuyerEmailRequestDto,
  SendLienBuyerEmailResponseDto,
  SellingPortfolioActivityDto,
  SellingPortfolioAnalyticsDto,
  SellingPortfolioDto,
  SellingPortfolioQuery,
  SellingPortfolioStatusHistoryDto,
  TransitionSellingPortfolioStatusRequestDto,
  UpdateSellingPortfolioRequestDto,
} from './lien-sales.types';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

const BASE = '/lien/api/liens/selling/portfolios';

export const lienSalesApi = {
  list(query: SellingPortfolioQuery = {}) {
    return apiClient.get<PaginatedResultDto<SellingPortfolioDto>>(
      `${BASE}${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<SellingPortfolioDto>(`${BASE}/${id}`);
  },

  create(request: CreateSellingPortfolioRequestDto) {
    return apiClient.post<SellingPortfolioDto>(BASE, request);
  },

  update(id: string, request: UpdateSellingPortfolioRequestDto) {
    return apiClient.put<SellingPortfolioDto>(`${BASE}/${id}`, request);
  },

  addLiens(id: string, request: AddSellingPortfolioLiensRequestDto) {
    return apiClient.post(`${BASE}/${id}/liens`, request);
  },

  removeLiens(id: string, lienIds: string[]) {
    return apiClient.post(`${BASE}/${id}/liens/remove`, { lienIds });
  },

  addBuyers(id: string, request: AddSellingPortfolioBuyersRequestDto) {
    return apiClient.post<SellingPortfolioDto>(`${BASE}/${id}/buyers`, request);
  },

  transitionStatus(id: string, request: TransitionSellingPortfolioStatusRequestDto) {
    return apiClient.post<SellingPortfolioDto>(`${BASE}/${id}/status`, request);
  },

  publish(id: string, notes?: string) {
    return apiClient.post<SellingPortfolioDto>(`${BASE}/${id}/publish`, { notes });
  },

  withdraw(id: string, notes?: string) {
    return apiClient.post<SellingPortfolioDto>(`${BASE}/${id}/withdraw`, { notes });
  },

  sendBuyerEmail(id: string, lienIdOrCode: string, request: SendLienBuyerEmailRequestDto) {
    return apiClient.post<SendLienBuyerEmailResponseDto>(
      `${BASE}/${id}/liens/${encodeURIComponent(lienIdOrCode)}/buyer-email`,
      request,
    );
  },

  getActivity(id: string) {
    return apiClient.get<SellingPortfolioActivityDto[]>(`${BASE}/${id}/activity`);
  },

  getStatusHistory(id: string) {
    return apiClient.get<SellingPortfolioStatusHistoryDto[]>(`${BASE}/${id}/status-history`);
  },

  getAnalytics(id: string) {
    return apiClient.get<SellingPortfolioAnalyticsDto>(`${BASE}/${id}/analytics`);
  },
};
