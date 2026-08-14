import { apiClient } from '@/shared/api/client';
import type { PagedResult } from '@/shared/types/api';

import type {
  CreateLienRequest,
  CreateManagementLienRequest,
  Lien,
  LienExportFile,
  LienExportFilter,
  LienFacility,
  LienFacilityContact,
  LienDocumentType,
  LienFacilityRequest,
  LienMedicalCodeRequest,
  LienMedicalRequest,
  LienQueryParams,
  ManagementLien,
  ManagementLienDetails,
  ManagementLienQueryParams,
  MakeOfferRequest,
  Offer,
  StatusHistoryEntry,
  UpdateLienRequest,
  UpdateManagementLienRequest,
  UpdateOfferRequest,
} from './types';

const LIENS_BASE_PATH = '/liens/api/liens/liens';
const CASE_LIENS_BASE_PATH = '/liens/api/liens/cases/liens';
const FACILITIES_BASE_PATH = '/liens/api/liens/facilities';

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
}

function unwrapData(value: unknown): unknown {
  let current = value;
  for (let index = 0; index < 2; index += 1) {
    const record = asRecord(current);
    if (!record || !('data' in record)) break;
    current = record.data;
  }
  return current;
}

export const lienKeys = {
  all: ['liens'] as const,
  list: (params: LienQueryParams) => [...lienKeys.all, 'list', params] as const,
  detail: (id: string) => [...lienKeys.all, 'detail', id] as const,
};

