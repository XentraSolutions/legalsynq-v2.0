export interface LookupGenericResponse {
  id: string;
  name: string;
}

export interface DocumentTypeResponse extends LookupGenericResponse {}

export interface TaskStatusResponse extends LookupGenericResponse {}

export interface AccidentTypeResponse extends LookupGenericResponse {}

export interface MedicalProcedureCodesResponse extends LookupGenericResponse {
  description: string;
  code: string;
}

export interface MedicalProcedureCostsResponse {
  facilityType: "asc" | "desc";
  total: string;
}

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
  LiensStatus: LookupData[] | [];
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
