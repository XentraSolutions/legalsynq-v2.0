import type { CreateSettlementPaymentRequest } from "./settlement.types";

export interface SettlementPaymentFormSelection {
  lienId: string;
  caseId: string;
  amount: number;
  paymentDate: string;
  paymentMethod: string;
  referenceNumber: string;
  notes: string;
  type: string;
  status: string;
  lienStatus: string;
}

/**
 * Keeps the three similarly named payment fields distinct when translating the
 * form to the API contract. Settlement type is who settled the lien, settlement
 * status is the payment outcome, and lien status is the linked lien lifecycle.
 */
export function buildSettlementPaymentRequest(
  selection: SettlementPaymentFormSelection,
): CreateSettlementPaymentRequest {
  return {
    lienId: selection.lienId,
    caseId: selection.caseId,
    amount: selection.amount,
    paymentDate: selection.paymentDate,
    paymentMethod: selection.paymentMethod,
    referenceNumber: selection.referenceNumber,
    notes: selection.notes,
    settlementType: selection.type,
    settlementStatus: selection.status,
    lienStatus: selection.lienStatus,
  };
}