export const LiensApi = {
  async listLiens(params: LienQueryParams): Promise<PagedResult<Lien>> {
    const response = await apiClient.get<PagedResult<Lien>>(LIENS_BASE_PATH, { params });
    return response.data;
  },

  async getLien(id: string): Promise<Lien> {
    const response = await apiClient.get<Lien>(`${LIENS_BASE_PATH}/${id}`);
    return response.data;
  },

  async createLien(body: CreateLienRequest): Promise<Lien> {
    const response = await apiClient.post<Lien>(LIENS_BASE_PATH, body);
    return response.data;
  },

  async updateLien(id: string, body: UpdateLienRequest): Promise<Lien> {
    const response = await apiClient.put<Lien>(`${LIENS_BASE_PATH}/${id}`, body);
    return response.data;
  },

  async getLienStatusHistory(id: string): Promise<StatusHistoryEntry[]> {
    const response = await apiClient.get<StatusHistoryEntry[]>(`/liens/api/liens/${id}/status-history`);
    return response.data;
  },

  async getLienOffers(lienId: string): Promise<Offer[]> {
    const response = await apiClient.get<Offer[]>(`/liens/api/liens/${lienId}/offers`);
    return response.data;
  },

  async makeOffer(lienId: string, body: MakeOfferRequest): Promise<Offer> {
    const response = await apiClient.post<Offer>(`/liens/api/liens/${lienId}/offers`, body);
    return response.data;
  },

  async updateOffer(
    lienId: string,
    offerId: string,
    body: UpdateOfferRequest
  ): Promise<Offer> {
    const response = await apiClient.patch<Offer>(`/liens/api/liens/${lienId}/offers/${offerId}`, body);
    return response.data;
  },

  async withdrawOffer(lienId: string, offerId: string): Promise<void> {
    await apiClient.delete(`/liens/api/liens/${lienId}/offers/${offerId}`);
  },

  async listManagementLiens(
    params: ManagementLienQueryParams
  ): Promise<PagedResult<ManagementLien>> {
    const response = await apiClient.get<PagedResult<ManagementLien>>(LIENS_BASE_PATH, { params });
    return response.data;
  },

  async listAllManagementLiens(
    filters: Omit<ManagementLienQueryParams, 'page' | 'pageSize'> = {}
  ): Promise<ManagementLien[]> {
    const items: ManagementLien[] = [];
    const pageSize = 200;
    let page = 1;
    let totalCount = Number.POSITIVE_INFINITY;

    while (items.length < totalCount) {
      const response = await apiClient.get<PagedResult<ManagementLien>>(LIENS_BASE_PATH, {
        params: { ...filters, page, pageSize },
      });
      const result = response.data;
      items.push(...result.items);
      totalCount = result.totalCount;
      if (result.items.length === 0) break;
      page += 1;
    }

    return items;
  },

  async listAllCaseLiens(caseId: string): Promise<ManagementLien[]> {
    const items: ManagementLien[] = [];
    const limit = 200;
    let page = 1;
    let totalCount = Number.POSITIVE_INFINITY;

    while (items.length < totalCount) {
      const response = await apiClient.post<PagedResult<ManagementLien>>(
        `${CASE_LIENS_BASE_PATH}/${caseId}`,
        { page, limit }
      );
      const result = response.data;
      items.push(...result.items);
      totalCount = result.totalCount;
      if (result.items.length === 0) break;
      page += 1;
    }

    return items;
  },

  async listFacilities(): Promise<LienFacility[]> {
    const response = await apiClient.get<PagedResult<LienFacility>>(FACILITIES_BASE_PATH, {
      params: { isActive: true, page: 1, pageSize: 500 },
    });
    return response.data.items;
  },

  async listFacilityContacts(facilityId: string): Promise<LienFacilityContact[]> {
    const response = await apiClient.get<LienFacilityContact[]>(
      `${FACILITIES_BASE_PATH}/${facilityId}/contact-persons`
    );
    return response.data;
  },

  async listDocumentTypes(): Promise<LienDocumentType[]> {
    const response = await apiClient.get<LienDocumentType[]>('/liens/lookup/document/type');
    return response.data.filter((item) => item.isActive);
  },

  async getManagementLien(id: string): Promise<ManagementLien> {
    const response = await apiClient.get<ManagementLien>(`${LIENS_BASE_PATH}/${id}`);
    return response.data;
  },

  async getManagementLienDetails(id: string): Promise<ManagementLienDetails> {
    const response = await apiClient.post<unknown>(`${LIENS_BASE_PATH}/details/${id}`, {});
    const payload = asRecord(unwrapData(response.data));
    return {
      medicalList: Array.isArray(payload?.medicalList) ? payload.medicalList : [],
      facilityList: Array.isArray(payload?.facilityList) ? payload.facilityList : [],
      codeList: Array.isArray(payload?.codeList) ? payload.codeList : [],
      documentList: Array.isArray(payload?.documentList) ? payload.documentList : [],
    } as ManagementLienDetails;
  },

  async createManagementLien(body: CreateManagementLienRequest): Promise<ManagementLien> {
    const response = await apiClient.post<ManagementLien>(LIENS_BASE_PATH, body);
    return response.data;
  },

  async updateManagementLien(
    id: string,
    body: UpdateManagementLienRequest
  ): Promise<ManagementLien> {
    const response = await apiClient.put<ManagementLien>(`${LIENS_BASE_PATH}/${id}`, body);
    return response.data;
  },

  async createMedicalInfo(body: LienMedicalRequest): Promise<void> {
    await apiClient.post(`${CASE_LIENS_BASE_PATH}/medical`, body);
  },

  async updateMedicalInfo(body: LienMedicalRequest): Promise<void> {
    await apiClient.post(`${CASE_LIENS_BASE_PATH}/update-medical`, body);
  },

  async createFacilityInfo(body: LienFacilityRequest): Promise<void> {
    await apiClient.post(`${CASE_LIENS_BASE_PATH}/facility`, body);
  },

  async updateFacilityInfo(body: LienFacilityRequest): Promise<void> {
    await apiClient.post(`${CASE_LIENS_BASE_PATH}/update-facility`, body);
  },

  async createMedicalCode(body: LienMedicalCodeRequest): Promise<void> {
    await apiClient.post(`${CASE_LIENS_BASE_PATH}/medicalcode`, body);
  },

  async updateMedicalCode(body: LienMedicalCodeRequest): Promise<void> {
    await apiClient.post(`${CASE_LIENS_BASE_PATH}/update-medicalcode`, body);
  },

  async deleteMedicalCode(id: string): Promise<void> {
    await apiClient.delete(`${LIENS_BASE_PATH}/delete-medicalcode/${id}`);
  },

  async deleteDocument(id: string): Promise<void> {
    await apiClient.delete(`${LIENS_BASE_PATH}/delete-medicaldocument/${id}`);
  },

  async exportLiens(body: LienExportFilter): Promise<LienExportFile> {
    const response = await apiClient.post<unknown>(`${CASE_LIENS_BASE_PATH}/generate-csv`, body);
    const data = unwrapData(response.data);
    const files = Array.isArray(data) ? data : [];
    const file = asRecord(files[0]);
    if (!file || typeof file.base64 !== 'string' || typeof file.filename !== 'string') {
      throw new Error('The lien export did not contain a file.');
    }
    return file as unknown as LienExportFile;
  },
};
