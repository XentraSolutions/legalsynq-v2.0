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

export interface UpdateLiensStatusRequest {
  caseId: string
  lienIds: string
  lienStatus: string
  closedDate: string
  note: string
}

export interface UpdateLiensStatusResponse extends SettlementGenericResponse {}

export interface DeletePaymentRequest {
  caseId: string
  paymentId: string
}
export interface LegacySaveReductionRequest {
  caseId: string
  data: {
    liensId: string,
    reductionAmount: number
  }[]
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

export interface CreateLienSettlementV2Request {
  lienId: string
  caseId: string
  settlementAmount: number
  settlementDate: string
  notes: string
}

export interface CreateLienSettlementV2Response extends SettlementGenericResponse {}

export interface CreateSettlementPaymentRequest {
  lienId: string
  caseId: string
  amount: number // received payment
  paymentDate: string
  paymentMethod: string
  referenceNumber: string
  notes: string

  // missing from the api
  settlementType: string
  settlementStatus: string
}

export interface CreateSettlementPaymentResponse extends SettlementGenericResponse {}

export interface CasePayment {
  id: string
  tenantId: string
  caseId: string
  lienId: string
  paymentNumber: number
  amount: number
  paymentDate: string | null
  payee: string | null
  checkNumber: string | null
  note: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

/** Payment record returned by the legacy settlement payments endpoint */
export interface LegacyCasePayment {
  id?: string
  caseId: string
  lienId: string
  lienCode?: string | null
  lienStatus?: string | null
  lienStatusId?: string | null
  amount: string | number
  amountToSettle?: string | number | null
  checkAmount?: string | number | null
  checkDate?: string | null
  checkNumber?: string | null
  typeId?: string | null
  type?: string | null
  statusId?: string | null
  status?: string | null
  payor?: string | null
  netProfit?: string | number | null
  note?: string | null
  paymentNumber?: string | number | null
  date?: string | null
}

export interface CaseReduction {
  id: string
  tenantId: string
  caseId: string
  lienId: string
  reductionDate: string
  amount: number
  note: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type SettlementHistoryItemType = 'payment' | 'reduction' | 'settlement'

interface SettlementHistoryItemBase {
  id: string
  type: SettlementHistoryItemType
  lienId: string
  amount: number
  note: string
  createdAt: string
}

export interface SettlementHistoryPaymentItem extends SettlementHistoryItemBase {
  type: 'payment'
  paymentNumber: number
  payee: string
  checkNumber: string
}

export interface SettlementHistoryReductionItem extends SettlementHistoryItemBase {
  type: 'reduction'
  date: string
}

export interface SettlementHistorySettlementItem extends SettlementHistoryItemBase {
  type: 'settlement'
  paymentNumber: number
  status: string
}

export type SettlementHistoryItemV3 =
  | SettlementHistoryPaymentItem
  | SettlementHistoryReductionItem
  | SettlementHistorySettlementItem

export interface GetSettlementHistoryV3Response {
  isSuccess: boolean
  message: string
  data: SettlementHistoryItemV3[]
  totalCount: number
  page: number
  limit: number
}

export interface SettlementHistoryV3Query {
  caseId: string
  page?: number
  limit?: number
}