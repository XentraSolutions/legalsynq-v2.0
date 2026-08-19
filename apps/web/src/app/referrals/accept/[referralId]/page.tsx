/**
 * LSCC-005 / LSCC-008 / LSCC-01-002-01: Legacy referral landing.
 *
 * Server component — fetches referral context from the backend before rendering.
 * No authentication required; the view token is the proof-of-identity.
 *
 * LSCC-01-002-01: Email links should open the referral details page first.
 * This route remains only for direct activation links. Invalid or expired
 * legacy links should fail here instead of being forwarded to the thread page.
 *
 * Routing:
 *   referralId === 'invalid'
 *     → <InvalidScreen> (bad/missing/expired token)
 *
 *   token validates but referral already accepted
 *     → <AlreadyAcceptedScreen>
 *
 *   token validates, referral is New
 *     → <ActivationLanding> (auth-required: Activate & Accept | Log in)
 *
 *   token cannot be validated (null summary)
 *     → redirect to invalid screen
 */

import { redirect } from "next/navigation";
import Link from "next/link";
import { ActivationLanding } from "./activation-landing";
import {
  mapFailureReasonToInvalidReason,
  readPublicReferralFailureReason,
} from "../../lib/public-referral-error";
import { fetchPublicCareConnect } from "../../lib/public-referral-proxy";
import {
  buildCareConnectLoginUrl,
  buildCareConnectReferralLoginUrl,
} from "@/lib/careconnect-login-url";
const INVALID_ID = "invalid";

interface PageProps {
  params: Promise<{ referralId: string }>;
  searchParams: Promise<{ token?: string; reason?: string }>;
}

interface PublicAttachmentInfo {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
}

interface PublicSummary {
  referralId: string;
  clientFirstName: string;
  clientLastName: string;
  referrerName: string;
  providerName: string;
  providerPhone: string;
  providerEmail: string;
  providerAddressLine1: string;
  providerCity: string;
  providerState: string;
  providerPostalCode: string;
  requestedService: string;
  status: string;
  isAlreadyAccepted: boolean;
  attachments: PublicAttachmentInfo[];
}

// ── Static screen components (no interactivity needed) ────────────────────────

function InvalidScreen({
  reason,
  loginUrl,
}: {
  reason: string;
  loginUrl: string;
}) {
  const isRevoked = reason === "revoked";
  const isMissing = reason === "missing-token";
  const isNotFound = reason === "referral-not-found";

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div
          className={`h-1.5 w-full ${isRevoked ? "bg-orange-400" : "bg-red-400"}`}
        />
        <div className="p-8 text-center">
          <div className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-5 bg-gray-100">
            {isRevoked ? (
              <svg
                className="w-7 h-7 text-orange-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 15v2m0 0v2m0-2h2m-2 0H10m7-7V9a5 5 0 00-10 0v1M5 12h14a1 1 0 011 1v7a1 1 0 01-1 1H5a1 1 0 01-1-1v-7a1 1 0 011-1z"
                />
              </svg>
            ) : (
              <svg
                className="w-7 h-7 text-red-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"
                />
              </svg>
            )}
          </div>

          <h1 className="text-xl font-semibold text-gray-900 mb-2">
            {isMissing && "Link Missing"}
            {isRevoked && "Link Revoked"}
            {isNotFound && "Referral Not Found"}
            {!isMissing &&
              !isRevoked &&
              !isNotFound &&
              "Link Expired or Invalid"}
          </h1>

          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            {isMissing &&
              "No access token was found in the link. Please use the original email link sent to you."}
            {isRevoked &&
              "This referral link has been revoked by the sending organisation. " +
                "A new link may have been sent to you — please check your inbox, " +
                "or contact the referring party to request a fresh invitation."}
            {isNotFound &&
              "This referral is no longer available. It may have been removed or replaced. " +
                "Please contact the referring party to request an updated referral link."}
            {!isMissing &&
              !isRevoked &&
              !isNotFound &&
              "This referral link has expired or is no longer valid. " +
                "Links are valid for 30 days from the date the referral was sent. " +
                "Please contact the referring party to request a new link."}
          </p>

          <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 text-left mb-6">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
              What to do next
            </p>
            <ol className="space-y-2 text-sm text-gray-600">
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">1.</span>
                Check your inbox for a more recent email from the referring
                party.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">2.</span>
                If you cannot find a newer link, contact the referring party and
                ask them to resend the referral invitation.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">3.</span>
                <span>
                  If you are an existing platform user, you can{" "}
                  <Link
                    href={loginUrl}
                    className="text-primary hover:underline"
                  >
                    log in
                  </Link>{" "}
                  to view referrals sent to your organisation.
                </span>
              </li>
            </ol>
          </div>

          <p className="text-xs text-gray-400">
            If you believe this is an error, please contact your system
            administrator.
          </p>
        </div>
      </div>
    </main>
  );
}

