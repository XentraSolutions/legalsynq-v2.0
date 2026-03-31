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
  latitude?:           number;
  longitude?:          number;
  radiusMiles?:        number;
  northLat?:           number;
  southLat?:           number;
  eastLng?:            number;
  westLng?:            number;
}

export interface ProviderMarker {
  id:                 string;
  name:               string;
  organizationName?:  string;
  displayLabel:       string;
  markerSubtitle:     string;
  city:               string;
  state:              string;
  addressLine1:       string;
  postalCode:         string;
  email:              string;
  phone:              string;
  acceptingReferrals: boolean;
  isActive:           boolean;
  latitude:           number;
  longitude:          number;
  geoPointSource?:    string;
  primaryCategory?:   string;
  categories:         string[];
}

// ── Referral history ─────────────────────────────────────────────────────────

export interface ReferralHistoryItem {
  id:              string;
  referralId:      string;
  oldStatus:       string;
  newStatus:       string;
  changedByUserId?: string;
  changedAtUtc:    string;
  notes?:          string;
}

// ── Referral ──────────────────────────────────────────────────────────────────

export const ReferralStatus = {
  New:       'New',
  Received:  'Received',
  Contacted: 'Contacted',
  Scheduled: 'Scheduled',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
} as const;
export type ReferralStatusValue = typeof ReferralStatus[keyof typeof ReferralStatus];

export const ReferralUrgency = {
  Low:       'Low',
  Normal:    'Normal',
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
  // LSCC-005-01: org context
  referringOrganizationId?: string;
  receivingOrganizationId?:  string;
  organizationRelationshipId?: string;
}

// LSCC-005-01 / LSCC-005-02: notification delivery record
export interface ReferralNotification {
  id:                string;
  notificationType:  string;
  recipientType:     string;
  recipientAddress?: string;
  status:            string;
  attemptCount:      number;
  failureReason?:    string;
  sentAtUtc?:        string;
  failedAtUtc?:      string;
  lastAttemptAtUtc?: string;
  createdAtUtc:      string;
  // LSCC-005-02: retry lifecycle fields
  /** How the notification was triggered: Initial | AutoRetry | ManualResend */
  triggerSource:      string;
  /** ISO 8601 UTC: when the next auto-retry is scheduled. Null if sent or exhausted. */
  nextRetryAfterUtc?: string;
  /** UI-friendly derived status: Pending | Sent | Failed | Retrying | RetryExhausted */
  derivedStatus:      string;
}

// LSCC-005-02: audit timeline event (status history + notification events merged)
export interface ReferralAuditEvent {
  /** Machine-readable event type, e.g. referral.status.accepted */
  eventType:   string;
  /** Human-readable label, e.g. "Provider Notification — Sent" */
  label:       string;
  /** ISO 8601 UTC timestamp */
  occurredAt:  string;
  /** Optional short context detail */
  detail?:     string;
  /** UI colour category: info | success | warning | error | security */
  category:    string;
}

// ReferralDetail — extends summary with hardening fields
export interface ReferralDetail extends ReferralSummary {
  // LSCC-005-01: token versioning + email delivery status
  tokenVersion?:              number;
  providerEmailStatus?:       string;
  providerEmailAttempts?:     number;
  providerEmailFailureReason?: string;
}

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
  /** LSCC-005: referrer identity for the notification email */
  referrerEmail?:   string;
  referrerName?:    string;
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

// ── LSCC-009: Admin Activation Queue ─────────────────────────────────────────

export interface ActivationRequestSummary {
  id:               string;
  providerName:     string;
  providerEmail:    string;
  requesterName:    string | null;
  requesterEmail:   string | null;
  clientName:       string | null;
  referringFirmName: string | null;
  requestedService: string | null;
  referralId:       string;
  providerId:       string;
  status:           string;
  createdAtUtc:     string;
}

export interface ActivationRequestDetail {
  id:                     string;
  tenantId:               string;
  referralId:             string;
  providerId:             string;
  providerName:           string;
  providerEmail:          string;
  providerPhone:          string | null;
  providerAddress:        string | null;
  providerOrganizationId: string | null;
  requesterName:          string | null;
  requesterEmail:         string | null;
  clientName:             string | null;
  referringFirmName:      string | null;
  requestedService:       string | null;
  referralStatus:         string;
  status:                 string;
  approvedByUserId:       string | null;
  approvedAtUtc:          string | null;
  linkedOrganizationId:   string | null;
  createdAtUtc:           string;
  isAlreadyActive:        boolean;
}
