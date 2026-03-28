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

// ── Appointment ───────────────────────────────────────────────────────────────

export const AppointmentStatus = {
  Scheduled:  'Scheduled',
  Confirmed:  'Confirmed',
  Cancelled:  'Cancelled',
  Completed:  'Completed',
  NoShow:     'NoShow',
} as const;
export type AppointmentStatusValue = typeof AppointmentStatus[keyof typeof AppointmentStatus];

/** One bookable time block returned by GET /providers/{id}/availability */
export interface AvailabilitySlot {
  id:              string;
  startUtc:        string;   // ISO-8601
  endUtc:          string;   // ISO-8601
  durationMinutes: number;
  isAvailable:     boolean;
  serviceType?:    string;
  location?:       string;
}

/** Full response for GET /providers/{id}/availability */
export interface ProviderAvailabilityResponse {
  providerId:   string;
  providerName: string;
  from:         string;      // ISO date yyyy-MM-dd
  to:           string;      // ISO date yyyy-MM-dd
  slots:        AvailabilitySlot[];
}

export interface AvailabilitySearchParams {
  from?:        string;      // yyyy-MM-dd
  to?:          string;      // yyyy-MM-dd
  serviceType?: string;
}

/** Row in the appointments list */
export interface AppointmentSummary {
  id:               string;
  referralId?:      string;
  providerId:       string;
  providerName:     string;
  scheduledAtUtc:   string;
  durationMinutes:  number;
  status:           string;
  serviceType?:     string;
  clientFirstName:  string;
  clientLastName:   string;
  caseNumber?:      string;
  createdAtUtc:     string;
  updatedAtUtc:     string;
}

export interface AppointmentStatusHistoryItem {
  status:          string;
  changedAtUtc:    string;
  changedByUserId: string;
  changedByName?:  string;
  notes?:          string;
}

/** Full appointment returned by GET /appointments/{id} */
export interface AppointmentDetail extends AppointmentSummary {
  referringOrganizationId?:   string;
  referringOrganizationName?: string;
  receivingOrganizationId?:   string;
  receivingOrganizationName?: string;
  scheduledEndAtUtc?:         string;
  notes?:                     string;
  location?:                  string;
  clientDob?:                 string;
  clientPhone?:               string;
  clientEmail?:               string;
  statusHistory:              AppointmentStatusHistoryItem[];
}

/** Body for POST /appointments */
export interface CreateAppointmentRequest {
  providerId:       string;
  referralId?:      string;
  slotId?:          string;
  scheduledAtUtc:   string;
  durationMinutes?: number;
  serviceType?:     string;
  notes?:           string;
  clientFirstName:  string;
  clientLastName:   string;
  clientDob?:       string;
  clientPhone?:     string;
  clientEmail?:     string;
  caseNumber?:      string;
}

export interface AppointmentSearchParams {
  status?:     string;
  providerId?: string;
  referralId?: string;
  from?:       string;
  to?:         string;
  page?:       number;
  pageSize?:   number;
}

// ── Pagination ────────────────────────────────────────────────────────────────

/** Matches the backend PagedResponse<T> envelope */
export interface PagedResponse<T> {
  items:      T[];
  page:       number;
  pageSize:   number;
  totalCount: number;
}
