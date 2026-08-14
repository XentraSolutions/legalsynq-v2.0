import type { PagedResult } from '@/shared/types/api';

export interface BillOfSaleQueryParams {
  search?: string;
  status?: string;
  lienId?: string;
  sellerOrgId?: string;
  buyerOrgId?: string;
  page?: number;
  pageSize?: number;
}

export interface BillOfSale {
  id: string;
  billOfSaleNumber: string;
  externalReference?: string | null;
  status: string;
  lienId: string;
  lienOfferId: string;
  sellerOrgId: string;
  buyerOrgId: string;
  purchaseAmount: number;
  originalLienAmount: number;
  discountPercent?: number | null;
  sellerContactName?: string | null;
  buyerContactName?: string | null;
  terms?: string | null;
  notes?: string | null;
  documentId?: string | null;
  issuedAtUtc: string;
  executedAtUtc?: string | null;
  effectiveAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export type BillOfSaleListResult = Omit<PagedResult<BillOfSale>, 'totalPages'> & {
  totalPages?: number;
};
