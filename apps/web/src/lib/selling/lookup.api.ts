import { apiClient } from "@/lib/api-client";

const BASE = "/selling/api/liens/selling/lookups";

export interface SellingLookupItem {
  id: string;
  name: string;
}

/** Funding-company contacts also carry an email (see SellingV2Endpoints.GetFundingCompanyContacts). */
export interface SellingFundingCompanyContactItem extends SellingLookupItem {
  email: string | null;
}

/** Facilities come from the Facilities table, not Contacts, and carry a code instead (see SellingV2Endpoints.GetFacilities). */
export interface SellingFacilityItem extends SellingLookupItem {
  code: string | null;
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
    return apiClient.get<{ items: SellingFundingCompanyContactItem[] }>(
      `${BASE}/funding-company-contacts?fundingCompanyId=${encodeURIComponent(fundingCompanyId)}`,
    );
  },
  medicalCodes(search: string) {
    return apiClient.get<{ data: SellingMedicalCodeLookupItem[] }>(
      `/lien/lookup/medical/procedure/codes`,
    );
  },
  documentTypes() {
    return apiClient.get<{ items: string[] }>(`${BASE}/document-types`);
  },
  facilities() {
    return apiClient.get<{ items: SellingFacilityItem[] }>(`${BASE}/facilities`);
  },
  lawFirms() {
    return apiClient.get<{ items: SellingLookupItem[] }>(`${BASE}/law-firms`);
  },
  caseManagers(lawFirmId: string) {
    return apiClient.get<{ items: SellingLookupItem[] }>(
      `${BASE}/case-managers?lawFirmId=${encodeURIComponent(lawFirmId)}`,
    );
  },
};
