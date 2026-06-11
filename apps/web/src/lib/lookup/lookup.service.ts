import { lookupApi } from './lookup.api';
import type {
  ContactsByIdResponse,
  DocumentTypeResponse,
  LookupResponse,
  MedicalProcedureCodesResponse,
  MedicalProcedureCostsResponse,
  TaskStatusResponse,
  UserListResponse,
} from './lookup.types';

export const lookupService = {
  async getDocumentType(): Promise<DocumentTypeResponse> {
    const { data } = await lookupApi.getDocumentType()
    return data
  },
  async getTaskStatus(): Promise<TaskStatusResponse> {
    const { data } = await lookupApi.getTaskStatus()
    return data
  },
  async getMedicalProcedureCodes(): Promise<MedicalProcedureCodesResponse> {
    const { data } = await lookupApi.getMedicalProcedureCodes()
    return data
  },
  async getMedicalProcedureCosts(code: MedicalProcedureCodesResponse['code']): Promise<MedicalProcedureCostsResponse> {
    const { data } = await lookupApi.getMedicalProcedureCosts(code)
    return data
  },
  async getLookupAll(): Promise<LookupResponse> {
    const { data } = await lookupApi.getLookupAll()
    return data
  },
  async getContactsById(id: string): Promise<ContactsByIdResponse> {
    const { data } = await lookupApi.getContactsById(id)
    return data
  },
  async getUserList(): Promise<UserListResponse> {
    const { data } = await lookupApi.getUserList()
    return data
  }
}