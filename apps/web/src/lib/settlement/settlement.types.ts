export interface SettlementGenericResponse {
  message: string
}

export interface CreateLienReductionRequest {
  caseId: string
  lienId: string
  reductionDate: string
  amount: number
  note: string
}

export interface CreateLienReductionResponse extends SettlementGenericResponse {}

export interface CreateLienSettlementRequest {
  amount: string
  amountToSettle: string
  checkAmount: string
  checkDate: string
  checkNumber: string
  closedDate: string
  lienId: string
  lienStatus: string
  netProfit: string
  note: string
  paymentNumber: string
  payor: string
  status: string
  type: string
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

interface HistoryData {
  id: string
  tenantId: string
  caseId: string
  lienId: string
  paymentNumber: number
  amount: number
  status: string
  date: string
  note: string
  user: string
}

export interface GetSettlementHistoryResponse {
  settlements: HistoryData[]
  reductions: HistoryData[]
  payments: HistoryData[]
}