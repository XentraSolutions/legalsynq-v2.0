import { apiClient } from "@/lib/api-client";
import type {
  ContactResponseDto,
  PaginatedResultDto,
  CreateContactRequestDto,
  UpdateContactRequestDto,
  ContactsQuery,
  ExportResponse,
} from "./contacts.types";

const BASE = "/lien/api/liens/contacts";

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    // ContactSubtype="" is a deliberate filter (main contacts only), not an
    // omitted param, so it's kept even though other blank strings are dropped.
    .filter(([k, v]) => v !== undefined && v !== null && (v !== "" || k === "ContactSubtype"))
    .map(
      ([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`,
    );
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const contactsApi = {
  list(query: ContactsQuery = {}) {
    return apiClient.get<PaginatedResultDto<ContactResponseDto>>(
      `${BASE}${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<ContactResponseDto>(`${BASE}/${id}`);
  },

  create(request: CreateContactRequestDto) {
    return apiClient.post<ContactResponseDto>(`${BASE}`, request);
  },

  update(id: string, request: UpdateContactRequestDto) {
    return apiClient.put<ContactResponseDto>(`${BASE}/${id}`, request);
  },

  deactivate(id: string) {
    return apiClient.put<ContactResponseDto>(`${BASE}/${id}/deactivate`, {});
  },

  reactivate(id: string) {
    return apiClient.put<ContactResponseDto>(`${BASE}/${id}/reactivate`, {});
  },

  delete(id: string) {
    return apiClient.delete<ContactResponseDto>(`/lien/contact/delete/${id}`);
  },

  export(contactType: string) {
    return apiClient.post<ExportResponse>(`${BASE}/export-csv`, {
      ContactType: contactType,
    });
  },
};
