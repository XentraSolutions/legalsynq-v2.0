import { lookupApi } from "./lookup.api";
import type {
  ContactsByIdResponse,
  LookupResponse,
  MedicalProcedureCodesResponse,
  MedicalProcedureCostsResponse,
  TaskStatusResponse,
  UserListResponse,
} from "./lookup.types";

export const lookupService = {
  async getDocumentType(): Promise<string[]> {
    const { data } = await lookupApi.getDocumentType();
    return data.map(type => type.name);
  },
  async getTaskStatus(): Promise<TaskStatusResponse> {
    const { data } = await lookupApi.getTaskStatus();
    return data;
  },
  async getMedicalProcedureCodes(): Promise<MedicalProcedureCodesResponse> {
    const { data } = await lookupApi.getMedicalProcedureCodes();
    return data;
  },
  async getMedicalProcedureCosts(
    code: MedicalProcedureCodesResponse["code"],
  ): Promise<MedicalProcedureCostsResponse> {
    const { data } = await lookupApi.getMedicalProcedureCosts(code);
    return data;
  },
  async getLookupAll(): Promise<LookupResponse> {
    const { data } = await lookupApi.getLookupAll();
    return data;
  },
  async getContactsById(id: string): Promise<ContactsByIdResponse> {
    const { data } = await lookupApi.getContactsById(id);
    return data;
  },
  async getUserList(): Promise<UserListResponse> {
    const { data } = await lookupApi.getUserList();
    return data;
  },

  async getCaseStatus(): Promise<{ items: unknown }> {
    const { data } = await lookupApi.getCaseStatus();
    return {
      items: data,
    };
  },
  async getLawfirm(): Promise<{ items: unknown }> {
    const { data } = await lookupApi.getLawfirm();
    return {
      items: data,
    };
  },

  async getContacts(): Promise<{ items: unknown }> {
    const { data } = await lookupApi.getContacts();
    return {
      items: data,
    };
  },

  async getAccidentType(): Promise<{ items: unknown }> {
    const { data } = await lookupApi.getAccidentType();
    return {
      items: data,
    };
  },

  async getContactTypes(): Promise<{ items: unknown }> {
    const { data } = await lookupApi.getContactTypes();
    return {
      items: data,
    };
  },

  async getStates(): Promise<{ items: unknown }> {
    const { data } = await lookupApi.getStates();
    return {
      items: data,
    };
  },
};
