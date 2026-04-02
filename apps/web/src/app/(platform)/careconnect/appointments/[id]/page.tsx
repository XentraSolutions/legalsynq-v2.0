import { notFound } from 'next/navigation';
import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { AppointmentDetailPanel } from '@/components/careconnect/appointment-detail-panel';
import { AppointmentActions } from '@/components/careconnect/appointment-actions';
import { AppointmentCancelButton } from '@/components/careconnect/appointment-cancel-button';

interface AppointmentDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function AppointmentDetailPage({ params }: AppointmentDetailPageProps) {
  const { id } = await params;
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
    appointment = await careConnectServerApi.appointments.getById(id);
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

      {/* Confirm / NoShow / Reschedule actions */}
      {appointment && (
        <AppointmentActions
          appointment={appointment}
          isReceiver={isReceiver}
          isReferrer={isReferrer}
        />
      )}

      {/* Cancel — separate panel below other actions */}
      {appointment && (isReferrer || isReceiver) && (
        <AppointmentCancelButton appointment={appointment} />
      )}
    </div>
  );
}
