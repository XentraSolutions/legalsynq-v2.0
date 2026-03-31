import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { StatusBadge, UrgencyBadge } from '@/components/careconnect/status-badge';
import type { ReferralSummary, AppointmentSummary } from '@/types/careconnect';

/**
 * /careconnect/dashboard — Role-aware landing page.
 *
 * CARECONNECT_REFERRER (law firm):
 *   - Active referrals (first 5, excluding Completed/Cancelled)
 *   - Upcoming appointments (first 5, status Scheduled/Confirmed)
 *   - CTA: Find Providers
 *
 * CARECONNECT_RECEIVER (provider):
 *   - Pending referrals (status New, first 5)
 *   - Today's appointments (status Scheduled/Confirmed)
 *   - CTA: Referral Inbox
 *
 * TenantAdmin bypass: admins without an explicit product role see the
 * referrer view as a best-effort fallback (they can still access all CareConnect pages).
 */

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    hour:   'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

function isToday(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth()    === now.getMonth()    &&
    d.getDate()     === now.getDate()
  );
}

function SectionCard({ title, viewAllHref, viewAllLabel, children }: {
  title:        string;
  viewAllHref:  string;
  viewAllLabel: string;
  children:     React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <h2 className="text-sm font-semibold text-gray-900">{title}</h2>
        <Link
          href={viewAllHref}
          className="text-xs text-primary font-medium hover:underline"
        >
          {viewAllLabel} →
        </Link>
      </div>
      {children}
    </div>
  );
}

function EmptyRow({ message }: { message: string }) {
  return (
    <div className="px-5 py-8 text-center">
      <p className="text-sm text-gray-400">{message}</p>
    </div>
  );
}

function ReferralRows({ referrals }: { referrals: ReferralSummary[] }) {
  if (referrals.length === 0) return <EmptyRow message="No active referrals." />;
  return (
    <ul className="divide-y divide-gray-50">
      {referrals.map(r => (
        <li key={r.id}>
          <Link
            href={`/careconnect/referrals/${r.id}`}
            className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors gap-4"
          >
            <div className="min-w-0">
              <p className="text-sm font-medium text-gray-900 truncate">
                {r.clientFirstName} {r.clientLastName}
              </p>
              <p className="text-xs text-gray-400 mt-0.5 truncate">
                {r.providerName} · {r.requestedService}
              </p>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <UrgencyBadge urgency={r.urgency} />
              <StatusBadge status={r.status} />
              <span className="text-xs text-gray-300 hidden sm:inline">
                {formatDate(r.createdAtUtc)}
              </span>
            </div>
          </Link>
        </li>
      ))}
    </ul>
  );
}

function AppointmentRows({ appointments }: { appointments: AppointmentSummary[] }) {
  if (appointments.length === 0) return <EmptyRow message="No upcoming appointments." />;
  return (
    <ul className="divide-y divide-gray-50">
      {appointments.map(a => (
        <li key={a.id}>
          <Link
            href={`/careconnect/appointments/${a.id}`}
            className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors gap-4"
          >
            <div className="min-w-0">
              <p className="text-sm font-medium text-gray-900 truncate">
                {a.clientFirstName} {a.clientLastName}
              </p>
              <p className="text-xs text-gray-400 mt-0.5 truncate">
                {a.providerName}{a.serviceType ? ` · ${a.serviceType}` : ''}
              </p>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <StatusBadge status={a.status} />
              <span className="text-xs text-gray-500 whitespace-nowrap">
                {formatDateTime(a.scheduledAtUtc)}
              </span>
            </div>
          </Link>
        </li>
      ))}
    </ul>
  );
}

