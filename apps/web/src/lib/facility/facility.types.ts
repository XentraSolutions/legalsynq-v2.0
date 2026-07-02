export interface FacilityGenericResponse {
  message: string;
}

export interface CreateFacilityRequest {
  name: string;
  email: string;
  phone?: string;
  addressLine1?: string;
  address?: string;
  city: string;
  state: string;
  postalCode?: string;
  zipcode?: string;
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
  facilityId: string;
  firstname: string;
  lastname: string;
  position?: string;
  email: string;
  phone?: string;
  isActive?: boolean;
}

export interface UpdateContactPersonRequest {
  contactPersonId: string;
  facilityId: string;
  firstName: string;
  lastName: string;
  position?: string;
  email: string;
  phone?: string;
  isActive?: boolean;
}

export interface ContactPersonResponse extends FacilityGenericResponse {}

export interface FacilityStaff {
  id: string;
  facilityId: string;
  firstName: string;
  lastName: string;
  position: string | null;
  email: string;
  phone: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  activeCases?: number;
}

export type GetContactPersonByFacilityResponse = FacilityStaff[];
