// ── Provider ──────────────────────────────────────────────────────────────────

export interface ProviderSummary {
  id:                 string;
  name:               string;
  organizationName?:  string;
  email:              string;
  phone:              string;
  city:               string;
  state:              string;
  postalCode:         string;
  isActive:           boolean;
  acceptingReferrals: boolean;
  categories:         string[];
  primaryCategory?:   string;
  displayLabel:       string;
  markerSubtitle:     string;
  hasGeoLocation:     boolean;
  latitude?:          number;
  longitude?:         number;
}

// ProviderDetail — same DTO as list (backend returns same shape for both)
export type ProviderDetail = ProviderSummary;

export interface ProviderSearchParams {
  name?:               string;
  categoryCode?:       string;
  city?:               string;
  state?:              string;
  acceptingReferrals?: boolean;
  isActive?:           boolean;
  page?:               number;
  pageSize?:           number;
}

// ── Referral ──────────────────────────────────────────────────────────────────

export const ReferralStatus = {
  Pending:   'Pending',
  Accepted:  'Accepted',
  Declined:  'Declined',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
} as const;
export type ReferralStatusValue = typeof ReferralStatus[keyof typeof ReferralStatus];

export const ReferralUrgency = {
  Routine:   'Routine',
  Urgent:    'Urgent',
  Emergency: 'Emergency',
} as const;
export type ReferralUrgencyValue = typeof ReferralUrgency[keyof typeof ReferralUrgency];

export interface ReferralSummary {
  id:               string;
  tenantId:         string;
  providerId:       string;
  providerName:     string;
  clientFirstName:  string;
  clientLastName:   string;
  clientDob?:       string;
  clientPhone:      string;
  clientEmail:      string;
  caseNumber?:      string;
  requestedService: string;
  urgency:          string;
  status:           string;
  notes?:           string;
  createdAtUtc:     string;
  updatedAtUtc:     string;
}

// ReferralDetail — same shape as summary for Phase 1
export type ReferralDetail = ReferralSummary;

export interface CreateReferralRequest {
  providerId:       string;
  clientFirstName:  string;
  clientLastName:   string;
  clientDob?:       string;
  clientPhone:      string;
  clientEmail:      string;
  caseNumber?:      string;
  requestedService: string;
  urgency:          string;
  notes?:           string;
}

export interface ReferralSearchParams {
  status?:      string;
  providerId?:  string;
  clientName?:  string;
  caseNumber?:  string;
  urgency?:     string;
  createdFrom?: string;
  createdTo?:   string;
  page?:        number;
  pageSize?:    number;
}

// ── Pagination ────────────────────────────────────────────────────────────────

/** Matches the backend PagedResponse<T> envelope */
export interface PagedResponse<T> {
  items:      T[];
  page:       number;
  pageSize:   number;
  totalCount: number;
}
