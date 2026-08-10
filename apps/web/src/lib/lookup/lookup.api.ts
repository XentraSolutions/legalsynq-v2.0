import { apiClient } from "@/lib/api-client";
import {
  AccidentTypeResponse,
  ContactsByIdResponse,
  DocumentTypeResponse,
  LawFirmListResponse,
  LiensStatusResponse,
  LookupGenericResponse,
  LookupResponse,
  MedicalProcedureCodesResponse,
  MedicalProcedureCodesLookupResponse,
  MedicalProcedureCostsResponse,
  MedicalProcedureCostsLookupResponse,
  MedicalProvidersResponse,
  TaskStatusResponse,
  UserListResponse,
  type LookupData,
} from "./lookup.types";
import { CaseStatusResponse } from "../cases/cases.types";

const BASE = "/lien/lookup";

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(
      ([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`,
    );
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const lookupApi = {
  getDocumentType() {
    return apiClient.get<DocumentTypeResponse[]>(`${BASE}/document/type`);
  },
  getTaskStatus() {
    return apiClient.get<TaskStatusResponse>(`${BASE}/task/status`);
  },
  getMedicalProcedureCodes() {
    return apiClient.get<MedicalProcedureCodesLookupResponse>(
      `${BASE}/medical/procedure/codes`,
    );
  },
  getMedicalProcedureCosts(code: MedicalProcedureCodesResponse["code"]) {
    return apiClient.get<MedicalProcedureCostsLookupResponse>(
      `${BASE}/medical/procedure/costs/${code}`,
    );
  },
  getLookupAll() {
    return apiClient.get<LookupResponse>(`${BASE}/all`);
  },
  getContactsById(id: string) {
    return apiClient.get<ContactsByIdResponse>(`${BASE}/contacts/${id}`);
  },
  getUserList() {
    return apiClient.get<UserListResponse>(`${BASE}/user-list`);
  },

  getCaseStatus() {
    return apiClient.get<CaseStatusResponse[]>(`${BASE}/case/status`);
  },

  getLawfirm() {
    return apiClient.get<LawFirmListResponse[]>(`${BASE}/contact/lawfirm`);
  },

  getContacts() {
    return apiClient.get<LookupGenericResponse[]>(`${BASE}/contact`);
  },

  getAccidentType() {
    return apiClient.get<AccidentTypeResponse[]>(`${BASE}/accident/type`);
  },

  getContactTypes() {
    return apiClient.get<LookupData[]>(`${BASE}/contact/type`);
  },

  getStates() {
    return apiClient.get<LookupGenericResponse[]>(`${BASE}/states`);
  },

  getLiensStatus() {
    return apiClient.get<LiensStatusResponse[]>(`${BASE}/liens/status`);
  },

  getFundingCompany() {
    return apiClient.get<LookupGenericResponse[]>(
      `${BASE}/contact/funding-company`,
    );
  },

  getCaseManagers() {
    return apiClient.get<LookupGenericResponse[]>(
      `/liens/contact/casemanagers`,
    );
  },

  getCaseManagersByLawfirm(id: string) {
    return apiClient.get<LookupGenericResponse[]>(`${BASE}/casemanager/${id}`);
  },

  getLawfirmRoles() {
    return apiClient.get<LookupGenericResponse[]>(
      `${BASE}/contact/lawfirm/role`,
    );
  },

  getMedicalProviders() {
    return apiClient.get<MedicalProvidersResponse[]>(
      `${BASE}/contact/medical-provider`,
    );
  },

  // Legacy: backed by facility/list/v3. Use the contacts API for new work.
  getMedicalFacility() {
    return apiClient.get<LookupGenericResponse[]>(`${BASE}/facility`);
  },

  getSettlementStatus() {
    return apiClient.get<LookupData[]>(`${BASE}/settlement/status`);
  },

  getSettlementType() {
    return apiClient.get<LookupData[]>(`${BASE}/settlement/type`);
  },

  getLawFirmContactRoles() {
    return apiClient.get<LookupData[]>(`${BASE}/contact/lawfirm/role`);
  },
};
