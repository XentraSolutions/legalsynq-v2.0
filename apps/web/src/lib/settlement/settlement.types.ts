export interface SettlementGenericResponse {
  message: string
}

export interface LienReductionData {
  liensId: string
  reductionAmount: string
}

export interface CreateLienReductionRequest {
  caseId: string
  data: LienReductionData[]
}

export interface CreateLienReductionResponse extends SettlementGenericResponse {}

export interface CreateLienSettlementRequest {
  caseId: string
  payment: string[]
}

export interface CreateLienSettlementResponse extends SettlementGenericResponse {}

export interface UpdateSettlementRequest {
  caseId: string,
  payments: string[]
}

export interface UpdateSettlementResponse extends SettlementGenericResponse {}

export interface DeletePaymentRequest {
  caseId: string
  paymentId: string
}

export interface GetSettlementHistoryResponse {
  date: string,
  description: string,
  user: string
}