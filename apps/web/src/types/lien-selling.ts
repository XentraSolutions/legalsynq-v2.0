export interface LienDetail {
  lienNumber: string;
  sellerStatus: string;
  status: string;
  initialServiceDate: string | null;
  endServiceDate: string | null;
  listingVisibility: string;
  notes: string | null;
  buyerMessage: string | null;
}

export interface LienFundingCompanyContact {
  id: string;
  name: string;
}

export interface LienFundingCompanyDetail {
  id: string;
  name: string;
  contact: LienFundingCompanyContact | null;
}

export interface LienCaseDetail {
  id: string;
  caseNumber: string;
  title: string | null;
  caseManagerId: string | null;
  caseManagerName: string | null;
  lawFirmId: string | null;
  lawFirm: string | null;
}

export interface MedicalPricingRowDetail {
  id: string;
  description: string;
  notes: string | null;
  createdAtUtc: string;
}

// Parsed out of MedicalPricingRowDetail.notes, which the API stores as a
// JSON blob rather than dedicated columns (see SaveMedicalPricing in
// SellingV2Endpoints.cs).
export interface MedicalPricingRowData {
  medicalCode: string;
  description: string | null;
  serviceDate: string | null;
  billingAmount: number;
  medicareCost: number;
  targetSaleAmount: number;
}

export interface SellingDocumentReference {
  id: string;
  description: string;
  notes: string | null;
}

// Parsed out of SellingDocumentReference.notes (JSON blob, see SaveDocuments
// in SellingV2Endpoints.cs).
export interface SellingDocumentReferenceData {
  documentId: string;
  documentType: string;
  displayName: string | null;
}

export interface SaleReadiness {
  ready: boolean;
  missing: string[];
}

export interface LienDetailsResult {
  lienId: string;
  lienInformation: LienDetail;
  caseInformation: LienCaseDetail | null;
  fundingCompany: LienFundingCompanyDetail | null;
  medicalPricing: {
    askAmount: number;
    billingAmount: number;
    rows: MedicalPricingRowDetail[];
  };
  documents: SellingDocumentReference[];
  saleReadiness: SaleReadiness;
  buyerOfferSummary: {
    count: number;
    highestBidAmount: number;
  };
  activity: unknown[];
  availableActions: string[];
}
export interface LienStatusHistoryItem {
  status: string;
  occurredAtUtc: string;
  label: string;
  actorOrgName?: string;
}
