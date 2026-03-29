import { serverApi } from '@/lib/server-api-client';
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

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions.
// Reads the platform_session cookie and calls the gateway directly (no extra hop).
// DO NOT import this in Client Components — it calls Next.js server-only APIs.

export const careConnectServerApi = {
  providers: {
    search: (params: ProviderSearchParams = {}) =>
      serverApi.get<PagedResponse<ProviderSummary>>(
        `/careconnect/api/providers${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      serverApi.get<ProviderDetail>(`/careconnect/api/providers/${id}`),

    getMarkers: (params: ProviderSearchParams = {}) =>
      serverApi.get<ProviderMarker[]>(
        `/careconnect/api/providers/map${toQs(params as Record<string, unknown>)}`,
      ),

    getAvailability: (id: string, params: AvailabilitySearchParams = {}) =>
      serverApi.get<ProviderAvailabilityResponse>(
        `/careconnect/api/providers/${id}/availability${toQs(params as Record<string, unknown>)}`,
      ),
  },

  referrals: {
    search: (params: ReferralSearchParams = {}) =>
      serverApi.get<PagedResponse<ReferralSummary>>(
        `/careconnect/api/referrals${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      serverApi.get<ReferralDetail>(`/careconnect/api/referrals/${id}`),
  },

  appointments: {
    search: (params: AppointmentSearchParams = {}) =>
      serverApi.get<PagedResponse<AppointmentSummary>>(
        `/careconnect/api/appointments${toQs(params as Record<string, unknown>)}`,
      ),

    getById: (id: string) =>
      serverApi.get<AppointmentDetail>(`/careconnect/api/appointments/${id}`),
  },
};

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
  },
};
