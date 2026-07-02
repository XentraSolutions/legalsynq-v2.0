import { apiClient } from '@/lib/api-client'
import {
  CaseReduction,
  CreateLienReductionRequest,
  CreateLienReductionResponse,
  CreateLienSettlementRequest,
  CreateLienSettlementResponse,
  CreateLienSettlementV2Request,
  CreateLienSettlementV2Response,
  CreateSettlementPaymentRequest,
  CreateSettlementPaymentResponse,
  DeletePaymentRequest,
  GetSettlementHistoryResponse,
  LegacyCasePayment,
  LegacySaveReductionRequest,
  SettlementGenericResponse,
  UpdateSettlementRequest,
  UpdateSettlementResponse
} from './settlement.types'

const BASE = '/lien/service'

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
  return pairs.length ? `?${pairs.join('&')}` : ''
}

export const settlementApi = {
  deletePayment(id: DeletePaymentRequest['caseId']) {
    return apiClient.delete<SettlementGenericResponse>(`${BASE}/delete-payment/${id}`)
  },
  createPayment(form: CreateLienSettlementRequest) {
    return apiClient.post<CreateLienSettlementResponse>(`${BASE}/liens/settlement/payment`, form)
  },
  createReduction(form: CreateLienReductionRequest) {
    return apiClient.post<CreateLienReductionResponse>(`${BASE}/liens/update/reduction`, form)
  },
  legacySaveReduction(form: LegacySaveReductionRequest) {
    return apiClient.post<CreateLienReductionResponse>(`${BASE}/liens/update/reduction`, form)
  },
  updateSettlement(form: UpdateSettlementRequest) {
    return apiClient.post<UpdateSettlementResponse>(`${BASE}/liens/update/settlement`, form)
  },
  getSettlementHistory(id: string) {
    return apiClient.get<GetSettlementHistoryResponse>(`${BASE}/settlement/history/${id}`)
  },
  createLienSettlement(form: CreateLienSettlementV2Request) {
    return apiClient.post<CreateLienSettlementV2Response>(`/lien/api/liens/settlement/create`, form)
  },
  createSettlementPayment(form: CreateSettlementPaymentRequest) {
    return apiClient.post<CreateSettlementPaymentResponse>(`/lien/api/liens/settlement/payments`, form)
  },
  getLienPaymentsByCase(caseId: string) {
    return apiClient.get<LegacyCasePayment[]>(`${BASE}/liens/settlement/payment-details/${caseId}`)
  },
  deleteSettlementPayment(id: string) {
    return apiClient.delete<SettlementGenericResponse>(`/lien/api/liens/settlement/payments/${id}`)
  },
  getLienReductionsByCase(caseId: string) {
    return apiClient.get<CaseReduction[]>(`/lien/api/liens/settlement/reductions/case/${caseId}`)
  },
}