function AlreadyAcceptedScreen({
  summary,
  loginUrl,
}: {
  summary: PublicSummary;
  loginUrl: string;
}) {
  const clientName = [summary.clientFirstName, summary.clientLastName]
    .filter(Boolean)
    .join(" ");
  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-green-400" />
        <div className="p-8 text-center">
          <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg
              className="w-7 h-7 text-green-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M5 13l4 4L19 7"
              />
            </svg>
          </div>
          <h1 className="text-xl font-semibold text-gray-900 mb-2">
            Referral Already Accepted
          </h1>
          <p className="text-sm text-gray-500 mb-2">
            {clientName
              ? `The referral for ${clientName} has already been accepted.`
              : "This referral has already been accepted."}
          </p>
          <p className="text-sm text-gray-500 mb-6">
            The referring party has been notified. No further action is required
            from this link.
          </p>
          <Link
            href={loginUrl}
            className="inline-block bg-primary text-white text-sm font-medium px-5 py-2 rounded-lg hover:opacity-90 transition-opacity"
          >
            Log in to view in portal
          </Link>
          <p className="mt-4 text-xs text-gray-400">
            Log in to track this referral and manage future referrals in your
            dashboard.
          </p>
        </div>
      </div>
    </main>
  );
}

// ── Server Component ──────────────────────────────────────────────────────────

export default async function ReferralAcceptPage({
  params,
  searchParams,
}: PageProps) {
  const { referralId } = await params;
  const sp = await searchParams;
  const token = sp.token?.trim() ?? "";
  const reason = sp.reason?.trim() ?? "";
  console.log("hereeee");
  // Static invalid route (e.g. /referrals/accept/invalid?reason=...)

  if (referralId === INVALID_ID) {
    const loginUrl = buildCareConnectLoginUrl(
      process.env.CC_COMMON_PORTAL_HOSTNAME,
    );
    return <InvalidScreen reason={reason} loginUrl={loginUrl} />;
  }

  if (!token) {
    redirect("/referrals/accept/invalid?reason=missing-token");
  }

  let summary: PublicSummary | null = null;
  let failureReason: string | null = null;
  try {
    const resp = await fetchPublicCareConnect(
      `/api/referrals/${referralId}/public-summary?token=${encodeURIComponent(token)}`,
    );
    if (resp.ok) {
      summary = await resp.json();
    } else {
      failureReason = await readPublicReferralFailureReason(resp);
    }
  } catch {
    // network error — fall through to invalid
  }

  if (!summary) {
    redirect(
      `/referrals/accept/invalid?reason=${mapFailureReasonToInvalidReason(failureReason)}`,
    );
  }

  if (summary.isAlreadyAccepted) {
    const loginUrl = buildCareConnectReferralLoginUrl(
      process.env.CC_COMMON_PORTAL_HOSTNAME,
      `/careconnect/referrals/${summary.referralId}`,
    );
    return <AlreadyAcceptedScreen summary={summary} loginUrl={loginUrl} />;
  }

  return (
    <ActivationLanding
      summary={summary}
      token={token}
      referralId={referralId}
    />
  );
}
