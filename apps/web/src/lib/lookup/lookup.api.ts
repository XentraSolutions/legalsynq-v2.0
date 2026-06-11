import { apiClient } from "@/lib/api-client";
import {
  ContactsByIdResponse,
  LookupResponse,
  MedicalProcedureCodesResponse,
  MedicalProcedureCostsResponse,
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
    return apiClient.get<LookupData[]>(`${BASE}/document/type`);
  },
  getTaskStatus() {
    return apiClient.get<TaskStatusResponse>(`${BASE}/task/status`);
  },
  getMedicalProcedureCodes() {
    return apiClient.get<MedicalProcedureCodesResponse>(
      `${BASE}/procedure/codes`,
    );
  },
  getMedicalProcedureCosts(code: MedicalProcedureCodesResponse["code"]) {
    return apiClient.get<MedicalProcedureCostsResponse>(
      `${BASE}/procedure/costs/${code}`,
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
    return apiClient.get<unknown[]>(`${BASE}/contact/lawfirm`);
  },

  getContacts() {
    return apiClient.get<unknown[]>(`${BASE}/contact`);
  },

  getAccidentType() {
    return apiClient.get<unknown[]>(`${BASE}/accident/type`);
  },

  getContactTypes() {
    return apiClient.get<unknown[]>(`${BASE}/contact/type`);
  },

  getStates() {
    return apiClient.get<unknown[]>(`${BASE}/states`);
  },

  lookupLiensStatus(id: string) {
    return apiClient.get<unknown[]>(`${BASE}/liens/status`);
  },
};
