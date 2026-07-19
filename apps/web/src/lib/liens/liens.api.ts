import { apiClient } from '@/lib/api-client';
import type {
  LienResponseDto,
  LienOfferResponseDto,
  SaleFinalizationResultDto,
  PaginatedResultDto,
  CreateLienRequestDto,
  UpdateLienRequestDto,
  CreateLienOfferRequestDto,
  LiensQuery,
  ReassignFacilityRequestDto,
  ReassignContactPersonRequestDto,
  ReassignFundingCompanyRequestDto,
  ReassignMedicalProviderRequestDto,
} from './liens.types';

function toQs(params: Record<string, unknown>): string {
  const pairs: string[] = [];
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '') continue;
    if (Array.isArray(v)) {
      if (v.length === 0) continue;
      for (const item of v) {
        pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(item))}`);
      }
    } else {
      pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
    }
  }
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const liensApi = {
  list(query: LiensQuery = {}) {
    return apiClient.get<PaginatedResultDto<LienResponseDto>>(
      `/lien/api/liens/liens${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<LienResponseDto>(`/lien/api/liens/liens/${id}`);
  },

  getByNumber(lienNumber: string) {
    return apiClient.get<LienResponseDto>(
      `/lien/api/liens/liens/by-number/${encodeURIComponent(lienNumber)}`,
    );
  },

  create(request: CreateLienRequestDto) {
    return apiClient.post<LienResponseDto>('/lien/api/liens/liens', request);
  },

  update(id: string, request: UpdateLienRequestDto) {
    return apiClient.put<LienResponseDto>(`/lien/api/liens/liens/${id}`, request);
  },

  getOffers(lienId: string) {
    return apiClient.get<LienOfferResponseDto[]>(
      `/lien/api/liens/liens/${lienId}/offers`,
    );
  },

  createOffer(request: CreateLienOfferRequestDto) {
    return apiClient.post<LienOfferResponseDto>('/lien/api/liens/offers', request);
  },

  acceptOffer(offerId: string) {
    return apiClient.post<SaleFinalizationResultDto>(
      `/lien/api/liens/offers/${offerId}/accept`,
      {},
    );
  },

  withdraw(id: string) {
    return apiClient.post<LienResponseDto>(
      `/lien/api/liens/liens/${id}/withdraw`,
      {},
    );
  },

  reassignFacility(request: ReassignFacilityRequestDto) {
    return apiClient.post<LienResponseDto>(
      '/lien/api/liens/liens/reassign/facility',
      request,
    );
  },

  reassignContactPerson(request: ReassignContactPersonRequestDto) {
    return apiClient.post<LienResponseDto>(
      '/lien/api/liens/liens/reassign/contact-person',
      request,
    );
  },

  reassignFundingCompany(request: ReassignFundingCompanyRequestDto) {
    return apiClient.post<LienResponseDto>(
      '/lien/api/liens/liens/reassign/funding-company',
      request,
    );
  },

  reassignMedicalProvider(request: ReassignMedicalProviderRequestDto) {
    return apiClient.post<LienResponseDto>(
      '/lien/api/liens/liens/reassign/medical-provider',
      request,
    );
  },
};
