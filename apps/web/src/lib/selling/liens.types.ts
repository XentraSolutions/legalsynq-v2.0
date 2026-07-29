export interface LienResponseDto {
  id: string;
  lienNumber: string;
  externalReference?: string | null;
  lienType: string;
  status: string;
  caseId?: string | null;
  facilityId?: string | null;
  facility: string | null;
  facilityName?: string | null;
  medicalFacility?: string | null;
  plaintiff?: string | null;
  lawFirm?: string | null;
  caseManager?: string | null;
  serviceDate?: string | null;
  purchaseDate?: string | null;
  purchaseAmount?: number | null;
  originalAmount: number;
  currentBalance?: number | null;
  offerPrice?: number | null;
  purchasePrice?: number | null;
  initialServiceDate: string;
  payoffAmount?: number | null;
  jurisdiction?: string | null;
  isConfidential: boolean;
  subjectFirstName?: string | null;
  subjectLastName?: string | null;
  subjectDisplayName?: string | null;
  orgId: string;
  sellingOrgId?: string | null;
  buyingOrgId?: string | null;
  holdingOrgId?: string | null;
  incidentDate?: string | null;
  totalPurchase?: number | null;
  totalBilling?: number | null;
  isServicing?: string | boolean | null;
  description?: string | null;
  openedAtUtc?: string | null;
  closedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface LienOfferResponseDto {
  id: string;
  lienId: string;
  offerAmount: number;
  status: string;
  buyerOrgId: string;
  sellerOrgId: string;
  notes?: string | null;
  responseNotes?: string | null;
  externalReference?: string | null;
  offeredAtUtc: string;
  expiresAtUtc?: string | null;
  respondedAtUtc?: string | null;
  withdrawnAtUtc?: string | null;
  isExpired: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SaleFinalizationResultDto {
  acceptedOfferId: string;
  acceptedOfferStatus: string;
  lienId: string;
  finalLienStatus: string;
  billOfSaleId: string;
  billOfSaleNumber: string;
  billOfSaleStatus: string;
  purchaseAmount: number;
  originalLienAmount: number;
  discountPercent?: number | null;
  documentId?: string | null;
  competingOffersRejected: number;
  finalizedAtUtc: string;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateLienRequestDto {
  lienNumber: string;
  externalReference?: string;
  lienType: string;
  caseId?: string;
  facilityId?: string;
  originalAmount: number;
  jurisdiction?: string;
  isConfidential: boolean;
  subjectFirstName?: string;
  subjectLastName?: string;
  incidentDate?: string;
  description?: string;
}

export interface UpdateLienRequestDto {
  externalReference?: string;
  lienType: string;
  caseId?: string;
  facilityId?: string;
  originalAmount: number;
  jurisdiction?: string;
  isConfidential?: boolean;
  subjectFirstName?: string;
  subjectLastName?: string;
  incidentDate?: string;
  description?: string;
}

export interface CreateLienOfferRequestDto {
  lienId: string;
  offerAmount: number;
  notes?: string;
  expiresAtUtc?: string;
}

export interface ReassignFacilityRequestDto {
  liensId: string;
  facility: string;
}

export interface ReassignContactPersonRequestDto {
  liensId: string;
  facilityContactPerson: string;
}

export interface ReassignFundingCompanyRequestDto {
  liensId: string;
  fundingCompany: string;
}

export interface ReassignMedicalProviderRequestDto {
  liensId: string;
  medicalProvider: string;
}

export interface LiensQuery {
  search?: string;
  status?: string;
  lienType?: string;
  caseId?: string;
  fundingCompanyIds?: string[];
  lienStatusIds?: string[];
  initialServiceDate?: string;
  page?: number;
  pageSize?: number;
  tab?: string;
  initialServiceDateFrom?: string;
  initialServiceDateTo?: string;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
}

// mapLienToListItem only carries over the subset of LienResponseDto that the
// liens list and case-liens views actually read. The backend response has
// more fields than this (see LienResponseDto) — originalAmount, currentBalance,
// offerPrice, purchasePrice, etc. are real DTO fields, deliberately left out
// here because nothing on this list-item path consumes them. A lien can
// bundle multiple medical billing line items, so purchaseAmount/totalBilling
// are the server-aggregated sums across those items, not a single line's
// price — that's the only "amount" shape this view needs. Add a field here
// only once something actually reads it; don't mirror the DTO 1:1.
export interface LienListItem {
  id: string;
  lienId: string;
  lienNumber: string;
  fundingCompany: string;
  initialServiceDate: string;
  billingAmount: string;
  askAmount: string;
  highestBid: string;
  status: string;
}

export interface AgingListItem {
  id: string;
  lienSeller: string;
  status: string;
  total: string;
  pastDue: string;
  [key: string]: string;
}

export interface LienDetail {
  id: string;
  lienNumber: string;
  externalReference: string;
  lienType: string;
  lienTypeLabel: string;
  status: string;
  caseId: string;
  originalAmount: number;
  currentBalance: number | null;
  offerPrice: number | null;
  purchasePrice: number | null;
  payoffAmount: number | null;
  jurisdiction: string;
  isConfidential: boolean;
  subjectName: string;
  subjectFirstName: string;
  subjectLastName: string;
  orgId: string;
  sellingOrgId: string;
  buyingOrgId: string;
  holdingOrgId: string;
  incidentDate: string;
  description: string;
  openedAt: string;
  closedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface LienOfferItem {
  id: string;
  lienId: string;
  offerAmount: number;
  status: string;
  buyerOrgId: string;
  sellerOrgId: string;
  notes: string;
  responseNotes: string;
  offeredAt: string;
  expiresAt: string;
  respondedAt: string;
  isExpired: boolean;
  createdAt: string;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
