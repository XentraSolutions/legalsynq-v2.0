export interface LienDetail {
  lienNumber: string;
  sellerStatus: string;
  status: string;
  purchaseDate: string | null;
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
  contactPerson: string | null; // currently seem to be the same as LienFundingCompanyContact['name']
  emailAddress: string | null; // old ui that ask for extra email field
  contact: LienFundingCompanyContact | null;
}

export interface LienMedicalProviderDetail {
  id: string | null;
  name: string;
}

export interface LienFacilityDetail {
  id: string | null;
  name: string;
  emailAddress: string | null;
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

export interface LienActivityItem {
  id: string;
  description: string;
  changedByUserId: string;
  changedAtUtc: string;
}

export interface LienDetailsResult {
  lienId: string;
  lienInformation: LienDetail;
  caseInformation: LienCaseDetail | null;
  fundingCompany: LienFundingCompanyDetail | null;
  facility: LienFacilityDetail | null;
  medicalProvider: LienMedicalProviderDetail | null;
  medicalPricing: {
    askAmount: number | null;
    billingAmount: number;
    rows: MedicalPricingRowDetail[];
  };
  documents: SellingDocumentReference[];
  saleReadiness: SaleReadiness;
  buyerOfferSummary: {
    count: number;
    highestBidAmount: number;
  };
  activity: LienActivityItem[];
  availableActions: string[];
}
export interface LienStatusHistoryItem {
  status: string;
  occurredAtUtc: string;
  label: string;
  actorOrgName?: string;
}
