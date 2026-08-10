import { apiClient } from '@/shared/api/client';
import type {
  Facility,
  FacilityContactPerson,
  FacilityContactPersonRequest,
  FacilityListResult,
  FacilityQueryParams,
  FacilityRequest,
} from './types';

const BASE = '/liens/api/liens/facilities';
export const FacilitiesApi = {
  async list(params: FacilityQueryParams = {}): Promise<FacilityListResult> {
    return (await apiClient.get<FacilityListResult>(BASE, { params })).data;
  },
  async get(id: string): Promise<Facility> {
    return (await apiClient.get<Facility>(`${BASE}/${id}`)).data;
  },
  async create(body: FacilityRequest): Promise<Facility> {
    return (await apiClient.post<Facility>(BASE, body)).data;
  },
  async update(id: string, body: FacilityRequest): Promise<Facility> {
    return (await apiClient.put<Facility>(`${BASE}/${id}`, body)).data;
  },
  async deactivate(id: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}`);
  },
  async listStaff(id: string): Promise<FacilityContactPerson[]> {
    return (await apiClient.get<FacilityContactPerson[]>(`${BASE}/${id}/contact-persons`)).data;
  },
  async createStaff(
    id: string,
    body: FacilityContactPersonRequest
  ): Promise<FacilityContactPerson> {
    return (await apiClient.post<FacilityContactPerson>(`${BASE}/${id}/contact-persons`, body))
      .data;
  },
  async updateStaff(
    id: string,
    personId: string,
    body: FacilityContactPersonRequest
  ): Promise<FacilityContactPerson> {
    return (
      await apiClient.put<FacilityContactPerson>(`${BASE}/${id}/contact-persons/${personId}`, body)
    ).data;
  },
  async deleteStaff(id: string, personId: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}/contact-persons/${personId}`);
  },
};
