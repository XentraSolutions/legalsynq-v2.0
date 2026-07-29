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

const BASE = "/selling/api/liens/selling/lookups";

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
  fundingCompanies() {
    return apiClient.get<PaginatedResultDto<any>>(`${BASE}/funding-companies`);
  },
  fundingCompanyContact(id: string) {
    return apiClient.get<PaginatedResultDto<any>>(
      `${BASE}/funding-company-contacts/fundingCompanyId=${id}`,
    );
  },
};
