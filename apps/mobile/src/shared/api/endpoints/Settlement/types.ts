export interface LienSettlement {
  id: string;
  tenantId: string;
  caseId: string;
  lienId: string;
  paymentNumber: number;
  amount: number;
  status: string;
  note?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface LienReduction {
  id: string;
  tenantId: string;
  caseId: string;
  lienId: string;
  reductionDate: string;
  amount: number;
  note?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SettlementPaymentDetail {
  id: string;
  tenantId: string;
  caseId: string;
  lienId: string;
  paymentNumber: number;
  amount: number;
  paymentDate?: string | null;
  payee?: string | null;
  checkNumber?: string | null;
  note?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
