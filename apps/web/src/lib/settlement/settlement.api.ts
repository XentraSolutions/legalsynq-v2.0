import { apiClient } from '@/lib/api-client'
import {
  CasePayment,
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
  GetSettlementHistoryV3Response,
  GetSettlementPaymentDetailsResponse,
  LegacyCasePayment,
  LegacySaveReductionRequest,
  SettlementGenericResponse,
  SettlementHistoryV3Query,
  UpdateLiensStatusRequest,
  UpdateLiensStatusResponse,
  UpdateSettlementRequest,
  UpdateSettlementResponse
} from './settlement.types'

const BASE = '/lien/service'

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
  updateLiensStatus(form: UpdateLiensStatusRequest) {
    return apiClient.post<UpdateLiensStatusResponse>(`${BASE}/update-liens-status`, form)
  },
  getSettlementHistory(id: string) {
    return apiClient.get<GetSettlementHistoryResponse>(`${BASE}/settlement/history/${id}`)
  },
  getSettlementHistoryV3({ caseId, page = 1, limit = 10 }: SettlementHistoryV3Query) {
    return apiClient.post<GetSettlementHistoryV3Response>(`${BASE}/settlement/history/v3`, { caseId, page, limit })
  },
  createLienSettlement(form: CreateLienSettlementV2Request) {
    return apiClient.post<CreateLienSettlementV2Response>(`/lien/api/liens/settlement/create`, form)
  },
  createSettlementPayment(form: CreateSettlementPaymentRequest) {
    return apiClient.post<CreateSettlementPaymentResponse>(`/lien/api/liens/settlement/payments`, form)
  },
  getLienPaymentsByCase(caseId: string) {
    return apiClient.get<CasePayment[]>(`/lien/api/liens/settlement/payments/case/${caseId}`)
  },
  deleteSettlementPayment(id: string) {
    return apiClient.delete<SettlementGenericResponse>(`/lien/api/liens/settlement/payments/${id}`)
  },
  getLienReductionsByCase(caseId: string) {
    return apiClient.get<CaseReduction[]>(`/lien/api/liens/settlement/reductions/case/${caseId}`)
  },
  getSettlementPaymentDetails(caseId: string) {
    return apiClient.get<GetSettlementPaymentDetailsResponse>(`${BASE}/liens/settlement/payment-details/${caseId}`)
  },
}
