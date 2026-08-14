import type { PagedResult } from '@/shared/types/api';

export type ContactType =
  | 'LawFirm'
  | 'Provider'
  | 'LienHolder'
  | 'Lead'
  | 'CaseManager'
  | 'InternalUser';

export interface ContactQueryParams {
  search?: string;
  contactType?: ContactType | string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface Contact {
  id: string;
  contactType: string;
  firstName: string;
  lastName: string;
  displayName: string;
  title?: string | null;
  organization?: string | null;
  email?: string | null;
  phone?: string | null;
  fax?: string | null;
  website?: string | null;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  notes?: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateContactRequest {
  contactType: ContactType | string;
  firstName: string;
  lastName: string;
  title?: string;
  organization?: string;
  email?: string;
  phone?: string;
  fax?: string;
  website?: string;
  addressLine1?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  notes?: string;
}

export type UpdateContactRequest = CreateContactRequest;
export type ContactListResult = Omit<PagedResult<Contact>, 'totalPages'> & {
  totalPages?: number;
};