export default async function DashboardPage() {
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);

  const showReferrerView = isReferrer || (!isReferrer && !isReceiver);

  // ── Fetch data (best-effort, failures show empty state rather than error) ──

  let referrals: ReferralSummary[]    = [];
  let appointments: AppointmentSummary[] = [];

  if (showReferrerView) {
    // Referrer: active referrals (not Completed/Cancelled/Declined) + upcoming scheduled appts
    const [refResult, apptResult] = await Promise.allSettled([
      careConnectServerApi.referrals.search({ pageSize: 5 }),
      careConnectServerApi.appointments.search({ status: 'Scheduled', pageSize: 5 }),
    ]);

    if (refResult.status === 'fulfilled') {
      referrals = refResult.value.items.filter(
        r => r.status !== 'Completed' && r.status !== 'Cancelled' && r.status !== 'Declined',
      ).slice(0, 5);
    }
    if (apptResult.status === 'fulfilled') {
      appointments = apptResult.value.items;
    }
  } else {
    // Receiver: new/pending referrals + today's appointments
    const [refResult, apptResult] = await Promise.allSettled([
      careConnectServerApi.referrals.search({ status: 'New', pageSize: 5 }),
      careConnectServerApi.appointments.search({ pageSize: 20 }),
    ]);

    if (refResult.status === 'fulfilled') {
      referrals = refResult.value.items;
    }
    if (apptResult.status === 'fulfilled') {
      appointments = apptResult.value.items
        .filter(a => isToday(a.scheduledAtUtc) && (a.status === 'Scheduled' || a.status === 'Confirmed'))
        .slice(0, 5);
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {showReferrerView
              ? 'Overview of your referral activity and upcoming appointments.'
              : 'Incoming referrals and today\'s appointment schedule.'}
          </p>
        </div>

        {/* Primary CTA */}
        {showReferrerView ? (
          <Link
            href="/careconnect/providers"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity shrink-0"
          >
            Find Providers
          </Link>
        ) : (
          <Link
            href="/careconnect/referrals"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity shrink-0"
          >
            Referral Inbox
          </Link>
        )}
      </div>

      {/* Stats bar */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {showReferrerView ? (
          <>
            <StatCard label="Active Referrals"  value={referrals.length}    href="/careconnect/referrals" />
            <StatCard label="Upcoming Appts"    value={appointments.length} href="/careconnect/appointments" />
            <StatCard label="New This Week"     value="—" href="/careconnect/referrals?status=New" />
            <StatCard label="Completed"         value="—" href="/careconnect/referrals?status=Completed" />
          </>
        ) : (
          <>
            <StatCard label="Pending Referrals" value={referrals.length}    href="/careconnect/referrals?status=New" />
            <StatCard label="Today's Appts"     value={appointments.length} href="/careconnect/appointments" />
            <StatCard label="Accepted"          value="—" href="/careconnect/referrals?status=Accepted" />
            <StatCard label="Completed"         value="—" href="/careconnect/referrals?status=Completed" />
          </>
        )}
      </div>

      {/* Main panels — side-by-side on lg */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {showReferrerView ? (
          <>
            <SectionCard
              title="Active Referrals"
              viewAllHref="/careconnect/referrals"
              viewAllLabel="View all"
            >
              <ReferralRows referrals={referrals} />
            </SectionCard>

            <SectionCard
              title="Upcoming Appointments"
              viewAllHref="/careconnect/appointments"
              viewAllLabel="View all"
            >
              <AppointmentRows appointments={appointments} />
            </SectionCard>
          </>
        ) : (
          <>
            <SectionCard
              title="Pending Referrals"
              viewAllHref="/careconnect/referrals?status=New"
              viewAllLabel="View all"
            >
              <ReferralRows referrals={referrals} />
            </SectionCard>

            <SectionCard
              title="Today's Appointments"
              viewAllHref="/careconnect/appointments"
              viewAllLabel="View all"
            >
              <AppointmentRows appointments={appointments} />
            </SectionCard>
          </>
        )}
      </div>

      {/* Quick actions row */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        {showReferrerView ? (
          <>
            <QuickAction href="/careconnect/providers"    icon="ri-search-line"       label="Find Providers"    desc="Search by name, specialty, or location" />
            <QuickAction href="/careconnect/referrals"    icon="ri-file-list-3-line"  label="All Referrals"     desc="Track the status of every referral" />
            <QuickAction href="/careconnect/appointments" icon="ri-calendar-2-line"   label="Appointments"      desc="View and manage your appointments" />
          </>
        ) : (
          <>
            <QuickAction href="/careconnect/referrals"    icon="ri-mail-line"         label="Referral Inbox"    desc="Review and accept incoming referrals" />
            <QuickAction href="/careconnect/appointments" icon="ri-calendar-check-line" label="Schedule"       desc="View today's and upcoming appointments" />
            <QuickAction href="/careconnect/providers"    icon="ri-hospital-line"     label="Provider Network"  desc="Browse providers in the network" />
          </>
        )}
      </div>
    </div>
  );
}

function StatCard({ label, value, href }: { label: string; value: number | string; href: string }) {
  return (
    <Link
      href={href}
      className="bg-white border border-gray-200 rounded-lg px-4 py-4 hover:border-primary transition-colors"
    >
      <p className="text-2xl font-bold text-gray-900">{value}</p>
      <p className="text-xs text-gray-500 mt-1">{label}</p>
    </Link>
  );
}

function QuickAction({ href, icon, label, desc }: {
  href:  string;
  icon:  string;
  label: string;
  desc:  string;
}) {
  return (
    <Link
      href={href}
      className="bg-white border border-gray-200 rounded-lg px-4 py-4 flex items-start gap-3 hover:border-primary transition-colors group"
    >
      <span className={`${icon} text-xl text-primary mt-0.5 shrink-0`} />
      <div>
        <p className="text-sm font-medium text-gray-900 group-hover:text-primary transition-colors">{label}</p>
        <p className="text-xs text-gray-400 mt-0.5">{desc}</p>
      </div>
    </Link>
  );
}
