export interface ContactResponseDto {
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
  contactSubtype?: string | null;
  lawFirmId?: string | null;
  facilityId?: string | null;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateContactRequestDto {
  contactType: string;
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
  contactSubtype?: string;
  lawFirmId?: string;
  facilityId?: string;
}

export interface UpdateContactRequestDto {
  contactType: string;
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
  contactSubtype?: string;
  lawFirmId?: string;
  facilityId?: string;
}

export interface ContactsQuery {
  search?: string;
  ContactType?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  LawFirmId?: string;
  FacilityId?: string;
  // Explicit "" (as opposed to omitting the field) tells the API to only
  // return contacts with no subtype — i.e. main contacts, not sub-contacts
  // like law firm staff or facility staff.
  ContactSubtype?: string | null;
}

export interface ContactListItem {
  id: string;
  firstName: string;
  lastName: string;
  contactType: string;
  displayName: string;
  organization: string;
  email: string;
  phone: string;
  city: string;
  state: string;
  isActive: boolean;
  createdAt: string;
  facilityId: string | null;
  lawFirmId: string | null;
}

export interface ContactDetail extends ContactListItem {
  firstName: string;
  lastName: string;
  title: string;
  fax: string;
  website: string;
  addressLine1: string;
  postalCode: string;
  notes: string;
  updatedAt: string;
  contactSubtype: string | null;
  facilityId: string | null;
  lawFirmId: string | null;
}

export interface ExportResponse {
  data: string;
}

export interface ContactCaseSummary {
  id: string;
  caseNumber: string;
  personName: string;
  accidentType: string | null;
  dateOfLoss: string | null;
  dateOfBirth: string | null;
  status: string;
  // Lien-level fields — the case v3 endpoints (law/leads/medical/facility/funding)
  // return case records only, with no lien id or amounts. Null until the
  // API/type exposes them.
  lienId: string | null;
  billingAmount: number | null;
  purchaseAmount: number | null;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
