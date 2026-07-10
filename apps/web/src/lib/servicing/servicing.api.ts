import { apiClient } from "@/lib/api-client";
import type {
  ServicingItemResponseDto,
  PaginatedResultDto,
  CreateServicingItemRequestDto,
  UpdateServicingItemRequestDto,
  UpdateServicingStatusRequestDto,
  ServicingQuery,
  UpdateServicingDetailsRequestDto,
  ServicingListItem,
  ExportResponse,
  ServicingListItemResponseDto,
} from "./servicing.types";
import {
  GenericPaginatedResult,
  GenericPaginationData,
} from "../lookup/lookup.types";

const BASE = "/lien/service";

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(
      ([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`,
    );
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const servicingApi = {
  list(query: GenericPaginationData) {
    return apiClient.post<GenericPaginatedResult<ServicingListItemResponseDto>>(
      `${BASE}/case/v3`,
      query,
    );
  },

  liensList(query: GenericPaginationData) {
    return apiClient.post<GenericPaginatedResult<ServicingListItemResponseDto>>(
      `${BASE}/all-liens/v3`,
      query,
    );
  },

  allLiensList(id: string) {
    return apiClient.get<GenericPaginatedResult<ServicingListItemResponseDto>>(
      `${BASE}/all-liens/${id}`,
    );
  },
  closedliensList(id: string) {
    return apiClient.get<GenericPaginatedResult<ServicingListItemResponseDto>>(
      `${BASE}/closed-liens/${id}`,
    );
  },

  getServiceCase(id: string) {
    return apiClient.get<GenericPaginatedResult<ServicingListItemResponseDto>>(
      `${BASE}/case`,
    );
  },

  getCase(id: string) {
    return apiClient.get<any>(`${BASE}/${id}`);
  },

  getById(id: string) {
    return apiClient.get<ServicingItemResponseDto>(
      `/lien/api/liens/servicing/${id}`,
    );
  },

  create(request: CreateServicingItemRequestDto) {
    return apiClient.post<ServicingItemResponseDto>(
      "/lien/api/liens/servicing",
      request,
    );
  },

  update(id: string, request: UpdateServicingItemRequestDto) {
    return apiClient.patch<ServicingItemResponseDto>(
      `/lien/api/liens/update/${id}`,
      request,
    );
  },

  updateDetails(request: UpdateServicingDetailsRequestDto) {
    return apiClient.patch<ServicingItemResponseDto>(
      `${BASE}/update-details`,
      request,
    );
  },

  updateStatus(id: string, request: UpdateServicingStatusRequestDto) {
    return apiClient.put<ServicingItemResponseDto>(
      `/lien/api/liens/servicing/${id}/status`,
      request,
    );
  },
  export(caseId: string = "") {
    return apiClient.post<ExportResponse>(`${BASE}/generate-csv`, {
      caseId: caseId,
    });
  },
};
