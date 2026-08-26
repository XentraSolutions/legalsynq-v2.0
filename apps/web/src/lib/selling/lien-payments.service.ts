import { lienPaymentsApi } from "./lien-payments.api";
import type {
  LienPaymentListResponse,
  LienPaymentQuery,
  RecordLienPaymentRequest,
  RecordLienPaymentResponse,
  VoidLienPaymentResponse,
} from "./lien-payments.types";

export const lienPaymentsService = {
  async getLienPayments(
    caseId: string,
    query: LienPaymentQuery = {},
  ): Promise<LienPaymentListResponse> {
    const { data } = await lienPaymentsApi.getLienPayments(caseId, query);
    return data;
  },
  async recordLienPayment(
    caseId: string,
    form: RecordLienPaymentRequest,
  ): Promise<RecordLienPaymentResponse> {
    const { data } = await lienPaymentsApi.recordLienPayment(caseId, form);
    return data;
  },
  async voidLienPayment(
    caseId: string,
    paymentId: string,
    reason: string,
  ): Promise<VoidLienPaymentResponse> {
    const { data } = await lienPaymentsApi.voidLienPayment(
      caseId,
      paymentId,
      reason,
    );
    return data;
  },
};
