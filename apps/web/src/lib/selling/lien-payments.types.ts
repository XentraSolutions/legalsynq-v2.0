// Lien Selling's own payment types — for payments the *buyer* makes to
// purchase a lien (proceeds of a sale), not Synq Liens' settlement/case
// payments (money recovered on a lien and owed back to the funder). The two
// concepts happen to share a backend endpoint today, but are kept as
// separate types here (deliberately not importing from src/lib/settlement)
// so either can change shape independently.

export interface LienPaymentQuery {
  search?: string;
  paymentMethod?: string;
  postingStatus?: string;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface LienPaymentSummary {
  lienSellingAmount: number;
  totalPaid: number;
  remainingBalance: number;
  overpaidAmount: number;
  lienAgingDays: number | null;
  currency: string;
}

export interface LienPaymentItem {
  id: string;
  receiptId: string | null;
  lienId: string;
  lienNumber: string;
  paymentNumber: number;
  paymentDate: string | null;
  paymentMethod: string;
  referenceNumber: string | null;
  amount: number;
  detailsContext: string | null;
  notes: string | null;
  settlementType: string | null;
  settlementStatus: string | null;
  postingStatus: "Posted" | "Voided" | string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface LienPaymentListResponse {
  summary: LienPaymentSummary;
  items: LienPaymentItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RecordLienPaymentRequest {
  amount: number;
  paymentDate: string;
  paymentMethod: string;
  referenceNumber: string;
  detailsContext?: string;
  notes?: string;
  settlementType?: string;
  settlementStatus?: string;
  lienStatus?: string;
  allocations: Array<{ lienId: string; amount: number }>;
}

export interface RecordLienPaymentResponse {
  receiptId: string;
  paymentNumber: number;
  amount: number;
  allocations: LienPaymentItem[];
}

export interface VoidLienPaymentResponse {
  receiptId: string | null;
  paymentId: string;
  voidedAllocations: number;
  postingStatus: string;
}
