import { apiClient } from '@/lib/api-client';
import type {
  ProviderSummary,
  ProviderDetail,
  ProviderMarker,
  ProviderSearchParams,
  ProviderAvailabilityResponse,
  AvailabilitySearchParams,
  ReferralSummary,
  ReferralDetail,
  ReferralHistoryItem,
  ReferralNotification,
  CreateReferralRequest,
  ReferralSearchParams,
  AppointmentSummary,
  AppointmentDetail,
  CreateAppointmentRequest,
  AppointmentSearchParams,
  PagedResponse,
} from '@/types/careconnect';

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Converts a params object to a query string, dropping undefined/empty values */
function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Client-side API ───────────────────────────────────────────────────────────
// Use in Client Components (forms, interactive UI).
// Calls /api/careconnect/* which routes through the BFF proxy → gateway.

export const careConnectApi = {
  providers: {
    search: (params: ProviderSearchParams = {}) =>
      apiClient.get<PagedResponse<ProviderSummary>>(
        `/careconnect/api/providers${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<ProviderDetail>(`/careconnect/api/providers/${id}`),

    getMarkers: (params: ProviderSearchParams = {}) =>
      apiClient.get<ProviderMarker[]>(
        `/careconnect/api/providers/map${toQs(params as Record<string, unknown>)}`,
      ),

    getAvailability: (id: string, params: AvailabilitySearchParams = {}) =>
      apiClient.get<ProviderAvailabilityResponse>(
        `/careconnect/api/providers/${id}/availability${toQs(params as Record<string, unknown>)}`,
      ),
  },

  referrals: {
    create: (body: CreateReferralRequest) =>
      apiClient.post<ReferralDetail>('/careconnect/api/referrals', body),

    search: (params: ReferralSearchParams = {}) =>
      apiClient.get<PagedResponse<ReferralSummary>>(
        `/careconnect/api/referrals${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<ReferralDetail>(`/careconnect/api/referrals/${id}`),

    /** PUT /api/referrals/{id} — update status (Accept / Decline / Cancel / etc.) */
    update: (id: string, body: { requestedService: string; urgency: string; status: string; notes?: string }) =>
      apiClient.put<ReferralDetail>(`/careconnect/api/referrals/${id}`, body),

    /** GET /api/referrals/{id}/history — status change audit log */
    getHistory: (id: string) =>
      apiClient.get<ReferralHistoryItem[]>(`/careconnect/api/referrals/${id}/history`),

    /**
     * POST /api/referrals/{id}/accept-by-token — PUBLIC (no auth).
     * Accepts a referral using a secure HMAC view token.
     */
    acceptByToken: (id: string, token: string) =>
      apiClient.post<void>(`/careconnect/api/referrals/${id}/accept-by-token`, { token }),

    // LSCC-005-01: hardening endpoints

    /** GET /api/referrals/{id}/notifications — email delivery history */
    getNotifications: (id: string) =>
      apiClient.get<ReferralNotification[]>(`/careconnect/api/referrals/${id}/notifications`),

    /** POST /api/referrals/{id}/resend-email — re-send provider notification */
    resendEmail: (id: string) =>
      apiClient.post<ReferralDetail>(`/careconnect/api/referrals/${id}/resend-email`, {}),

    /** POST /api/referrals/{id}/revoke-token — revoke all existing view tokens */
    revokeToken: (id: string) =>
      apiClient.post<ReferralDetail>(`/careconnect/api/referrals/${id}/revoke-token`, {}),
  },

  appointments: {
    create: (body: CreateAppointmentRequest) =>
      apiClient.post<AppointmentDetail>('/careconnect/api/appointments', body),

    search: (params: AppointmentSearchParams = {}) =>
      apiClient.get<PagedResponse<AppointmentSummary>>(
        `/careconnect/api/appointments${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      apiClient.get<AppointmentDetail>(`/careconnect/api/appointments/${id}`),

    /** POST /api/appointments/{id}/cancel */
    cancel: (id: string, body: { notes?: string } = {}) =>
      apiClient.post<AppointmentDetail>(`/careconnect/api/appointments/${id}/cancel`, body),

    /** PUT /api/appointments/{id} — update status (Confirm, NoShow, etc.) */
    update: (id: string, body: { status: string; notes?: string }) =>
      apiClient.put<AppointmentDetail>(`/careconnect/api/appointments/${id}`, body),

    /** POST /api/appointments/{id}/reschedule */
    reschedule: (id: string, body: { newAppointmentSlotId: string; notes?: string }) =>
      apiClient.post<AppointmentDetail>(`/careconnect/api/appointments/${id}/reschedule`, body),
  },
};
