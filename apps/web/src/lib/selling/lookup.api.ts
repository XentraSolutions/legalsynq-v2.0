import { apiClient } from "@/lib/api-client";

const BASE = "/selling/api/liens/selling/lookups";

export interface SellingLookupItem {
  id: string;
  name: string;
}

export interface SellingMedicalCodeLookupItem {
  code: string;
  description: string;
}

// Mirrors SellingV2Endpoints.cs's lookup routes (GetFundingCompanies,
// GetFundingCompanyContacts). fundingCompanyContact previously built an
// invalid path segment (`.../fundingCompanyId=${id}`) instead of a query
// string, so it never actually hit the real route.
export const sellingLookupsApi = {
  fundingCompanies() {
    return apiClient.get<{ items: SellingLookupItem[] }>(
      `${BASE}/funding-companies`,
    );
  },
  fundingCompanyContacts(fundingCompanyId: string) {
    return apiClient.get<{ items: SellingLookupItem[] }>(
      `${BASE}/funding-company-contacts?fundingCompanyId=${encodeURIComponent(fundingCompanyId)}`,
    );
  },
  medicalCodes(search: string) {
    return apiClient.get<{ items: SellingMedicalCodeLookupItem[] }>(
      `${BASE}/medical-codes?search=${encodeURIComponent(search)}`,
    );
  },
  documentTypes() {
    return apiClient.get<{ items: string[] }>(`${BASE}/document-types`);
  },
};
