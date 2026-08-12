import type { PaginatedResultDto } from "@/lib/contacts/contacts.types";

export interface CompanyTypeLookupItem {
  id: string;
  name: string;
  code: string;
  sortOrder: number;
}

export interface ContactPersonTypeLookupItem {
  id: string;
  name: string;
  code: string;
  companyTypeId: string;
  sortOrder: number;
}

export interface CreateContactPersonTypeRequest {
  companyTypeId: string;
  code: string;
  name: string;
  sortOrder: number;
}

export interface Company {
  id: string;
  companyTypeId: string;
  companyTypeName: string;
  linkedTenantId: string | null;
  name: string;
  addressLine1: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  phone: string | null;
  email: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export type CompanyDetail = Company;

export interface CreateCompanyRequest {
  companyTypeId: string;
  linkedTenantId?: string | null;
  name: string;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  phone?: string | null;
  email?: string | null;
}

export interface UpdateCompanyRequest {
  linkedTenantId?: string | null;
  name: string;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  phone?: string | null;
  email?: string | null;
}

export interface CompaniesQuery {
  search?: string;
  isActive?: boolean;
  companyTypeId?: string;
  page?: number;
  pageSize?: number;
}

export type CompaniesExportQuery = Pick<CompaniesQuery, "search" | "companyTypeId" | "isActive">;

export interface ContactPersonsExportQuery {
  search?: string;
  contactPersonTypeId?: string;
  isActive?: boolean;
}

export interface ContactsExportQuery extends ContactPersonsExportQuery {
  companyTypeId?: string;
}

// Raw shape returned by the API — it has no displayName field, only
// firstName/lastName (unlike Company, which has a single `name`).
export interface ContactPersonDto {
  id: string;
  companyId: string;
  contactPersonTypeId: string;
  contactPersonTypeCode: string;
  contactPersonTypeName: string;
  firstName: string;
  lastName: string;
  addressLine1: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  phone: string | null;
  email: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

// Domain shape used throughout the UI — adds a client-computed displayName
// so components never have to know firstName/lastName aren't combined
// server-side.
export interface ContactPerson extends ContactPersonDto {
  displayName: string;
}

export function toContactPerson(dto: ContactPersonDto): ContactPerson {
  return { ...dto, displayName: `${dto.firstName} ${dto.lastName}`.trim() };
}

export interface CreateContactPersonRequest {
  contactPersonTypeId: string;
  firstName: string;
  lastName: string;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  phone?: string | null;
  email?: string | null;
}

export interface UpdateContactPersonRequest {
  contactPersonTypeId: string;
  firstName: string;
  lastName: string;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  phone?: string | null;
  email?: string | null;
}

export type CompaniesPaginatedResult = PaginatedResultDto<Company>;

export interface ReassignCompanyRequest {
  targetCompanyId: string;
}

export interface ReassignContactPersonRequest {
  targetContactPersonId: string;
}
