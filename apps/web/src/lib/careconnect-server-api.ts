import { serverApi } from '@/lib/server-api-client';
import type {
  ProviderSummary,
  ProviderDetail,
  ProviderMarker,
  ProviderSearchParams,
  ProviderAvailabilityResponse,
  AvailabilitySearchParams,
  ReferralSummary,
  ReferralDetail,
  ActivationRequestSummary,
  ActivationRequestDetail,
  ReferralSearchParams,
  AppointmentSummary,
  AppointmentDetail,
  AppointmentSearchParams,
  PagedResponse,
  ActivationFunnelMetrics,
  ProviderReadinessDiagnostics,
  ProvisionCareConnectResult,
  ProviderActivationResult,
  DashboardMetrics,
  BlockedProviderLogPage,
  AdminReferralPage,
} from '@/types/careconnect';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions ONLY.
// Reads the platform_session cookie and calls the gateway directly (no extra hop).
// DO NOT import this in Client Components — use careconnect-api.ts instead.

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

  // LSCC-009: Admin activation queue (server-side only — requires admin session)
  adminActivations: {
    getPending: () =>
      serverApi.get<{ items: ActivationRequestSummary[]; count: number }>(
        `/careconnect/api/admin/activations`,
      ),

    getById: (id: string) =>
      serverApi.get<ActivationRequestDetail>(
        `/careconnect/api/admin/activations/${id}`,
      ),
  },

  // LSCC-011: Activation funnel analytics (admin-only)
  analytics: {
    getFunnel: (params: { days?: number; startDate?: string; endDate?: string } = {}) =>
      serverApi.get<ActivationFunnelMetrics>(
        `/careconnect/api/admin/analytics/funnel${toQs(params as Record<string, unknown>)}`,
      ),
  },

  // LSCC-01-003: Admin CareConnect receiver provisioning (server-side only)
  adminProvisioning: {
    // GET /api/admin/users/{userId}/careconnect-readiness  (Identity service)
    getReadiness: (userId: string) =>
      serverApi.get<ProviderReadinessDiagnostics>(
        `/identity/api/admin/users/${userId}/careconnect-readiness`,
      ),

    // POST /api/admin/users/{userId}/provision-careconnect  (Identity service)
    provision: (userId: string) =>
      serverApi.post<ProvisionCareConnectResult>(
        `/identity/api/admin/users/${userId}/provision-careconnect`,
        {},
      ),

    // POST /api/admin/providers/{providerId}/activate-for-careconnect  (CareConnect service)
    activateProvider: (providerId: string) =>
      serverApi.post<ProviderActivationResult>(
        `/careconnect/api/admin/providers/${providerId}/activate-for-careconnect`,
        {},
      ),
  },

  // LSCC-01-004: Admin dashboard, blocked-provider queue, referral monitor (server-side only)
  adminDashboard: {
    // GET /api/admin/dashboard — aggregate operational metrics
    getMetrics: () =>
      serverApi.get<DashboardMetrics>(`/careconnect/api/admin/dashboard`),

    // GET /api/admin/providers/blocked — paged blocked-access log
    getBlockedProviders: (params: { page?: number; pageSize?: number; since?: string } = {}) =>
      serverApi.get<BlockedProviderLogPage>(
        `/careconnect/api/admin/providers/blocked${toQs(params as Record<string, unknown>)}`,
      ),

    // GET /api/admin/referrals — cross-tenant referral monitor
    getReferrals: (params: {
      page?:     number;
      pageSize?: number;
      status?:   string;
      tenantId?: string;
      since?:    string;
    } = {}) =>
      serverApi.get<AdminReferralPage>(
        `/careconnect/api/admin/referrals${toQs(params as Record<string, unknown>)}`,
      ),
  },
};
