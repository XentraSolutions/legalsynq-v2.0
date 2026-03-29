import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { AppointmentListTable } from '@/components/careconnect/appointment-list-table';

interface AppointmentsPageProps {
  searchParams: {
    status?: string;
    page?:   string;
  };
}

/**
 * /careconnect/appointments — Appointment list.
 *
 * Access: CARECONNECT_REFERRER or CARECONNECT_RECEIVER.
 *
 * UX shaping by role:
 *   - CARECONNECT_REFERRER (law firm):  "Sent Appointments"
 *     (they see only appointments that originated from their referrals)
 *   - CARECONNECT_RECEIVER (provider):  "Incoming Appointments"
 *     (they see only appointments directed at their org)
 *   The backend scopes results automatically; the role only affects the heading.
 *
 * The "New Appointment" flow is always initiated via the availability page
 * (Find Providers → availability → BookingPanel), not from here directly.
 * Referrers get a shortcut button to /careconnect/providers as a reminder.
 */
export default async function AppointmentsPage({ searchParams }: AppointmentsPageProps) {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  if (!isReferrer && !isReceiver) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a CareConnect role. Contact your administrator to gain access.
      </div>
    );
  }

  const page = Math.max(1, parseInt(searchParams.page ?? '1') || 1);

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.appointments.search({
      status:   searchParams.status || undefined,
      page,
      pageSize: 20,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load appointments.';
  }

  const heading = isReferrer ? 'Sent Appointments' : 'Incoming Appointments';

  const STATUS_FILTERS = ['', 'Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'];

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>

        {/* Referrers: shortcut to the booking entry-point */}
        {isReferrer && (
          <Link
            href="/careconnect/providers"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Book Appointment
          </Link>
        )}
      </div>

      {/* Status filter chips */}
      <div className="flex items-center gap-2 flex-wrap">
        {STATUS_FILTERS.map(s => (
          <Link
            key={s}
            href={s ? `/careconnect/appointments?status=${s}` : '/careconnect/appointments'}
            className={`text-sm px-3 py-1 rounded-full border transition-colors ${
              (searchParams.status ?? '') === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s || 'All'}
          </Link>
        ))}
      </div>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Table */}
      {result && (
        <AppointmentListTable
          appointments={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
        />
      )}
    </div>
  );
}
