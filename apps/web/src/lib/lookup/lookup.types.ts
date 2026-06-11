export interface LookupGenericResponse {
  id: string
  name: string
}

export interface DocumentTypeResponse extends LookupGenericResponse {}

export interface TaskStatusResponse extends LookupGenericResponse {}

export interface MedicalProcedureCodesResponse extends LookupGenericResponse {
  description: string
  code: string
}

export interface MedicalProcedureCostsResponse {
  facilityType: 'asc' | 'desc'
  total: string
}

export interface LookupData {
  category: string
  code: string
  description: null | string
  id: string
  isActive: boolean
  isSystem: boolean
  name: string
  sortOrder: number
}

export interface LookupResponse {
  states: LookupData | []
  liensStatus: LookupData | []
  accidentType: LookupData | []
  contactType: LookupData | []
  caseStatus: LookupData | []
  medicalStatus: LookupData | []
  currentAttr: LookupData | []
  settlementStatus: LookupData | []
  settlementType: LookupData | []
  taskPriority: LookupData | []
  contactLawfirmRole: LookupData | []
}

export interface ContactsByIdResponse extends LookupGenericResponse {}

export interface UserListResponse {
  userId: string
  firstName: string
  lastName: string
}