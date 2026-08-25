import { apiClient } from "@/lib/api-client";
import type {
  LienPaymentListResponse,
  LienPaymentQuery,
  RecordLienPaymentRequest,
  RecordLienPaymentResponse,
  VoidLienPaymentResponse,
} from "./lien-payments.types";

// Lien Selling's own client for the buyer-payment endpoint (payments made
// against a sold lien), kept independent from Synq Liens' settlement.api.ts
// even though both currently call the same case-scoped backend route (a
// case backs exactly one lien in the lien-selling flow).
export const lienPaymentsApi = {
  getLienPayments(caseId: string, query: LienPaymentQuery = {}) {
    const params = new URLSearchParams();
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== "") params.set(key, String(value));
    });
    const suffix = params.size > 0 ? `?${params.toString()}` : "";
    return apiClient.get<LienPaymentListResponse>(
      `/lien/api/liens/cases/${caseId}/payments${suffix}`,
    );
  },

  recordLienPayment(caseId: string, form: RecordLienPaymentRequest) {
    return apiClient.post<RecordLienPaymentResponse>(
      `/lien/api/liens/cases/${caseId}/payments`,
      form,
      { "Idempotency-Key": crypto.randomUUID() },
    );
  },

  voidLienPayment(caseId: string, paymentId: string, reason: string) {
    return apiClient.post<VoidLienPaymentResponse>(
      `/lien/api/liens/cases/${caseId}/payments/${paymentId}/void`,
      { reason },
      { "Idempotency-Key": crypto.randomUUID() },
    );
  },
};
