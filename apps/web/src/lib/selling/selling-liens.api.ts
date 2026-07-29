import { apiClient } from "@/lib/api-client";
import type {
  LienResponseDto,
  LienOfferResponseDto,
  SaleFinalizationResultDto,
  PaginatedResultDto,
  CreateLienRequestDto,
  UpdateLienRequestDto,
  CreateLienOfferRequestDto,
  LiensQuery,
  ReassignFacilityRequestDto,
  ReassignContactPersonRequestDto,
  ReassignFundingCompanyRequestDto,
  ReassignMedicalProviderRequestDto,
} from "./liens.types";
import { DashboardQuery } from "./dashboard.types";
import { DraftLienParams, LienInfoParams } from "../liens/liens.types";
import { LienDetailsResult } from "@/types/lien-selling";

const BASE = "/selling/api/liens/selling";

// Arrays are sent as a single comma-joined value (e.g. `lawFirmIds=a,b`) —
// same convention the cases v3 endpoint uses for its multi-select filters
// (see cases/page.tsx's `.join(",")` query fields) — rather than repeated
// query keys.
function toQs(params: Record<string, unknown>): string {
  const pairs: string[] = [];
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === "") continue;
    if (Array.isArray(v)) {
      if (v.length === 0) continue;
      pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(v.join(","))}`);
    } else {
      pairs.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
    }
  }
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const liensApi = {
  list(query: LiensQuery = {}) {
    return apiClient.get<PaginatedResultDto<any>>(
      `${BASE}/liens${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<LienDetailsResult>(`${BASE}/liens/${id}`);
  },

  getDashboard(query: DashboardQuery = {}) {
    console.log(query);
    return apiClient.get<LienResponseDto>(
      `${BASE}/dashboard${toQs(query as Record<string, unknown>)}`,
    );
  },

  bulkUpload(request: FormData) {
    return apiClient.postForm<FormData>(`${BASE}/bulk-imports`, request);
  },

  downloadTemplate() {
    return apiClient.get<any>(`${BASE}/bulk-import-template`);
  },

  confirmUpload(id: string) {
    return apiClient.post<any>(
      `/selling/bulk-upload/api/liens/selling/bulk-imports/${id}/confirm`,
      {},
    );
  },

  validateUpload(id: string) {
    return apiClient.post<any>(`${BASE}/bulk-imports/${id}/validate`, {});
  },

  createLienInfo(request: LienInfoParams) {
    return apiClient.post<any>(`${BASE}/liens/lien-information`, request);
  },

  createLienDraft(request: DraftLienParams) {
    return apiClient.post<any>(`${BASE}/drafts`, request);
  },
};
