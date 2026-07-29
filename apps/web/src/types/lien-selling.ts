export interface LienDetail {
  lienNumber: string;
  sellerStatus: string;
  status: string;
  initialServiceDate: string;
  endServiceDate: string;
  listingVisibility: string;
  notes: string | null;
  buyerMessage: string | null;
}

export interface LienFundingCompanyDetail {
  fundingCompany: string | null;
  contactPerson: string | null;
  email: string;
}
export interface LienCaseDetail {
  id: string;
  caseNumber: string;
  lawfirm: string;
  caseManager: string;
  title: string | null;
}
export interface MedicalCodeDetail {
  id: string;
  askAmount: number;
  billingAmount: number;
  code: string;
}
export interface LienDetailsResult {
  lienId: string;
  lienInformation: LienDetail;
  caseInformation: LienCaseDetail;
  fundingCompany: string | null;
  contactPerson: string;
  email: string;
  medicalPricing: {
    askAmount: string | null;
    billingAmount: number;
    rows: MedicalCodeDetail[];
  };
  documents: [];
  saleReadiness: {};
  buyerOfferSummary: {
    count: number;
    highestBidAmount: number;
  };
  activity: [];
  availableActions: ["prepare-sale", "archive"];
}
export interface LienStatusHistoryItem {
  status: string;
  occurredAtUtc: string;
  label: string;
  actorOrgName?: string;
}
