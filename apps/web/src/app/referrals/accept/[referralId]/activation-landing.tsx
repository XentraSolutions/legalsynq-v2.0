import Link from 'next/link';

/**
 * LSCC-01-002-01: Public direct acceptance removed.
 *
 * This landing page is now a legacy routing surface for old email links
 * that point directly to /referrals/accept/{referralId}. New email links
 * are routed through /referrals/view → /login via the secure view token.
 *
 * This component no longer presents or handles direct token-based
 * acceptance. Providers must log in (or activate an account first)
 * before they can accept a referral.
 *
 * CTAs:
 *   Primary  — "Activate & Accept Referral" → /referrals/activate (new providers)
 *   Secondary — "Already have an account? Log in" → /login?returnTo=...
 */

interface ReferralPublicSummary {
  referralId:       string;
  clientFirstName:  string;
  clientLastName:   string;
  referrerName:     string;
  providerName:     string;
  requestedService: string;
  status:           string;
  isAlreadyAccepted: boolean;
}

interface ActivationLandingProps {
  summary:    ReferralPublicSummary;
  token:      string;
  referralId: string;
}

export function ActivationLanding({ summary, token, referralId }: ActivationLandingProps) {
  const activateUrl = `/referrals/activate?referralId=${referralId}&token=${encodeURIComponent(token)}`;
  const loginUrl    = `/login?returnTo=${encodeURIComponent(`/careconnect/referrals/${referralId}`)}&reason=referral-view`;

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full space-y-4">

        {/* Referral summary card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="h-1.5 w-full bg-primary" />
          <div className="px-6 py-5">
            <div className="flex items-start gap-3 mb-5">
              <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center shrink-0">
                <svg className="w-5 h-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
              </div>
              <div>
                <h1 className="text-lg font-semibold text-gray-900">Referral Received</h1>
                <p className="text-sm text-gray-500">You have a new referral through LegalSynq CareConnect</p>
              </div>
            </div>

            {/* Referral details */}
            <div className="bg-gray-50 border border-gray-200 rounded-lg divide-y divide-gray-200">
              {summary.clientFirstName && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Client</span>
                  <span className="text-sm text-gray-900 font-medium text-right">
                    {summary.clientFirstName} {summary.clientLastName}
                  </span>
                </div>
              )}
              {summary.referrerName && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Referred by</span>
                  <span className="text-sm text-gray-900 text-right">{summary.referrerName}</span>
                </div>
              )}
              {summary.requestedService && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Service</span>
                  <span className="text-sm text-gray-900 text-right">{summary.requestedService}</span>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Auth-required CTA card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 px-6 py-5 space-y-4">
          <div>
            <h2 className="text-sm font-semibold text-gray-900 mb-1">Log in to view and accept this referral</h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Accepting a referral requires platform access. Log in if you already have a CareConnect account,
              or activate your account to get started.
            </p>
          </div>

          <ul className="space-y-2">
            {[
              'Accept and track referrals from a single dashboard',
              'Receive notifications when new referrals are sent to you',
              'View referral history and client details securely',
              'Coordinate directly with referring firms',
            ].map((benefit) => (
              <li key={benefit} className="flex items-start gap-2 text-sm text-gray-600">
                <svg className="w-4 h-4 text-green-500 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
                </svg>
                {benefit}
              </li>
            ))}
          </ul>

          {/* Primary CTA — new providers without an account */}
          <Link
            href={activateUrl}
            className="block w-full bg-primary text-white text-sm font-medium text-center py-2.5 rounded-lg hover:opacity-90 transition-opacity"
          >
            Activate &amp; Accept Referral
          </Link>

          {/* Secondary CTA — existing platform users */}
          <div className="text-center">
            <Link href={loginUrl} className="text-sm text-primary hover:underline font-medium">
              Already have an account? Log in
            </Link>
          </div>
        </div>

      </div>
    </main>
  );
}
