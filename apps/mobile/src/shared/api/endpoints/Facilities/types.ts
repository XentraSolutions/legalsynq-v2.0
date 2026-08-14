import type { PagedResult } from '@/shared/types/api';

export interface FacilityQueryParams {
  search?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}
export interface Facility {
  id: string;
  name: string;
  code?: string | null;
  externalReference?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  phone?: string | null;
  email?: string | null;
  fax?: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface FacilityContactPerson {
  id: string;
  facilityId: string;
  firstName: string;
  lastName: string;
  position?: string | null;
  email?: string | null;
  phone?: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface FacilityRequest {
  name: string;
  code?: string;
  externalReference?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  phone?: string;
  email?: string;
  fax?: string;
}
export interface FacilityContactPersonRequest {
  firstName: string;
  lastName: string;
  position?: string;
  email?: string;
  phone?: string;
}
export type FacilityListResult = Omit<PagedResult<Facility>, 'totalPages'> & {
  totalPages?: number;
};
