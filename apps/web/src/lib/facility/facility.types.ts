export interface FacilityGenericResponse {
  message: string
}

export interface CreateFacilityRequest {
  name: string
  email: string
  phone?: string
  addressLine1: string
  city: string
  state: string
  postalCode: string
}

export interface CreateFacilityResponse extends FacilityGenericResponse {}

export interface LegacyFacilityItem {
  id: string;
  name: string;
  code: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string;
  state: string;
  postalCode: string | null;
  phone: string | null;
  email: string;
  fax: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface FacilityListResponse {
  items: LegacyFacilityItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ContactPersonRequest {
  firstName: string
  lastName: string
  email: string
  phone?: string
  facilityId: string
}

export interface UpdateContactPersonRequest {
  id: string
  firstName: string
  lastName: string
  email: string
  phone?: string
  facilityId: string
}

export interface ContactPersonResponse extends FacilityGenericResponse {}

export interface FacilityStaff {
  id: string
  facilityId: string
  firstName: string
  lastName: string
  position: string | null
  email: string
  phone: string
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
  activeCases?: number
}

export type GetContactPersonByFacilityResponse = FacilityStaff[]