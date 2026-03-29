import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { AppointmentDetailPanel } from '@/components/careconnect/appointment-detail-panel';

interface AppointmentDetailPageProps {
  params: { id: string };
}

/**
 * /careconnect/appointments/[id] — Appointment detail.
 *
 * Access: CARECONNECT_REFERRER or CARECONNECT_RECEIVER.
 *   Backend enforces org-participant check; a 403 on a valid ID means the
 *   caller's org is neither the referring nor the receiving org.
 *
 * Rendering: Server Component.
 *   Status changes (Confirm / Cancel / NoShow) are Phase 2 — Server Actions.
 */
export default async function AppointmentDetailPage({ params }: AppointmentDetailPageProps) {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!isReferrer && !isReceiver) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a CareConnect role.
      </div>
    );
  }

  let appointment = null;
  let fetchError: string | null = null;

  try {
    appointment = await careConnectServerApi.appointments.getById(params.id);
  } catch (err) {
    if (err instanceof ServerApiError) {
      if (err.isNotFound) notFound();
      if (err.isForbidden) {
        fetchError = 'You do not have access to this appointment. Your organization is not a participant.';
      } else {
        fetchError = err.message;
      }
    } else {
      fetchError = 'Failed to load appointment.';
    }
  }

  return (
    <div className="space-y-4">
      {/* Back link */}
      <nav className="flex items-center gap-4">
        <Link
          href="/careconnect/appointments"
          className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
        >
          ← Back to Appointments
        </Link>

        {/* Link back to the source referral if present */}
        {appointment?.referralId && (
          <Link
            href={`/careconnect/referrals/${appointment.referralId}`}
            className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
          >
            View referral →
          </Link>
        )}
      </nav>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {appointment && <AppointmentDetailPanel appointment={appointment} />}

      {/* Phase 2 placeholder for status actions (receivers: Confirm / NoShow) */}
      {appointment && isReceiver && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-400">
          Status management (Confirm, Cancel, No-show) is coming in Phase 2.
        </div>
      )}
    </div>
  );
}
