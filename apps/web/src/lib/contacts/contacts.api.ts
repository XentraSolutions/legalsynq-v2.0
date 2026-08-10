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

function splitFullName(
  fullName: string,
): Pick<CreateContactRequestDto, "firstName" | "lastName"> {
  const parts = fullName
    .trim()
    .split(/\s+/)
    .filter(Boolean);

  if (parts.length === 0) {
    return { firstName: "", lastName: "" };
  }

  if (parts.length === 1) {
    return { firstName: parts[0], lastName: "" };
  }

  return {
    firstName: parts.slice(0, -1).join(" "),
    lastName: parts.at(-1) ?? "",
  };
}

function normalizeContactRequest<T extends CreateContactRequestDto | UpdateContactRequestDto>(
  request: T,
): T {
  const fullName = request.fullName ?? request.fullname;
  if (!fullName?.trim()) {
    return request;
  }

  const normalizedFirstName = request.firstName?.trim();
  const normalizedLastName = request.lastName?.trim();
  if (normalizedFirstName && normalizedLastName) {
    return {
      ...request,
      fullName: fullName.trim(),
      firstName: normalizedFirstName,
      lastName: normalizedLastName,
    };
  }

  const { firstName, lastName } = splitFullName(fullName);
  return {
    ...request,
    fullName: fullName.trim(),
    firstName: normalizedFirstName || firstName,
    lastName: normalizedLastName || lastName,
  };
}

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    // ContactSubtype="" is a deliberate filter (main contacts only), not an
    // omitted param, so it's kept even though other blank strings are dropped.
    .filter(
      ([k, v]) =>
        v !== undefined && v !== null && (v !== "" || k === "ContactSubtype"),
    )
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
    return apiClient.post<ContactResponseDto>(
      `${BASE}`,
      normalizeContactRequest(request),
    );
  },

  update(id: string, request: UpdateContactRequestDto) {
    return apiClient.put<ContactResponseDto>(
      `${BASE}/${id}`,
      normalizeContactRequest(request),
    );
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
