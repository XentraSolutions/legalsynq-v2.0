import { settlementApi } from "./settlement.api";
import type {
  CasePayment,
  CasePaymentListResponse,
  CasePaymentQuery,
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
  RecordCasePaymentRequest,
  RecordCasePaymentResponse,
  SettlementGenericResponse,
  SettlementHistoryItemV3,
  SettlementHistoryV3Query,
  UpdateLienSettlementRequest,
  UpdateLiensStatusRequest,
  UpdateLiensStatusResponse,
  UpdateSettlementRequest,
  UpdateSettlementResponse,
  VoidCasePaymentResponse,
} from "./settlement.types";

export interface SettlementHistoryV3Result {
  items: SettlementHistoryItemV3[];
  pagination: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
}

export const settlementService = {
  async getCasePayments(
    caseId: string,
    query: CasePaymentQuery = {},
  ): Promise<CasePaymentListResponse> {
    const { data } = await settlementApi.getCasePayments(caseId, query);
    return data;
  },
  async recordCasePayment(
    caseId: string,
    form: RecordCasePaymentRequest,
  ): Promise<RecordCasePaymentResponse> {
    const { data } = await settlementApi.recordCasePayment(caseId, form);
    return data;
  },
  async voidCasePayment(
    caseId: string,
    paymentId: string,
    reason: string,
  ): Promise<VoidCasePaymentResponse> {
    const { data } = await settlementApi.voidCasePayment(caseId, paymentId, reason);
    return data;
  },
  async deletePayment(
    id: DeletePaymentRequest["caseId"],
  ): Promise<SettlementGenericResponse> {
    const { data } = await settlementApi.deletePayment(id);
    return data;
  },
  async createPayment(
    form: CreateLienSettlementRequest,
  ): Promise<CreateLienSettlementResponse> {
    const { data } = await settlementApi.createPayment(form);
    return data;
  },
  async createReduction(
    form: CreateLienReductionRequest,
  ): Promise<CreateLienReductionResponse> {
    const { data } = await settlementApi.createReduction(form);
    return data;
  },
  async createReductions(
    forms: CreateLienReductionRequest[],
  ): Promise<CreateLienReductionResponse[]> {
    return Promise.all(forms.map((form) => this.createReduction(form)));
  },
  async updateSettlement(
    form: CreateSettlementPaymentRequest | UpdateSettlementRequest,
  ): Promise<UpdateSettlementResponse> {
    const { data } = await settlementApi.updateSettlement(form);
    return data;
  },
  async updateLiensStatus(
    form: UpdateLiensStatusRequest,
  ): Promise<UpdateLiensStatusResponse> {
    const { data } = await settlementApi.updateLiensStatus(form);
    return data;
  },
  async getSettlementHistory(
    id: string,
  ): Promise<GetSettlementHistoryResponse> {
    const { data } = await settlementApi.getSettlementHistory(id);
    return data;
  },
  async getSettlementHistoryV3(
    query: SettlementHistoryV3Query,
  ): Promise<SettlementHistoryV3Result> {
    const { data } = await settlementApi.getSettlementHistoryV3(query);
    const limit = data.limit || query.limit || 10;
    return {
      items: data.data,
      pagination: {
        page: data.page,
        pageSize: limit,
        totalCount: data.totalCount,
        totalPages: Math.max(1, Math.ceil(data.totalCount / limit)),
      },
    };
  },
  async createLienSettlement(
    form: CreateLienSettlementV2Request,
  ): Promise<CreateLienSettlementV2Response> {
    const { data } = await settlementApi.createLienSettlement(form);
    return data;
  },
  async createSettlementPayment(
    form: CreateSettlementPaymentRequest,
  ): Promise<CreateSettlementPaymentResponse> {
    const { data } = await settlementApi.createSettlementPayment(form);
    return data;
  },

  async updateSettlementPayment(
    id: string,
    form: UpdateLienSettlementRequest,
  ): Promise<UpdateLienSettlementRequest> {
    const { data } = await settlementApi.updateSettlementPayment(id, form);
    return data;
  },


  async legacySaveReduction(
    form: LegacySaveReductionRequest,
  ): Promise<CreateLienReductionResponse> {
    const { data } = await settlementApi.legacySaveReduction(form);
    return data;
  },
  async getLienPaymentsByCase(caseId: string): Promise<CasePayment[]> {
    const { data } = await settlementApi.getLienPaymentsByCase(caseId);
    return Array.isArray(data) ? data : [];
  },
  async deleteSettlementPayment(
    id: string,
  ): Promise<SettlementGenericResponse> {
    const { data } = await settlementApi.deleteSettlementPayment(id);
    return data;
  },
  async getLienReductionsByCase(caseId: string): Promise<CaseReduction[]> {
    const { data } = await settlementApi.getLienReductionsByCase(caseId);
    return data;
  },
  async getSettlementPaymentDetails(
    caseId: string,
  ): Promise<LegacyCasePayment[]> {
    const { data } = await settlementApi.getSettlementPaymentDetails(caseId);
    return Array.isArray(data?.data) ? data.data : [];
  },
};
