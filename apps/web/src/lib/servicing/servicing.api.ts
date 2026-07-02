import { apiClient } from "@/lib/api-client";
import type {
  ServicingItemResponseDto,
  PaginatedResultDto,
  CreateServicingItemRequestDto,
  UpdateServicingItemRequestDto,
  UpdateServicingStatusRequestDto,
  ServicingQuery,
  UpdateServicingDetailsRequestDto,
} from "./servicing.types";
import { GenericPaginationData } from "../lookup/lookup.types";

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(
      ([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`,
    );
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const servicingApi = {
  list(query: ServicingQuery = {}) {
    return apiClient.get<PaginatedResultDto<ServicingItemResponseDto>>(
      `/lien/api/liens/servicing${toQs(query as Record<string, unknown>)}`,
    );
  },
  getCase(id: string) {
    return apiClient.get<any>(`/lien/service/${id}`);
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
      `/lien/service/update-details`,
      request,
    );
  },

  updateStatus(id: string, request: UpdateServicingStatusRequestDto) {
    return apiClient.put<ServicingItemResponseDto>(
      `/lien/api/liens/servicing/${id}/status`,
      request,
    );
  },
};
