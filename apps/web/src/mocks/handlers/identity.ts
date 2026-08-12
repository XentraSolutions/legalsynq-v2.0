import { http, HttpResponse } from "msw";

/**
 * Fixed session identity used by every mocked-suite spec that needs an
 * authenticated platform page — orgId/userProducts are required for
 * requireOrg()/requireProductAccess(SynqLien) to pass (see
 * src/lib/auth-guards.ts, src/lib/session.ts).
 */
export const MOCK_SESSION = {
  userId: "user-mock-001",
  email: "mock.user@example.com",
  tenantId: "tenant-mock-001",
  tenantCode: "mocktenant",
  orgId: "org-mock-001",
  orgType: "LAW_FIRM",
  orgName: "Mock Law Firm",
  productRoles: ["SYNQ_LIENS:SYNQLIEN_SELLER"],
  systemRoles: ["TenantAdmin"],
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  sessionTimeoutMinutes: 60,
  enabledProducts: ["SynqLien"],
  userProducts: ["SynqLien"],
};

export const identityHandlers = [
  // Backs both the server-side session guards (requireOrg(), called from
  // every (platform) layout) and the client-side SessionProvider's
  // background refresh — see src/lib/session.ts and
  // src/providers/session-provider.tsx.
  http.get("*/identity/api/auth/me", () => {
    return HttpResponse.json(MOCK_SESSION);
  }),

  // Mirrors what e2e/mocked/mock-identity-server.mjs used to hand-roll —
  // accepts any invite token and returns a deterministic tenant portal URL.
  http.post("*/identity/api/auth/accept-invite", async () => {
    return HttpResponse.json({
      message: "Invitation accepted. Your account is now active.",
      tenantPortalUrl: "https://acmefirm.portal.example.com",
    });
  }),
];
