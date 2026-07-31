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
  SaveSellingLienInformationRequest,
  SaveSellingCaseInformationRequest,
  SaveSellingMedicalPricingRequest,
  SaveSellingDocumentsRequest,
  PrepareSellingLienRequest,
  ConfirmSellingLienSaleRequest,
  WithdrawSellingLienRequest,
  ArchiveSellingLienRequest,
  SubmitSellingLienRequest,
  LienListItem,
} from "./liens.types";
import { DashboardQuery } from "./dashboard.types";
import { DraftLienParams, LienInfoParams } from "../liens/liens.types";
import { LienDetailsResult } from "@/types/lien-selling";
import { LienListResult } from "./selling-liens.service";

const BASE = "/selling/api/liens/selling";

// prepare-sale / confirm-sale / withdraw-sale / archive all require an
// Idempotency-Key header per SellingV2Endpoints.cs (SellingIdempotency.TryGetKey).
function idempotencyHeaders(): Record<string, string> {
  return { "Idempotency-Key": crypto.randomUUID() };
}

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
    return apiClient.get<PaginatedResultDto<LienListItem>>(
      `${BASE}/liens${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<LienDetailsResult>(`${BASE}/liens/${id}`);
  },

  getDashboard(query: DashboardQuery = {}) {
    return apiClient.get<LienResponseDto>(
      `${BASE}/dashboard${toQs(query as Record<string, unknown>)}`,
    );
  },

  bulkUpload(request: FormData) {
    return apiClient.postForm<FormData>(`${BASE}/bulk-imports`, request);
  },

  downloadTemplate() {
    return apiClient.getBlob(`${BASE}/bulk-import-template`);
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

  createLienInfo(lienId: string, request: LienInfoParams) {
    return apiClient.put<any>(
      `${BASE}/liens/${lienId}/lien-information`,
      request,
    );
  },

  createLienDraft(request: DraftLienParams) {
    return apiClient.post<any>(`${BASE}/drafts`, request);
  },

  saveLienInformation(
    lienId: string,
    request: SaveSellingLienInformationRequest,
  ) {
    return apiClient.put<any>(
      `${BASE}/liens/${lienId}/lien-information`,
      request,
    );
  },

  saveCaseInformation(
    lienId: string,
    request: SaveSellingCaseInformationRequest,
  ) {
    return apiClient.put<any>(
      `${BASE}/liens/${lienId}/case-information`,
      request,
    );
  },

  saveMedicalPricing(
    lienId: string,
    request: SaveSellingMedicalPricingRequest,
  ) {
    return apiClient.put<any>(
      `${BASE}/liens/${lienId}/medical-pricing`,
      request,
    );
  },

  saveDocuments(lienId: string, request: SaveSellingDocumentsRequest) {
    return apiClient.put<any>(`${BASE}/liens/${lienId}/documents`, request);
  },

  prepareSale(lienId: string, request: PrepareSellingLienRequest) {
    return apiClient.post<any>(
      `${BASE}/liens/${lienId}/prepare-sale`,
      request,
      idempotencyHeaders(),
    );
  },

  confirmSale(lienId: string, request: ConfirmSellingLienSaleRequest) {
    return apiClient.post<any>(
      `${BASE}/liens/${lienId}/confirm-sale`,
      request,
      idempotencyHeaders(),
    );
  },

  withdrawSale(lienId: string, request: WithdrawSellingLienRequest = {}) {
    return apiClient.post<any>(
      `${BASE}/liens/${lienId}/withdraw-sale`,
      request,
      idempotencyHeaders(),
    );
  },

  archiveLien(lienId: string, request: ArchiveSellingLienRequest = {}) {
    return apiClient.post<any>(
      `${BASE}/liens/${lienId}/archive`,
      request,
      idempotencyHeaders(),
    );
  },

  submitLien(lienId: string, request: SubmitSellingLienRequest) {
    return apiClient.put<any>(
      `${BASE}/liens/${lienId}/lien-information`,
      request,
      idempotencyHeaders(),
    );
  },
};
