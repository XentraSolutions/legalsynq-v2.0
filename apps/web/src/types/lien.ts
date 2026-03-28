// ── Lien status ───────────────────────────────────────────────────────────────

export const LienStatus = {
  Draft:     'Draft',
  Offered:   'Offered',
  Sold:      'Sold',
  Withdrawn: 'Withdrawn',
} as const;
export type LienStatusValue = typeof LienStatus[keyof typeof LienStatus];

// ── Lien type codes ───────────────────────────────────────────────────────────

export const LienType = {
  MedicalLien:           'MedicalLien',
  AttorneyLien:          'AttorneyLien',
  SettlementAdvance:     'SettlementAdvance',
  WorkersCompLien:       'WorkersCompLien',
  PropertyLien:          'PropertyLien',
  Other:                 'Other',
} as const;
export type LienTypeValue = typeof LienType[keyof typeof LienType];

export const LIEN_TYPE_LABELS: Record<string, string> = {
  MedicalLien:       'Medical Lien',
  AttorneyLien:      'Attorney Lien',
  SettlementAdvance: 'Settlement Advance',
  WorkersCompLien:   "Workers' Comp Lien",
  PropertyLien:      'Property Lien',
  Other:             'Other',
};

// ── Subject party snapshot ────────────────────────────────────────────────────

export interface PartySnapshot {
  firstName?: string;
  lastName?:  string;
  caseRef?:   string;
}

// ── Org snapshot ──────────────────────────────────────────────────────────────

export interface OrgSnapshot {
  orgId:   string;
  orgName: string;
}

// ── Lien offer (buyer → seller negotiation) ───────────────────────────────────

export interface LienOfferSummary {
  id:            string;
  lienId:        string;
  buyerOrgId:    string;
  buyerOrgName?: string;
  offerAmount:   number;
  notes?:        string;
  status:        'Pending' | 'Accepted' | 'Rejected' | 'Withdrawn';
  createdAtUtc:  string;
  updatedAtUtc:  string;
}

// ── Status history ────────────────────────────────────────────────────────────

export interface LienStatusHistoryItem {
  status:        string;
  occurredAtUtc: string;
  label:         string;
  actorOrgName?: string;
}

// ── Lien summary (list row) ───────────────────────────────────────────────────

export interface LienSummary {
  id:                   string;
  tenantId:             string;
  lienNumber:           string;
  lienType:             string;
  status:               string;
  originalAmount:       number;
  offerPrice?:          number;
  purchasePrice?:       number;
  jurisdiction?:        string;
  caseRef?:             string;
  isConfidential:       boolean;
  subjectParty?:        PartySnapshot;
  sellingOrg?:          OrgSnapshot;
  buyingOrg?:           OrgSnapshot;
  holdingOrg?:          OrgSnapshot;
  createdAtUtc:         string;
  updatedAtUtc:         string;
}

// ── Lien detail (full record) ─────────────────────────────────────────────────

export interface LienDetail extends LienSummary {
  incidentDate?:       string;      // yyyy-MM-dd
  description?:        string;
  offerExpiresAtUtc?:  string;
  offerNotes?:         string;
  sellingOrgId?:       string;
  buyingOrgId?:        string;
  holdingOrgId?:       string;
  subjectPartyId?:     string;
  offers?:             LienOfferSummary[];
  createdByUserId?:    string;
  updatedByUserId?:    string;
}

// ── Requests ──────────────────────────────────────────────────────────────────

export interface CreateLienRequest {
  lienType:              string;
  originalAmount:        number;
  jurisdiction?:         string;
  caseRef?:              string;
  incidentDate?:         string;    // yyyy-MM-dd
  description?:          string;
  isConfidential:        boolean;
  // Subject party inline snapshot
  subjectFirstName?:     string;
  subjectLastName?:      string;
}

export interface OfferLienRequest {
  offerPrice:     number;
  offerNotes?:    string;
  expiresAtUtc?:  string;  // ISO 8601
}

export interface SubmitLienOfferRequest {
  offerAmount: number;
  notes?:      string;
}

export interface PurchaseLienRequest {
  purchaseAmount: number;
  notes?:         string;
}

// ── Search params ─────────────────────────────────────────────────────────────

export interface LienSearchParams {
  status?:     string;
  lienType?:   string;
  jurisdiction?: string;
  minAmount?:  number;
  maxAmount?:  number;
  page?:       number;
  pageSize?:   number;
}
