/**
 * LSCC-008: Provider activation intent capture page.
 *
 * Reached by clicking "Activate & Accept Referral" on the activation landing page.
 * Server component — fetches referral context, renders the activation form (client).
 *
 * Flow:
 *   /referrals/activate?referralId=...&token=...
 *     → validates token via the same public thread endpoint used by the referral page
 *     → shows referral context + ActivationForm (email + name capture)
 *     → on submit: emits ActivationStarted funnel event, shows confirmation
 *
 * This page remains valid after a referral has already been accepted. Providers
 * may still need to activate a portal account so they can log in and manage the
 * accepted referral from the dashboard.
 *
 * Deferred step (documented in LSCC-008 report):
 *   Automated tenant provisioning is not yet implemented.
 *   The activation form records intent and an admin will provision the account manually.
 *   After provisioning, the provider logs in and accepts the referral from the portal.
 */

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { EnrollmentForm } from '@/app/enroll/enrollment-form';
import type { EnrollmentPrefill } from '@/app/enroll/actions';
import { mapFailureReasonToInvalidReason, readPublicReferralFailureReason } from '../lib/public-referral-error';
import { fetchPublicCareConnect } from '../lib/public-referral-proxy';
import { buildCareConnectReferralLoginUrl } from '@/lib/careconnect-login-url';

export const dynamic = 'force-dynamic';

interface PageProps {
  searchParams: Promise<{ referralId?: string; token?: string; companyName?: string }>;
}

interface PublicThreadData {
  referralId:    string;
  tenantId:      string;
  providerId:    string;
  status:        string;
  clientName:    string;
  service:       string;
  providerName:  string;
  providerTitle?: string | null;
  providerFirstName?: string | null;
  providerLastName?:  string | null;
  providerEmail?: string;
  providerPhone?: string;
  locationAddressLine1?: string;
  locationCity?: string;
  locationState?: string;
  locationPostalCode?: string;
  locationIsMobile?: boolean;
  providerHasAccount?: boolean;
  referrerName:  string | null;
}

function toEnrollmentPrefill(data: PublicThreadData, fallbackCompanyName?: string): EnrollmentPrefill {
  const companyName = data.providerName.trim() || fallbackCompanyName?.trim() || '';

  // Only prefill from the provider's actual stored First/Last name. If neither is on
  // record, leave both blank — guessing a name from the email address risks writing a
  // wrong, unreviewable name onto the provider's account (the field is locked once prefilled).
  const providerContact = {
    firstName: data.providerFirstName?.trim() ?? '',
    lastName: data.providerLastName?.trim() ?? '',
  };

  return {
    providerId: data.providerId,
    companyName,
    companyType: 'Provider',
    email: data.providerEmail ?? '',
    phone: data.providerPhone ?? '',
    title: data.providerTitle?.trim() ?? '',
    firstName: providerContact.firstName,
    lastName: providerContact.lastName,
    // Mobile/roaming providers have no fixed street address — LocationAddressLine1 holds a
    // human-readable service-area label instead (e.g. "Greater Las Vegas Metro"), so it must
    // not be prefilled/locked into the account's street address field.
    addressLine1: data.locationIsMobile ? '' : data.locationAddressLine1 ?? '',
    city: data.locationIsMobile ? '' : data.locationCity ?? '',
    state: data.locationState ?? '',
    postalCode: data.locationIsMobile ? '' : data.locationPostalCode ?? '',
  };
}

export default async function ActivatePage({ searchParams }: PageProps) {
  const sp = await searchParams;
  const referralId = sp.referralId?.trim() ?? '';
  const token      = sp.token?.trim() ?? '';
  const companyName = sp.companyName?.trim() ?? '';

  if (!referralId || !token) {
    redirect('/referrals/accept/invalid?reason=missing-token');
  }

  let threadData: PublicThreadData | null = null;
  let failureReason: string | null = null;
  try {
    const resp = await fetchPublicCareConnect(
      `/api/public/referrals/thread?token=${encodeURIComponent(token)}`,
    );
    if (resp.ok) {
      const data = await resp.json() as PublicThreadData;
      if (data.referralId === referralId) {
        threadData = data;
      } else {
        failureReason = 'referral_mismatch';
      }
    } else {
      failureReason = await readPublicReferralFailureReason(resp);
    }
  } catch {
    // fall through
  }

  if (!threadData) {
    redirect(`/referrals/accept/invalid?reason=${mapFailureReasonToInvalidReason(failureReason)}`);
  }

  const prefill = toEnrollmentPrefill(threadData, companyName);
  const loginUrl = buildCareConnectReferralLoginUrl(
    process.env.CC_COMMON_PORTAL_HOSTNAME,
    `/careconnect/referrals/${referralId}`,
  );

  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50">
      <div className="max-w-2xl mx-auto px-4 py-12">
        <div className="mb-6">
          <Link
            href={`/referrals/thread?token=${encodeURIComponent(token)}`}
            className="text-sm text-gray-500 hover:text-gray-700 transition-colors"
          >
            ← Back to referral
          </Link>
        </div>

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
          prefill={prefill}
          providerId={threadData.providerId}
          tenantId={threadData.tenantId}
          referralPrefill={null}
          isFirmEnrollment={false}
        />

        {threadData.providerHasAccount && (
          <p className="text-center text-xs text-gray-400 mt-6">
            Already have platform access?{' '}
            <Link href={loginUrl} className="text-primary hover:underline">
              Log in to accept this referral
            </Link>
          </p>
        )}

      </div>
    </main>
  );
}
