import { apiClient } from '@/lib/api-client'
import {
  ContactPersonRequest,
  ContactPersonResponse,
  CreateFacilityRequest,
  CreateFacilityResponse,
  FacilityListResponse,
  GetContactPersonByFacilityResponse,
} from './facility.types'

const BASE = '/lien/facility'

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
  return pairs.length ? `?${pairs.join('&')}` : ''
}

export const facilityApi = {
  createFacility(form: CreateFacilityRequest) {
    return apiClient.post<CreateFacilityResponse>(`${BASE}/create`, form)
  },
  getFacilityList() {
    return apiClient.post<FacilityListResponse>(`${BASE}/list/v3`, {})
  },
  contactPerson(form: ContactPersonRequest) {
    return apiClient.post<ContactPersonResponse>(`${BASE}/contactperson`, form)
  },
  getContactPersonByFacility(facilityId: string) {
    return apiClient.post<GetContactPersonByFacilityResponse>(`${BASE}/get-contactperson/${facilityId}`, {})
  }
}
