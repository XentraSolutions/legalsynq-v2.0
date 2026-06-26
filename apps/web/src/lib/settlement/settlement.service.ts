import { settlementApi } from './settlement.api';
import type { CreateLienReductionRequest, CreateLienReductionResponse, CreateLienSettlementRequest, CreateLienSettlementResponse, CreateLienSettlementV2Request, CreateLienSettlementV2Response, CreateSettlementPaymentRequest, CreateSettlementPaymentResponse, DeletePaymentRequest, GetSettlementHistoryResponse, SettlementGenericResponse, UpdateSettlementRequest, UpdateSettlementResponse } from './settlement.types';

export const settlementService = {
  async deletePayment(id: DeletePaymentRequest['caseId']): Promise<SettlementGenericResponse> {
    const { data } = await settlementApi.deletePayment(id)
    return data
  },
  async createPayment(form: CreateLienSettlementRequest): Promise<CreateLienSettlementResponse> {
    const { data } = await settlementApi.createPayment(form)
    return data
  },
  async createReduction(form: CreateLienReductionRequest): Promise<CreateLienReductionResponse> {
    const { data } = await settlementApi.createReduction(form)
    return data
  },
  async createReductions(forms: CreateLienReductionRequest[]): Promise<CreateLienReductionResponse[]> {
    return Promise.all(forms.map((form) => this.createReduction(form)))
  },
  async updateSettlement(form: UpdateSettlementRequest): Promise<UpdateSettlementResponse> {
    const { data } = await settlementApi.updateSettlement(form)
    return data
  },
  async getSettlementHistory(id: string): Promise<GetSettlementHistoryResponse> {
    const { data } = await settlementApi.getSettlementHistory(id)
    return data
  },
  async createLienSettlement(form: CreateLienSettlementV2Request): Promise<CreateLienSettlementV2Response> {
    const { data } = await settlementApi.createLienSettlement(form)
    return data
  },
  async createSettlementPayment(form: CreateSettlementPaymentRequest): Promise<CreateSettlementPaymentResponse> {
    const { data } = await settlementApi.createSettlementPayment(form)
    return data
  },
}