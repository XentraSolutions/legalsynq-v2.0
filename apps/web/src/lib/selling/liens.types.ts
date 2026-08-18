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
  lienId: string;
  lienNumber: string;
  fundingCompany: string;
  initialServiceDate: string;
  billingAmount: number;
  askAmount: number;
  highestBid: string;
  status: string;
  sellerStatus: string;
  availableActions: string[];

  caseId?: string;
  caseNumber?: string;
  fundingCompanyId?: string;
  lawFirmId?: string;
  lawFirm?: string;
  caseManagerId?: null | string;
  caseManager?: null | string;
  facilityId?: null | string;
  facility?: null | string;
  highestBidAmount?: 0;
  purchasePrice?: null;
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

// Mirrors Liens.Application/DTOs/SellingV2Dtos.cs on origin/sellingV2 — the
// backend actually deployed behind /api/liens/selling (this branch's local
// apps/services/liens checkout doesn't have it; see the plan doc).
export interface SaveSellingLienInformationRequest {
  sellerStatus: string;
  initialServiceDate?: string;
  endServiceDate?: string;
  listingVisibility?: string;
  notes?: string;
}

export interface SaveSellingCaseInformationRequest {
  medicalProviderId?: string;
  fundingCompanyId?: string;
  fundingCompanyContactId?: string;
  handlingLawFirmId?: string;
  caseManagerId?: string;
  caseId?: string;
  createCaseIfMissing?: boolean;
}

export interface SellingMedicalPricingRowRequest {
  medicalCode: string;
  description?: string;
  serviceDate?: string;
  billingAmount: number;
  medicareCost: number;
  targetSaleAmount: number;
}

export interface SaveSellingMedicalPricingRequest {
  askAmount?: number;
  billingAmount?: number;
  rows: SellingMedicalPricingRowRequest[];
}

export interface SellingDocumentReferenceRequest {
  documentId: string;
  documentType: string;
  displayName?: string;
}

export interface SaveSellingDocumentsRequest {
  documents: SellingDocumentReferenceRequest[];
}

export interface PrepareSellingLienRequest {
  buyerFundingCompanyId: string;
  buyerContactId?: string;
  askAmount?: number;
  listingVisibility?: string;
  messageToBuyer?: string;
}

export interface ConfirmSellingLienSaleRequest {
  confirmationAccepted: boolean;
  sendBuyerNotification: boolean;
}

export interface WithdrawSellingLienRequest {
  reason?: string;
}

export interface ArchiveSellingLienRequest {
  reason?: string;
}

export interface LienArchivedStatusResult {
  lienId: string;
  lienNumber: string;
  isArchived: boolean;
  sellerStatus: string;
  archivedAtUtc: string | null;
  archivedReason: string | null;
}

export interface SubmitSellingLienRequest {
  sellerStatus: string;
  listingVisibility: string;
}

// Shape returned by GET {lienId}/activity (SellingV2Endpoints.GetLienActivity) —
// distinct field names from the `activity` array embedded in GetLienDetail
// (LienActivityItem), which uses changedByUserId/changedAtUtc instead.
export interface LienActivityFeedItem {
  id: string;
  eventType: string;
  description: string;
  actorUserId: string;
  timestampUtc: string;
}

export interface LienActivityFeedResult {
  lienId: string;
  items: LienActivityFeedItem[];
}

// Shape returned by GET /bulk-imports/{id} (SellingV2Endpoints.GetBulkImport / MapBulkImport).
export interface BulkImportSummary {
  importId: string;
  status: string;
  fileName?: string | null;
  createdAtUtc: string;
  createdByUserId?: string | null;
  updatedAtUtc?: string | null;
}

// Row status returned by GET /bulk-imports/{id}/rows (row-level validation state).
export type BulkImportRowStatus = "PENDING" | "VALID" | "INVALID";

// dataJson is a JSON-serialized Dictionary<string,string> of the CSV columns
// for that row (e.g. "Case Code*", "Funding Company", "Billing Amount*", ...) —
// see SellingBulkImportTemplateColumns in SellingEndpoints.cs.
export interface BulkImportRowItem {
  id: string;
  rowNumber: number;
  status: BulkImportRowStatus;
  reason?: string | null;
  dataJson: string;
}

export interface BulkImportRowsResult {
  importId: string;
  page: number;
  pageSize: number;
  totalCount: number;
  items: BulkImportRowItem[];
}
