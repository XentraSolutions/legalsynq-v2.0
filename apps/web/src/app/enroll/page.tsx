import { fetchEnrollmentPrefill, decodeEnrollmentToken, fetchExistingEnrollmentPrefill, checkPortalAccessStatus } from './actions';
import { EnrollmentForm }                               from './enrollment-form';
import { buildCareConnectPortalLoginUrl }               from '@/lib/careconnect-login-url';
import { getServerSession }                             from '@/lib/session';
import { OrgType, ProductRole }                         from '@/types';

export const dynamic = 'force-dynamic';

function coalesceName(primary?: string, fallback?: string): string {
  return primary?.trim() || fallback?.trim() || '';
}

interface AuthenticatedOrgPrefill {
  companyName: string;
  companyType: string;
  email:       string;
  phone:       string;
}

interface SearchParams {
  id?:       string;  // provider enrollment (not token-protected — tenantId scoped by HMAC prefill)
  tenantId?: string;  // provider enrollment
  token?:    string;  // firm/referral enrollment — HMAC-signed enrollment token (replaces raw PII params)
}

interface PageProps {
  searchParams: Promise<SearchParams>;
}

export default async function EnrollPage({ searchParams }: PageProps) {
  const { id: providerId, tenantId, token } = await searchParams;
  const session = await getServerSession();

  let prefill = null;
  let providerAlreadyEnrolled = false;

  if (providerId && tenantId) {
    try {
      const prefillResult = await fetchEnrollmentPrefill(providerId, tenantId);
      prefill = prefillResult.data;
      providerAlreadyEnrolled = prefillResult.status === 'already_enrolled';
    } catch {
      // prefill stays null — form shows empty
    }
  }

  // Firm/referral flow: decode the signed enrollment token to get prefill context.
  // The token is generated server-side and is HMAC-signed — no raw PII in the URL.
  const claims = token ? await decodeEnrollmentToken(token) : null;

  // Resolve the effective tenantId:
  // — provider flow: from URL (scoped by the HMAC-authenticated prefill call above)
  // — firm/referral flow: from the decoded, signature-verified token
  const effectiveTenantId = claims?.tenantId ?? tenantId ?? null;

  const existingEnrollmentPrefill =
    claims?.email && effectiveTenantId
      ? await fetchExistingEnrollmentPrefill(effectiveTenantId, claims.email)
      : null;

  // Referral prefill from decoded token only (no raw URL params accepted for PII).
  // Prefer the split contactFirstName/contactLastName claims (no full-name slicing
  // ambiguity); fall back to splitting the legacy single `contact` claim only for
  // tokens issued before the split existed.
  let refFirst = (claims?.contactFirstName ?? '').trim();
  let refLast  = (claims?.contactLastName ?? '').trim();
  if (!refFirst && !refLast && claims?.contact) {
    const parts = claims.contact.trim().split(/\s+/).filter(Boolean);
    refFirst = parts[0] ?? '';
    refLast  = parts.slice(1).join(' ');
  }
  const referralPrefill = claims ? (
    existingEnrollmentPrefill
      ? {
          companyName:  existingEnrollmentPrefill.companyName,
          email:        existingEnrollmentPrefill.email,
          phone:        existingEnrollmentPrefill.phone,
          firstName:    coalesceName(existingEnrollmentPrefill.firstName, refFirst),
          lastName:     coalesceName(existingEnrollmentPrefill.lastName, refLast),
          addressLine1: existingEnrollmentPrefill.addressLine1,
          city:         existingEnrollmentPrefill.city,
          state:        existingEnrollmentPrefill.state,
          postalCode:   existingEnrollmentPrefill.postalCode,
        }
      : {
          companyName:  claims.firm  ?? '',
          email:        claims.email ?? '',
          phone:        claims.phone ?? '',
          firstName:    refFirst,
          lastName:     refLast,
          addressLine1: '',
          city:         '',
          state:        '',
          postalCode:   '',
        }
  ) : null;

  // Firm enrollment: no providerId + valid token with a tenantId = firm/referral path.
  // Never derived from a user-controlled URL param.
  const isFirmEnrollment = !providerId && !!effectiveTenantId;

  const hasCareConnectRole = !!session && (
    session.productRoles.includes(ProductRole.CareConnectReferrer) ||
    session.productRoles.includes(ProductRole.CareConnectReceiver)
  );

  const isExternalOrg = session?.orgType === OrgType.LawFirm || session?.orgType === OrgType.Provider;

  const authenticatedOrgPrefill: AuthenticatedOrgPrefill | null =
    session?.orgId && hasCareConnectRole && isExternalOrg
      ? {
          companyName: session.orgName ?? '',
          companyType: session.orgType === OrgType.Provider ? 'Provider' : 'Law Firm',
          email:       session.email,
          phone:       session.phone ?? '',
        }
      : null;

  // Check if the user from the firm/referral token is already actively enrolled.
  const firmAlreadyEnrolled =
    isFirmEnrollment && claims?.email && effectiveTenantId
      ? (await checkPortalAccessStatus(effectiveTenantId, claims.email)) === 'active_in_tenant'
      : false;

  const alreadyEnrolled = providerAlreadyEnrolled || firmAlreadyEnrolled;

  if (alreadyEnrolled) {
    const portalLoginUrl = buildCareConnectPortalLoginUrl(process.env.CC_COMMON_PORTAL_HOSTNAME);
    return (
      <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 flex items-center justify-center">
        <div className="max-w-md mx-auto px-4 py-12 text-center">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-blue-100 mb-4">
            <i className="ri-shield-check-line text-2xl text-blue-600" />
          </div>
          <h1 className="text-2xl font-bold text-gray-900">Account Already Created</h1>
          <p className="mt-2 text-gray-500">
            An account with this email address has already been set up.
            You can sign in to access your portal.
          </p>
          <a
            href={portalLoginUrl}
            className="inline-block mt-6 px-6 py-3 rounded-xl bg-blue-600 text-white font-semibold text-sm hover:bg-blue-700 transition-colors"
          >
            Sign In to Your Portal
          </a>
        </div>
      </main>
    );
  }

  // This is a token-gated enrollment form — not a self-serve signup page.
  // The provider flow only counts as valid if the backend actually found a matching
  // provider/tenant prefill record — id+tenantId alone are unauthenticated user input.
  const hasValidAccess = !!(providerId && tenantId && prefill) || !!claims || !!authenticatedOrgPrefill;
  if (!hasValidAccess) {
    return (
      <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 flex items-center justify-center">
        <div className="max-w-md mx-auto px-4 py-12 text-center">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-red-100 mb-4">
            <i className="ri-error-warning-line text-2xl text-red-600" />
          </div>
          <h1 className="text-2xl font-bold text-gray-900">Invalid or Expired Link</h1>
          <p className="mt-2 text-gray-500">
            This enrollment link is invalid or has expired. Please request a new link or sign in to your account.
          </p>
          <a href="/login" className="inline-block mt-6 text-blue-600 hover:underline">Sign in</a>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50">
      <div className="max-w-2xl mx-auto px-4 py-12">

        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-blue-100 mb-4">
            <i className="ri-shield-check-line text-2xl text-blue-600" />
          </div>
          <h1 className="text-3xl font-bold text-gray-900">Get Full Portal Access</h1>
          <p className="mt-2 text-gray-500 max-w-md mx-auto">
            Set up your CareConnect account to manage referrals, appointments, and
            communications — all in one place.
          </p>
        </div>

        <EnrollmentForm
          authenticatedOrgPrefill={authenticatedOrgPrefill}
          prefill={prefill}
          providerId={providerId ?? null}
          tenantId={effectiveTenantId}
          referralPrefill={referralPrefill}
          isFirmEnrollment={isFirmEnrollment}
        />

        <p className="text-center text-xs text-gray-400 mt-6">
          Already have an account?{' '}
          <a href="/login" className="text-blue-600 hover:underline">Sign in</a>
        </p>
      </div>
    </main>
  );
}
