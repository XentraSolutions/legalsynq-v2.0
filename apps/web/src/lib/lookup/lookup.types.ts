import { ContactResponseDto } from "../contacts/contacts.types";

export interface LookupGenericResponse {
  id: string;
  name: string;
}

export interface GenericPaginationData {
  keyword?: string;
  search?: string;
  page?: number;
  limit?: number;
  sortBy?: string;
  sortDirection?: string;
}

export interface GenericPaginatedResult<T> {
  data: T[];
  limit: number;
  page: number;
  totalCount: number;
  message?: string;
  isSuccess?: boolean;
  pageSize?: number;
}

export interface PaginatedResultWithItems<T> {
  items: T[];
  pagination: PaginationMeta;
}
export interface PaginationMeta {
  page: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface DocumentTypeResponse extends LookupGenericResponse {
  category: string;
  code: string;
  description: string;
  id: string;
  isActive: boolean;
  isSystem: boolean;
  name: string;
  sortOrder: number;
}

export interface LiensStatusResponse extends LookupGenericResponse {
  category: string;
  code: string;
  description: string;
  id: string;
  isActive: boolean;
  isSystem: boolean;
  name: string;
  sortOrder: number;
}

export interface TaskStatusResponse extends LookupGenericResponse {}

export interface AccidentTypeResponse extends LookupGenericResponse {}

export interface MedicalProcedureCodesResponse extends LookupGenericResponse {
  description: string;
  code: string;
  data?: Array<Record<string, unknown>>;
}

export interface MedicalProcedureCostsResponse {
  data: Array<Record<string, unknown>>;
}

export interface MedicalProvidersResponse
  extends LookupGenericResponse, ContactResponseDto {}

export interface LookupData {
  category: string;
  code: string;
  description: null | string;
  id: string;
  isActive: boolean;
  isSystem: boolean;
  name: string;
  sortOrder: number;
}

export interface LookupResponse {
  AccidentType: LookupData[] | [];
  CaseStatus: LookupData[] | [];
  ContactType: LookupData[] | [];
  CurrentAttributes: LookupData[] | [];
  DocumentCategory: LookupData[] | [];
  LienStatus: LookupData[] | [];
  LienType: LookupData[] | [];
  MedicalStatus: LookupData[] | [];
  ProcedureCode: LookupData[] | [];
  ServicingPriority: LookupData[] | [];
  ServicingStatus: LookupData[] | [];
  SettlementStatus: LookupData[] | [];
  SettlementType: LookupData[] | [];
  State: LookupData[] | [];
}

export interface ContactsByIdResponse extends LookupGenericResponse {}

export interface UserListResponse {
  userId: string;
  firstName: string;
  lastName: string;
}

export interface LawFirmListResponse {
  addressLine1: string | null;
  city: string;
  contactType: string;
  createdAtUtc: string;
  displayName: string;
  email: string;
  fax: string | null;
  firstName: string;
  id: string;
  isActive: boolean;
  lastName: string;
  notes: string | null;
  organization: string;
  phone: string;
  postalCode: string | null;
  state: string;
  title: string | null;
  updatedAtUtc: string;
  website: string | null;
}

export type DropdownOption = {
  key: string;
  value: string;
  label: string;
};
