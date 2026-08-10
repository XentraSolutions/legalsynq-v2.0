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
  facilityId?: string;
  lawFirmIds?: string[];
  medicalFacilityIds?: string[];
  caseManagerIds?: string[];
  lienStatusIds?: string[];
  purchaseDateFrom?: string;
  purchaseDateTo?: string;
  closedDateFrom?: string;
  closedDateTo?: string;
  page?: number;
  pageSize?: number;
  // TODO: ListLiens (Liens.Api/Endpoints/LienEndpoints.cs) currently only
  // accepts search/status/lienType/caseId/facilityId/page/pageSize — the
  // fields below match the filter shape ReportTemplate already uses for
  // DIY Reports (lien-report.types.ts) and are sent assuming the backend
  // will be extended to accept them on this endpoint too. Until then they
  // are silently ignored server-side. Revisit once that lands.
  initialServiceDateFrom?: string;
  initialServiceDateTo?: string;
  // Same situation as the filter fields above — not yet in ListLiens'
  // documented parameter list, sent on the assumption the backend will
  // recognize them once wired up. sortBy is expected to be a LienResponse
  // field name (see the SORT_BY_MAP comment in liens/page.tsx).
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
  lienNumber: string;
  lienType: string;
  lienTypeLabel: string;
  status: string;
  facility: string | null;
  facilityId: string | null;
  facilityName: string | null;
  plaintiff: string | null;
  lawFirm: string | null;
  caseManager: string | null;
  caseId: string;
  initialServiceDate: string;
  purchaseDate: string;
  purchaseAmount: number | null;
  totalBilling: number | null;
  closedAtUtc: string | null;
  isServicing: boolean;
  jurisdiction: string;
  isConfidential: boolean;
  subjectName: string;
  createdAt: string;
  updatedAt: string;
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

// Maps to CreateSellingLienRequest / the anonymous response object in
// SellingV2Endpoints.CreateLien (apps/services/liens/Liens.Api/Endpoints/
// SellingV2Endpoints.cs) — undocumented in any OpenAPI spec, so this is the
// source of truth for the contract. sellerStatus must be "Pending" or
// "Internal" (NormalizeIntakeStatus rejects anything else, e.g. "Sold" or
// "Draft") or the API 400s.
export interface CreateLienParams {
  sellerStatus: string;
  source?: string;
}
export interface CreateLienResult {
  lienId: string;
  lienNumber: string;
  sellerStatus: string;
}
export interface LienInfoParams {
  sellerStatus: string;
  initialServiceDate: string;
  endServiceDate: string | null;
  listingVisibility: string;
  notes: string;
}

export interface LienFundingCompanyParams {
  fundingCompanyId: string;
  fundingCompanyContactId: string;
  handlingLawFirmId: string;
  caseManagerId: string;
  caseId: string;
  createCaseIfMissing: boolean;
}

export interface LienMedicalCodesParams {
  askAmount: number;
  billingAmount: number;
  rows: [
    {
      medicalCode: string;
      description: string;
      serviceDate: string;
      billingAmount: number;
      medicareCost: number;
      targetSaleAmount: number;
    },
  ];
}

export interface LienUploadDocumentsParams {
  documents: [
    {
      documentId: string;
      documentType: string;
      displayName: string;
    },
  ];
}
