import { apiClient } from "@/lib/api-client";
import type {
  CaseResponseDto,
  PaginatedResultDto,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
  CasesQuery,
  DashboardStats,
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
    return apiClient.post<CaseResponseDto[]>(`${BASE}/v3`, request);
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
    return apiClient.post<CaseResponseDto>("${BASE}", request);
  },

  update(id: string, request: UpdateCaseRequestDto) {
    return apiClient.put<CaseResponseDto>(`${BASE}/${id}`, request);
  },

  listLiensByCase(caseId: string) {
    return apiClient.get<any[]>(`${BASE}/liens-updates/${caseId}`);
  },

  listLiensUpdates(caseId: string) {
    return apiClient.get<any[]>(`${BASE}/liens-updates/${caseId}`);
  },

  getCaseUpdates(caseId: string) {
    return apiClient.get<PaginatedResultDto<any>>(
      `${BASE}/case-updates/${caseId}`,
    );
  },

  getCaseLiens(id: string) {
    return apiClient.post<CaseResponseDto>(`${BASE}/liens/v3`, {});
  },

  getDashboardStats() {
    return apiClient.get<DashboardStats>(`${BASE}/dashboard/piechart`)
  }
};
