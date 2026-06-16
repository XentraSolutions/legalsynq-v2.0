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
  providerEmail?: string;
  providerPhone?: string;
  providerAddressLine1?: string;
  providerCity?: string;
  providerState?: string;
  providerPostalCode?: string;
  providerHasAccount?: boolean;
  referrerName:  string | null;
}

function splitName(value: string | null | undefined): { firstName: string; lastName: string } {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) return { firstName: '', lastName: '' };

  const parts = trimmed.split(/\s+/).filter(Boolean);
  return {
    firstName: toDisplayCase(parts[0] ?? ''),
    lastName: toDisplayCase(parts.slice(1).join(' ')),
  };
}

function toDisplayCase(value: string): string {
  return value
    .split(/\s+/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
    .join(' ');
}

function deriveNameFromEmail(email: string | undefined): { firstName: string; lastName: string } {
  const local = email?.split('@')[0] ?? '';
  const normalized = local
    .replace(/\+.*/, '')
    .replace(/[._-]+/g, ' ')
    .replace(/\d+/g, ' ')
    .trim();

  return splitName(normalized);
}

function toEnrollmentPrefill(data: PublicThreadData, fallbackCompanyName?: string): EnrollmentPrefill {
  const companyName = data.providerName.trim() || fallbackCompanyName?.trim() || '';
  const providerContact = deriveNameFromEmail(data.providerEmail);

  return {
    providerId: data.providerId,
    companyName,
    companyType: 'Provider',
    email: data.providerEmail ?? '',
    phone: data.providerPhone ?? '',
    firstName: providerContact.firstName,
    lastName: providerContact.lastName,
    addressLine1: data.providerAddressLine1 ?? '',
    city: data.providerCity ?? '',
    state: data.providerState ?? '',
    postalCode: data.providerPostalCode ?? '',
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
