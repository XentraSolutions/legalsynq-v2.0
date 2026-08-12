import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types";
import type { PaginatedResultDto } from "@/lib/contacts/contacts.types";
import {
  toContactPerson,
  type CompanyTypeLookupItem,
  type ContactPersonTypeLookupItem,
  type Company,
  type CompanyDetail,
  type CreateCompanyRequest,
  type UpdateCompanyRequest,
  type CreateContactPersonTypeRequest,
  type CompaniesQuery,
  type ContactPerson,
  type ContactPersonDto,
  type CreateContactPersonRequest,
  type UpdateContactPersonRequest,
  type CompaniesExportQuery,
  type ContactPersonsExportQuery,
  type ContactsExportQuery,
  type ReassignCompanyRequest,
  type ReassignContactPersonRequest,
  type CompanyDetailsQuery,
  type CompanyDetailsSummary,
} from "./companies.types";

const BASE = "/selling/api/liens/selling";

// Deactivate endpoints (DELETE) require an Idempotency-Key header per the
// "LegalSynq Company and Contact Person API" spec, same convention as
// selling-liens.api.ts's mutating endpoints.
function idempotencyHeaders(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

function toQs(params: Record<string, unknown>): string {
  const pairs: string[] = [];
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === "") continue;
    pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  }
  return pairs.length ? `?${pairs.join("&")}` : "";
}

// The API returns raw firstName/lastName with no displayName — compute it
// once here so every consumer of `ContactPerson` can rely on it existing.
function mapContactPersonResponse(
  res: ApiResponse<ContactPersonDto>,
): ApiResponse<ContactPerson> {
  return { ...res, data: toContactPerson(res.data) };
}

function mapContactPersonListResponse(
  res: ApiResponse<{ items: ContactPersonDto[] }>,
): ApiResponse<{ items: ContactPerson[] }> {
  return { ...res, data: { items: res.data.items.map(toContactPerson) } };
}

export const companiesApi = {
  companyTypes() {
    return apiClient.get<{ items: CompanyTypeLookupItem[] }>(
      `${BASE}/lookups/company-types`,
    );
  },

  contactPersonTypes(companyTypeId: string) {
    return apiClient.get<{ items: ContactPersonTypeLookupItem[] }>(
      `${BASE}/lookups/contact-person-types${toQs({ companyTypeId })}`,
    );
  },

  createContactPersonType(request: CreateContactPersonTypeRequest) {
    return apiClient.post<ContactPersonTypeLookupItem>(
      `${BASE}/lookups/contact-person-types`,
      request,
      idempotencyHeaders(),
    );
  },

  listCompanies(query: CompaniesQuery = {}) {
    return apiClient.get<PaginatedResultDto<Company>>(
      `${BASE}/companies${toQs(query as Record<string, unknown>)}`,
    );
  },

  createCompany(request: CreateCompanyRequest) {
    return apiClient.post<CompanyDetail>(
      `${BASE}/companies`,
      request,
      idempotencyHeaders(),
    );
  },

  getCompany(id: string) {
    return apiClient.get<CompanyDetail>(`${BASE}/companies/${id}`);
  },

  companyDetails(id: string, query: CompanyDetailsQuery = {}) {
    return apiClient.get<CompanyDetailsSummary>(
      `${BASE}/company-details/${id}${toQs(query as Record<string, unknown>)}`,
    );
  },

  updateCompany(id: string, request: UpdateCompanyRequest) {
    return apiClient.put<CompanyDetail>(
      `${BASE}/companies/${id}`,
      request,
      idempotencyHeaders(),
    );
  },

  deactivateCompany(id: string) {
    return apiClient.delete<void>(
      `${BASE}/companies/${id}`,
      idempotencyHeaders(),
    );
  },

  reactivateCompany(id: string) {
    return apiClient.put<void>(
      `${BASE}/companies/${id}/reactivate`,
      {},
      idempotencyHeaders(),
    );
  },

  reassignCompany(companyId: string, request: ReassignCompanyRequest) {
    return apiClient.post<void>(
      `${BASE}/companies/${companyId}/reassign`,
      request,
      idempotencyHeaders(),
    );
  },

  exportCompanies(query: CompaniesExportQuery = {}) {
    return apiClient.getBlob(
      `${BASE}/companies/export${toQs(query as Record<string, unknown>)}`,
    );
  },

  exportContacts(query: ContactsExportQuery = {}) {
    return apiClient.getBlob(
      `${BASE}/contacts/export${toQs(query as Record<string, unknown>)}`,
    );
  },

  listContactPersons(companyId: string, isActive?: boolean) {
    return apiClient
      .get<{ items: ContactPersonDto[] }>(
        `${BASE}/companies/${companyId}/contacts${toQs({ isActive })}`,
      )
      .then(mapContactPersonListResponse);
  },

  createContactPerson(companyId: string, request: CreateContactPersonRequest) {
    return apiClient
      .post<ContactPersonDto>(
        `${BASE}/companies/${companyId}/contacts`,
        request,
        idempotencyHeaders(),
      )
      .then(mapContactPersonResponse);
  },

  getContactPerson(companyId: string, contactId: string) {
    return apiClient
      .get<ContactPersonDto>(`${BASE}/companies/${companyId}/contacts/${contactId}`)
      .then(mapContactPersonResponse);
  },

  updateContactPerson(
    companyId: string,
    contactId: string,
    request: UpdateContactPersonRequest,
  ) {
    return apiClient
      .put<ContactPersonDto>(
        `${BASE}/companies/${companyId}/contacts/${contactId}`,
        request,
        idempotencyHeaders(),
      )
      .then(mapContactPersonResponse);
  },

  deactivateContactPerson(companyId: string, contactId: string) {
    return apiClient.delete<void>(
      `${BASE}/companies/${companyId}/contacts/${contactId}`,
      idempotencyHeaders(),
    );
  },

  reactivateContactPerson(companyId: string, contactId: string) {
    return apiClient.put<void>(
      `${BASE}/companies/${companyId}/contacts/${contactId}/reactivate`,
      {},
      idempotencyHeaders(),
    );
  },

  reassignContactPerson(
    companyId: string,
    contactId: string,
    request: ReassignContactPersonRequest,
  ) {
    return apiClient.post<void>(
      `${BASE}/companies/${companyId}/contacts/${contactId}/reassign`,
      request,
      idempotencyHeaders(),
    );
  },

  exportCompanyContacts(companyId: string, query: ContactPersonsExportQuery = {}) {
    return apiClient.getBlob(
      `${BASE}/companies/${companyId}/contacts/export${toQs(query as Record<string, unknown>)}`,
    );
  },
};
