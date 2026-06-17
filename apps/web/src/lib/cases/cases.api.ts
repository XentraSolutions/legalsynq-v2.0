import { apiClient } from "@/lib/api-client";
import type {
  CaseResponseDto,
  PaginatedResultDto,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
  CasesQuery,
  DashboardStats,
  CasePaginatedParams,
  CasePaginatedResult,
  CaseLiensApiResponse,
  CasesFilters,
  ExportResponse,
} from "./cases.types";

const BASE = "/lien/api/liens/cases";
function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(
      ([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`,
    );
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const casesApi = {
  list(id: CasesQuery) {
    return apiClient.get<CaseResponseDto>(`${BASE}`);
  },

  listBySearch(request: CasesQuery) {
    return apiClient.post<CasePaginatedResult>(`${BASE}/v3`, request);
  },

  getById(id: string) {
    return apiClient.get<CaseResponseDto>(`${BASE}/${id}`);
  },

  getByNumber(caseNumber: string) {
    return apiClient.get<CaseResponseDto>(
      `${BASE}/by-number/${encodeURIComponent(caseNumber)}`,
    );
  },

  create(request: CreateCaseRequestDto) {
    return apiClient.post<CaseResponseDto>(`${BASE}/create`, request);
  },

  update(id: string, request: UpdateCaseRequestDto) {
    return apiClient.put<CaseResponseDto>(`${BASE}/${id}`, request);
  },

  listLiensByCase(request: CasePaginatedParams) {
    return apiClient.post<CaseLiensApiResponse>(
      `/lien/api/liens/cases/liens/v3`,
      request,
    );
  },
  listLiensUpdatesByCase(request: CasePaginatedParams) {
    return apiClient.post<PaginatedResultDto<unknown>>(
      `${BASE}/liens-updates/`,
      request,
    );
  },

  listLiensUpdates(request: CasePaginatedParams) {
    return apiClient.post<PaginatedResultDto<unknown>>(
      `${BASE}/liens-updates/v3`,
      request,
    );
  },

  getCaseUpdates(request: CasePaginatedParams) {
    return apiClient.post<PaginatedResultDto<unknown>>(
      `${BASE}/case-updates/v3`,
      request,
    );
  },

  getCaseUpdatesv1(id: string) {
    return apiClient.get<PaginatedResultDto<unknown>>(
      `${BASE}/case-updates/${id}`,
    );
  },

  getCaseLiens(id: string) {
    return apiClient.post<CaseResponseDto>(`${BASE}/liens/v3`, {});
  },

  getDashboardStats() {
    return apiClient.get<DashboardStats>(`${BASE}/dashboard/piechart`);
  },
  export(request: CasesFilters) {
    return apiClient.post<ExportResponse>(`${BASE}/generate-csv`, request);
  },
};
