import { CaseStatusResponse } from "../cases/cases.types";
import { lookupApi } from "./lookup.api";
import type {
  AccidentTypeResponse,
  ContactsByIdResponse,
  DocumentTypeResponse,
  LawFirmListResponse,
  LiensStatusResponse,
  LookupData,
  LookupGenericResponse,
  LookupResponse,
  MedicalProcedureCodesResponse,
  MedicalProcedureCostsResponse,
  MedicalProvidersResponse,
  TaskStatusResponse,
  UserListResponse,
} from "./lookup.types";

export const lookupService = {
  async getDocumentType(): Promise<DocumentTypeResponse[]> {
    const { data } = await lookupApi.getDocumentType();
    return data;
  },
  async getTaskStatus(): Promise<TaskStatusResponse> {
    const { data } = await lookupApi.getTaskStatus();
    return data;
  },
  async getMedicalProcedureCodes(): Promise<{
    data: MedicalProcedureCodesResponse[];
  }> {
    const { data } = await lookupApi.getMedicalProcedureCodes();
    return { data: data.data };
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

  async getCaseStatus(): Promise<{ items: CaseStatusResponse[] }> {
    const { data } = await lookupApi.getCaseStatus();
    return {
      items: data,
    };
  },
  async getLawfirm(): Promise<{ items: LawFirmListResponse[] }> {
    const { data } = await lookupApi.getLawfirm();
    return {
      items: data,
    };
  },

  async getContacts(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getContacts();
    return {
      items: data,
    };
  },

  async getAccidentType(): Promise<{ items: AccidentTypeResponse[] }> {
    const { data } = await lookupApi.getAccidentType();
    return {
      items: data,
    };
  },

  async getContactTypes(): Promise<{ items: LookupData[] }> {
    const { data } = await lookupApi.getContactTypes();
    return {
      items: data,
    };
  },

  async getStates(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getStates();
    return {
      items: data,
    };
  },

  async getLiensStatus(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getLiensStatus();
    return {
      items: data,
    };
  },

  async getCaseManagers(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getCaseManagers();
    return {
      items: data,
    };
  },

  async getCaseManagersByLawfirm(
    id: string,
  ): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getCaseManagersByLawfirm(id);
    return {
      items: data,
    };
  },

  async getLawfirmRoles(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getLawfirmRoles();
    return {
      items: data,
    };
  },
  async getMedicalFacility(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getMedicalFacility();
    return {
      items: data,
    };
  },

  async getMedicalProviders(): Promise<{ items: MedicalProvidersResponse[] }> {
    const { data } = await lookupApi.getMedicalProviders();
    return {
      items: data,
    };
  },

  async getFundingCompany(): Promise<{ items: LookupGenericResponse[] }> {
    const { data } = await lookupApi.getFundingCompany();
    return {
      items: data,
    };
  },

  async getSettlementStatus(): Promise<{ items: LookupData[] }> {
    const { data } = await lookupApi.getSettlementStatus();
    return { items: data };
  },

  async getSettlementType(): Promise<{ items: LookupData[] }> {
    const { data } = await lookupApi.getSettlementType();
    return { items: data };
  },
};
