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
 *   Stats: Active referrals | Upcoming appts (7 days) | Completed referrals | Declined referrals
 *   Content: Active referral list + upcoming appointments
 *
 * CARECONNECT_RECEIVER (provider):
 *   Stats: New referrals | Today's appts | Accepted referrals | Completed referrals
 *   Content: Pending referral inbox + today's schedule
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

function isWithinDays(iso: string, days: number): boolean {
  const d   = new Date(iso);
  const now = new Date();
  const end = new Date(now);
  end.setDate(end.getDate() + days);
  return d >= now && d <= end;
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
        <Link href={viewAllHref} className="text-xs text-primary font-medium hover:underline">
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

  // ── Fetch data ──────────────────────────────────────────────────────────────
  // Best-effort: failures silently fall back to 0/empty rather than breaking the page.

  let referrals:    ReferralSummary[]    = [];
  let appointments: AppointmentSummary[] = [];

  // Secondary stat counters
  let completedReferralCount = 0;
  let declinedReferralCount  = 0;
  let acceptedReferralCount  = 0;
  let upcomingApptCount      = 0;

  if (showReferrerView) {
    const [activeRef, completedRef, declinedRef, scheduledAppt, confirmedAppt] =
      await Promise.allSettled([
        careConnectServerApi.referrals.search({ pageSize: 5 }),
        careConnectServerApi.referrals.search({ status: 'Completed', pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'Declined',  pageSize: 1 }),
        careConnectServerApi.appointments.search({ status: 'Scheduled', pageSize: 20 }),
        careConnectServerApi.appointments.search({ status: 'Confirmed', pageSize: 20 }),
      ]);

    if (activeRef.status === 'fulfilled') {
      referrals = activeRef.value.items.filter(
        r => !['Completed', 'Cancelled', 'Declined'].includes(r.status),
      ).slice(0, 5);
    }
    if (completedRef.status === 'fulfilled') {
      completedReferralCount = completedRef.value.totalCount;
    }
    if (declinedRef.status === 'fulfilled') {
      declinedReferralCount = declinedRef.value.totalCount;
    }

    // Merge scheduled + confirmed, filter to next 7 days, deduplicate
    const apptMap = new Map<string, AppointmentSummary>();
    if (scheduledAppt.status === 'fulfilled') {
      scheduledAppt.value.items.forEach(a => apptMap.set(a.id, a));
    }
    if (confirmedAppt.status === 'fulfilled') {
      confirmedAppt.value.items.forEach(a => apptMap.set(a.id, a));
    }
    appointments   = [...apptMap.values()]
      .filter(a => isWithinDays(a.scheduledAtUtc, 7))
      .sort((a, b) => new Date(a.scheduledAtUtc).getTime() - new Date(b.scheduledAtUtc).getTime())
      .slice(0, 5);
    upcomingApptCount = apptMap.size;

  } else {
    // Receiver
    const [newRef, acceptedRef, completedRef, scheduledAppt, confirmedAppt] =
      await Promise.allSettled([
        careConnectServerApi.referrals.search({ status: 'New',       pageSize: 5 }),
        careConnectServerApi.referrals.search({ status: 'Accepted',  pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'Completed', pageSize: 1 }),
        careConnectServerApi.appointments.search({ status: 'Scheduled', pageSize: 50 }),
        careConnectServerApi.appointments.search({ status: 'Confirmed', pageSize: 50 }),
      ]);

    if (newRef.status === 'fulfilled') {
      referrals = newRef.value.items;
    }
    if (acceptedRef.status === 'fulfilled') {
      acceptedReferralCount = acceptedRef.value.totalCount;
    }
    if (completedRef.status === 'fulfilled') {
      completedReferralCount = completedRef.value.totalCount;
    }

    const apptMap = new Map<string, AppointmentSummary>();
    if (scheduledAppt.status === 'fulfilled') {
      scheduledAppt.value.items.forEach(a => apptMap.set(a.id, a));
    }
    if (confirmedAppt.status === 'fulfilled') {
      confirmedAppt.value.items.forEach(a => apptMap.set(a.id, a));
    }
    appointments = [...apptMap.values()]
      .filter(a => isToday(a.scheduledAtUtc))
      .sort((a, b) => new Date(a.scheduledAtUtc).getTime() - new Date(b.scheduledAtUtc).getTime())
      .slice(0, 5);
  }

  // ── Render ─────────────────────────────────────────────────────────────────
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
            <StatCard label="Active Referrals"   value={referrals.length}        href="/careconnect/referrals" />
            <StatCard label="Upcoming (7 days)"  value={upcomingApptCount}       href="/careconnect/appointments" />
            <StatCard label="Completed"          value={completedReferralCount}   href="/careconnect/referrals?status=Completed" />
            <StatCard label="Declined"           value={declinedReferralCount}    href="/careconnect/referrals?status=Declined" />
          </>
        ) : (
          <>
            <StatCard label="Pending Referrals"  value={referrals.length}         href="/careconnect/referrals?status=New" />
            <StatCard label="Today's Appts"      value={appointments.length}      href="/careconnect/appointments" />
            <StatCard label="Accepted"           value={acceptedReferralCount}    href="/careconnect/referrals?status=Accepted" />
            <StatCard label="Completed"          value={completedReferralCount}   href="/careconnect/referrals?status=Completed" />
          </>
        )}
      </div>

      {/* Main panels */}
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

      {/* Quick actions */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        {showReferrerView ? (
          <>
            <QuickAction href="/careconnect/providers"    icon="ri-search-line"       label="Find Providers"    desc="Search by name, specialty, or location" />
            <QuickAction href="/careconnect/referrals"    icon="ri-file-list-3-line"  label="All Referrals"     desc="Track the status of every referral" />
            <QuickAction href="/careconnect/appointments" icon="ri-calendar-2-line"   label="Appointments"      desc="View and manage your appointments" />
          </>
        ) : (
          <>
            <QuickAction href="/careconnect/referrals"    icon="ri-mail-line"           label="Referral Inbox"   desc="Review and accept incoming referrals" />
            <QuickAction href="/careconnect/appointments" icon="ri-calendar-check-line" label="Schedule"         desc="View today's and upcoming appointments" />
            <QuickAction href="/careconnect/providers"    icon="ri-hospital-line"       label="Provider Network" desc="Browse providers in the network" />
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
