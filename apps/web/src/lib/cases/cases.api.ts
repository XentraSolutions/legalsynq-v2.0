import { apiClient } from "@/lib/api-client";
import type {
  CaseResponseDto,
  PaginatedResultDto,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
  CasesQuery,
} from "./cases.types";

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
    return apiClient.get<CaseResponseDto>("/lien/api/liens/cases");
  },

  listBySearch(request: CasesQuery) {
    return apiClient.post<CaseResponseDto[]>(
      "/lien/api/liens/cases/v3",
      request,
    );
  },

  getById(id: string) {
    return apiClient.get<CaseResponseDto>(`/lien/api/liens/cases/${id}`);
  },

  getByNumber(caseNumber: string) {
    return apiClient.get<CaseResponseDto>(
      `/lien/api/liens/cases/by-number/${encodeURIComponent(caseNumber)}`,
    );
  },

  create(request: CreateCaseRequestDto) {
    return apiClient.post<CaseResponseDto>("/lien/api/liens/cases", request);
  },

  update(id: string, request: UpdateCaseRequestDto) {
    return apiClient.put<CaseResponseDto>(
      `/lien/api/liens/cases/${id}`,
      request,
    );
  },

  listLiensByCase(caseId: string) {
    return apiClient.get<any[]>(
      `/lien/api/liens/cases/liens-updates/${caseId}`,
    );
  },

  listLiensUpdates(caseId: string) {
    return apiClient.get<any[]>(
      `/lien/api/liens/cases/liens-updates/${caseId}`,
    );
  },

  getCaseUpdates(caseId: string) {
    return apiClient.get<PaginatedResultDto<any>>(
      `/lien/api/liens/cases/case-updates/${caseId}`,
    );
  },

  getCaseLiens(id: string) {
    return apiClient.get<any[]>(`lien/api/liens/cases/liens-updates/${id}`);
  },
};
