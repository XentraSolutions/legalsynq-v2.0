import Link from 'next/link';
import { Suspense } from 'react';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { StatusBadge, UrgencyBadge } from '@/components/careconnect/status-badge';
import { DateRangePicker } from '@/components/careconnect/analytics/date-range-picker';
import { ReferralFunnel } from '@/components/careconnect/analytics/referral-funnel';
import { AppointmentMetricsPanel } from '@/components/careconnect/analytics/appointment-metrics';
import { ProviderPerformanceTable } from '@/components/careconnect/analytics/provider-performance';
import { parseDateRangeParams, formatDisplayDate } from '@/lib/daterange';
import {
  computeReferralFunnel,
  computeAppointmentMetrics,
  computeProviderPerformance,
} from '@/lib/careconnect-metrics';
import { formatShortTimestamp } from '@/lib/format-date';
import { tenantServerApi } from '@/lib/tenant-api';
import type { AppointmentSummary, ReferralSummary } from '@/types/careconnect';

export const dynamic = 'force-dynamic';

interface DashboardPageProps {
  searchParams: Promise<{
    analyticsFrom?: string;
    analyticsTo?:   string;
  }>;
}

type AdminReferralDashboardItem = {
  id: string;
  tenantId: string;
  providerId: string;
  status: string;
  urgency: string;
  clientFirstName: string;
  clientLastName: string;
  caseNumber?: string | null;
  requestedService: string;
  providerName: string | null;
  providerEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

function formatDateTime(iso: string, timezone: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone: timezone,
  });
}

function isToday(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  return d.getFullYear() === now.getFullYear()
    && d.getMonth() === now.getMonth()
    && d.getDate() === now.getDate();
}

function isWithinDays(iso: string, days: number): boolean {
  const d = new Date(iso);
  const now = new Date();
  const end = new Date(now);
  end.setDate(end.getDate() + days);
  return d >= now && d <= end;
}

function mapAdminReferralToSummary(item: AdminReferralDashboardItem): ReferralSummary {
  return {
    id: item.id,
    tenantId: item.tenantId,
    providerId: item.providerId,
    providerName: item.providerName ?? 'Unknown Provider',
    clientFirstName: item.clientFirstName,
    clientLastName: item.clientLastName,
    clientPhone: '',
    clientEmail: '',
    caseNumber: item.caseNumber ?? undefined,
    requestedService: item.requestedService,
    urgency: item.urgency,
    status: item.status,
    createdAtUtc: item.createdAtUtc,
    updatedAtUtc: item.updatedAtUtc,
  };
}

function SectionCard({ title, viewAllHref, viewAllLabel, children }: {
  title: string;
  viewAllHref: string;
  viewAllLabel: string;
  children: React.ReactNode;
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

function ReferralRows({ referrals, timezone }: { referrals: ReferralSummary[]; timezone: string }) {
  if (referrals.length === 0) return <EmptyRow message="No active referrals." />;
  return (
    <ul className="divide-y divide-gray-50">
      {referrals.map((r) => (
        <li key={r.id}>
          <Link
            href={`/careconnect/referrals/${r.id}?from=dashboard`}
            className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors gap-4"
          >
            <div className="min-w-0">
              <p className="text-sm font-medium text-gray-900 truncate">
                {r.clientFirstName} {r.clientLastName}
              </p>
              <p className="text-xs text-gray-400 mt-0.5 truncate">
                {r.providerName}{r.requestedService ? ` · ${r.requestedService}` : ''}
              </p>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <UrgencyBadge urgency={r.urgency} />
              <StatusBadge status={r.status} />
              <span className="text-xs text-gray-300 hidden sm:inline">
                {formatShortTimestamp(r.createdAtUtc, timezone)}
              </span>
            </div>
          </Link>
        </li>
      ))}
    </ul>
  );
}

function AppointmentRows({ appointments, timezone }: { appointments: AppointmentSummary[]; timezone: string }) {
  if (appointments.length === 0) return <EmptyRow message="No upcoming appointments." />;
  return (
    <ul className="divide-y divide-gray-50">
      {appointments.map((a) => (
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
                {formatDateTime(a.scheduledAtUtc, timezone)}
              </span>
            </div>
          </Link>
        </li>
      ))}
    </ul>
  );
}

function AnalyticsPanelCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-5 py-4 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-900">{title}</h3>
      </div>
      <div className="px-5 py-4">{children}</div>
    </div>
  );
}

function StatCard({ label, value, href }: { label: string; value: number | string; href: string }) {
  return (
    <Link href={href} className="bg-white border border-gray-200 rounded-lg px-4 py-4 hover:border-primary transition-colors">
      <p className="text-2xl font-bold text-gray-900">{value}</p>
      <p className="text-xs text-gray-500 mt-1">{label}</p>
    </Link>
  );
}

function QuickAction({ href, icon, label, desc }: {
  href: string;
  icon: string;
  label: string;
  desc: string;
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

export default async function DashboardPage({ searchParams }: DashboardPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  const tenantTimezoneResult = await tenantServerApi
    .getTimezoneSetting(session.tenantId)
    .catch(() => null);
  const tenantTimezone = tenantTimezoneResult?.value ?? 'America/Los_Angeles';

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);
  const isReceiver = session.productRoles.includes(ProductRole.CareConnectReceiver);
  const isTenantAdminView = session.isTenantAdmin && !session.isPlatformAdmin;

  if (!isReferrer && !isReceiver && !isTenantAdminView) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a CareConnect role. Contact your administrator to gain access.
      </div>
    );
  }

  const { range: analyticsRange, activePreset } = parseDateRangeParams(
    searchParamsData.analyticsFrom,
    searchParamsData.analyticsTo,
  );

  if (isTenantAdminView) {
    const upcomingTo = new Date();
    upcomingTo.setDate(upcomingTo.getDate() + 7);

    const [
      metricsResult,
      activeReferralsResult,
      scheduledAppointmentsResult,
      confirmedAppointmentsResult,
      rescheduledAppointmentsResult,
      totalRefResult,
      acceptedRefResult,
      declinedRefResult,
      cancelledRefResult,
      scheduledRefResult,
      completedRefResult,
      totalApptResult,
      completedApptResult,
      cancelledApptResult,
      noShowApptResult,
      referralItemsResult,
      appointmentItemsResult,
    ] = await Promise.allSettled([
      careConnectServerApi.adminDashboard.getMetrics(),
      careConnectServerApi.adminDashboard.getReferrals({ pageSize: 50 }),
      careConnectServerApi.adminAppointments.search({
        status: 'Scheduled',
        from: new Date().toISOString(),
        to: upcomingTo.toISOString(),
        pageSize: 50,
      }),
      careConnectServerApi.adminAppointments.search({
        status: 'Confirmed',
        from: new Date().toISOString(),
        to: upcomingTo.toISOString(),
        pageSize: 50,
      }),
      careConnectServerApi.adminAppointments.search({
        status: 'Rescheduled',
        from: new Date().toISOString(),
        to: upcomingTo.toISOString(),
        pageSize: 50,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        pageSize: 1,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        status: 'Accepted',
        pageSize: 1,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        status: 'Declined',
        pageSize: 1,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        status: 'Cancelled',
        pageSize: 1,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        status: 'Scheduled',
        pageSize: 1,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        status: 'Completed',
        pageSize: 1,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminAppointments.search({
        from: analyticsRange.from,
        to: analyticsRange.to,
        pageSize: 1,
      }),
      careConnectServerApi.adminAppointments.search({
        status: 'Completed',
        from: analyticsRange.from,
        to: analyticsRange.to,
        pageSize: 1,
      }),
      careConnectServerApi.adminAppointments.search({
        status: 'Cancelled',
        from: analyticsRange.from,
        to: analyticsRange.to,
        pageSize: 1,
      }),
      careConnectServerApi.adminAppointments.search({
        status: 'NoShow',
        from: analyticsRange.from,
        to: analyticsRange.to,
        pageSize: 1,
      }),
      careConnectServerApi.adminDashboard.getReferrals({
        pageSize: 200,
        createdFrom: analyticsRange.from,
        createdTo: analyticsRange.to,
      }),
      careConnectServerApi.adminAppointments.search({
        from: analyticsRange.from,
        to: analyticsRange.to,
        pageSize: 200,
      }),
    ]);

    const hasFetchFailure = [
      metricsResult,
      activeReferralsResult,
      scheduledAppointmentsResult,
      confirmedAppointmentsResult,
      rescheduledAppointmentsResult,
      totalRefResult,
      acceptedRefResult,
      declinedRefResult,
      cancelledRefResult,
      scheduledRefResult,
      completedRefResult,
      totalApptResult,
      completedApptResult,
      cancelledApptResult,
      noShowApptResult,
      referralItemsResult,
      appointmentItemsResult,
    ].some((result) => result.status === 'rejected');

    const dashboardMetrics = metricsResult.status === 'fulfilled' ? metricsResult.value : null;
    const operationalReferrals = activeReferralsResult.status === 'fulfilled'
      ? activeReferralsResult.value.items
      : [];
    const appointmentMap = new Map<string, AppointmentSummary>();
    if (scheduledAppointmentsResult.status === 'fulfilled') {
      scheduledAppointmentsResult.value.items.forEach((item) => appointmentMap.set(item.id, item));
    }
    if (confirmedAppointmentsResult.status === 'fulfilled') {
      confirmedAppointmentsResult.value.items.forEach((item) => appointmentMap.set(item.id, item));
    }
    if (rescheduledAppointmentsResult.status === 'fulfilled') {
      rescheduledAppointmentsResult.value.items.forEach((item) => appointmentMap.set(item.id, item));
    }

    const referrals = operationalReferrals
      .filter((item) => !['Completed', 'Cancelled', 'Declined'].includes(item.status))
      .slice(0, 5)
      .map((item) => mapAdminReferralToSummary(item as AdminReferralDashboardItem));

    const appointments = [...appointmentMap.values()]
      .filter((item) => isWithinDays(item.scheduledAtUtc, 7))
      .sort((a, b) => new Date(a.scheduledAtUtc).getTime() - new Date(b.scheduledAtUtc).getTime())
      .slice(0, 5);

    const getAdminCount = (
      result: PromiseSettledResult<{ total: number }>,
    ) => (result.status === 'fulfilled' ? result.value.total : 0);
    const getPagedCount = (
      result: PromiseSettledResult<{ totalCount: number }>,
    ) => (result.status === 'fulfilled' ? result.value.totalCount : 0);

    const funnelMetrics = computeReferralFunnel(
      getAdminCount(totalRefResult),
      getAdminCount(acceptedRefResult),
      getAdminCount(declinedRefResult),
      getAdminCount(scheduledRefResult),
      getAdminCount(completedRefResult),
    );

    const appointmentMetrics = computeAppointmentMetrics(
      getPagedCount(totalApptResult),
      getPagedCount(completedApptResult),
      getPagedCount(cancelledApptResult),
      getPagedCount(noShowApptResult),
    );

    const referralItems = referralItemsResult.status === 'fulfilled'
      ? referralItemsResult.value.items
      : [];
    const appointmentItems = appointmentItemsResult.status === 'fulfilled'
      ? appointmentItemsResult.value.items
      : [];
    const isCapped =
      (referralItemsResult.status === 'fulfilled' && referralItemsResult.value.total > 200)
      || (appointmentItemsResult.status === 'fulfilled' && appointmentItemsResult.value.totalCount > 200);

    const providerRows = computeProviderPerformance(
      referralItems.map((item) => ({
        providerId: item.providerId,
        providerName: item.providerName ?? 'Unknown Provider',
        status: item.status,
      })),
      appointmentItems.map((item) => ({ providerId: item.providerId, status: item.status })),
    );

    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Tenant-wide CareConnect activity across referrals and appointments.
            </p>
          </div>
        </div>

        {hasFetchFailure && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
            Some dashboard data could not be loaded. Showing the tenant activity that was available.
          </div>
        )}

        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
          <StatCard
            label="Open Referrals"
            value={dashboardMetrics?.openReferrals ?? referrals.length}
            href="/careconnect/referrals?from=dashboard"
          />
          <StatCard
            label="Upcoming (7 days)"
            value={
              (scheduledAppointmentsResult.status === 'fulfilled' ? scheduledAppointmentsResult.value.totalCount : 0)
              + (confirmedAppointmentsResult.status === 'fulfilled' ? confirmedAppointmentsResult.value.totalCount : 0)
              + (rescheduledAppointmentsResult.status === 'fulfilled' ? rescheduledAppointmentsResult.value.totalCount : 0)
            }
            href="/careconnect/appointments"
          />
          <StatCard
            label="Completed"
            value={getAdminCount(completedRefResult)}
            href={`/careconnect/referrals?from=dashboard&status=Completed&createdFrom=${analyticsRange.from}&createdTo=${analyticsRange.to}`}
          />
          <StatCard
            label="Declined"
            value={getAdminCount(declinedRefResult)}
            href={`/careconnect/referrals?from=dashboard&status=Declined&createdFrom=${analyticsRange.from}&createdTo=${analyticsRange.to}`}
          />
          <StatCard
            label="Cancelled"
            value={getAdminCount(cancelledRefResult)}
            href={`/careconnect/referrals?from=dashboard&status=Cancelled&createdFrom=${analyticsRange.from}&createdTo=${analyticsRange.to}`}
          />
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <SectionCard title="Active Referrals" viewAllHref="/careconnect/referrals?from=dashboard" viewAllLabel="View all">
            <ReferralRows referrals={referrals} timezone={tenantTimezone} />
          </SectionCard>
          <SectionCard title="Upcoming Appointments" viewAllHref="/careconnect/appointments" viewAllLabel="View all">
            <AppointmentRows appointments={appointments} timezone={tenantTimezone} />
          </SectionCard>
        </div>

        <div className="space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-2 border-t border-gray-100">
            <div>
              <h2 className="text-base font-semibold text-gray-900">Performance Overview</h2>
              <p className="text-xs text-gray-400 mt-0.5">
                {formatDisplayDate(analyticsRange.from)} — {formatDisplayDate(analyticsRange.to)}
              </p>
            </div>
            <Suspense fallback={null}>
              <DateRangePicker
                activePreset={activePreset}
                currentFrom={analyticsRange.from}
                currentTo={analyticsRange.to}
              />
            </Suspense>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <AnalyticsPanelCard title="Referral Funnel">
              <ReferralFunnel
                metrics={funnelMetrics}
                from={analyticsRange.from}
                to={analyticsRange.to}
              />
            </AnalyticsPanelCard>

            <AnalyticsPanelCard title="Appointment Performance">
              <AppointmentMetricsPanel
                metrics={appointmentMetrics}
                from={analyticsRange.from}
                to={analyticsRange.to}
              />
            </AnalyticsPanelCard>
          </div>

          <AnalyticsPanelCard title="Provider Performance">
            <ProviderPerformanceTable
              rows={providerRows}
              from={analyticsRange.from}
              to={analyticsRange.to}
              isCapped={isCapped}
            />
          </AnalyticsPanelCard>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <QuickAction
            href="/careconnect/referrals?from=dashboard"
            icon="ri-file-list-3-line"
            label="Tenant Referrals"
            desc="Review tenant-wide referral activity"
          />
          <QuickAction
            href="/careconnect/appointments"
            icon="ri-calendar-2-line"
            label="Tenant Appointments"
            desc="Review tenant-wide appointment activity"
          />
        </div>
      </div>
    );
  }

  const showReferrerView = isReferrer;

  let referrals: ReferralSummary[] = [];
  let appointments: AppointmentSummary[] = [];
  let completedReferralCount = 0;
  let declinedReferralCount = 0;
  let cancelledReferralCount = 0;
  let acceptedReferralCount = 0;
  let upcomingApptCount = 0;

  if (showReferrerView) {
    const [activeRef, completedRef, declinedRef, cancelledRef, scheduledAppt, confirmedAppt] =
      await Promise.allSettled([
        careConnectServerApi.referrals.search({ pageSize: 5 }),
        careConnectServerApi.referrals.search({ status: 'Completed', pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'Declined', pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'Cancelled', pageSize: 1 }),
        careConnectServerApi.appointments.search({ status: 'Scheduled', pageSize: 20 }),
        careConnectServerApi.appointments.search({ status: 'Confirmed', pageSize: 20 }),
      ]);

    if (activeRef.status === 'fulfilled') {
      referrals = activeRef.value.items
        .filter((r) => !['Completed', 'Cancelled', 'Declined'].includes(r.status))
        .slice(0, 5);
    }
    if (completedRef.status === 'fulfilled') completedReferralCount = completedRef.value.totalCount;
    if (declinedRef.status === 'fulfilled') declinedReferralCount = declinedRef.value.totalCount;
    if (cancelledRef.status === 'fulfilled') cancelledReferralCount = cancelledRef.value.totalCount;

    const apptMap = new Map<string, AppointmentSummary>();
    if (scheduledAppt.status === 'fulfilled') scheduledAppt.value.items.forEach((a) => apptMap.set(a.id, a));
    if (confirmedAppt.status === 'fulfilled') confirmedAppt.value.items.forEach((a) => apptMap.set(a.id, a));
    appointments = [...apptMap.values()]
      .filter((a) => isWithinDays(a.scheduledAtUtc, 7))
      .sort((a, b) => new Date(a.scheduledAtUtc).getTime() - new Date(b.scheduledAtUtc).getTime())
      .slice(0, 5);
    upcomingApptCount = apptMap.size;
  } else {
    const [newRef, acceptedRef, completedRef, scheduledAppt, confirmedAppt] =
      await Promise.allSettled([
        careConnectServerApi.referrals.search({ status: 'New', pageSize: 5 }),
        careConnectServerApi.referrals.search({ status: 'Accepted', pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'Completed', pageSize: 1 }),
        careConnectServerApi.appointments.search({ status: 'Scheduled', pageSize: 50 }),
        careConnectServerApi.appointments.search({ status: 'Confirmed', pageSize: 50 }),
      ]);

    if (newRef.status === 'fulfilled') referrals = newRef.value.items;
    if (acceptedRef.status === 'fulfilled') acceptedReferralCount = acceptedRef.value.totalCount;
    if (completedRef.status === 'fulfilled') completedReferralCount = completedRef.value.totalCount;

    const apptMap = new Map<string, AppointmentSummary>();
    if (scheduledAppt.status === 'fulfilled') scheduledAppt.value.items.forEach((a) => apptMap.set(a.id, a));
    if (confirmedAppt.status === 'fulfilled') confirmedAppt.value.items.forEach((a) => apptMap.set(a.id, a));
    appointments = [...apptMap.values()]
      .filter((a) => isToday(a.scheduledAtUtc))
      .sort((a, b) => new Date(a.scheduledAtUtc).getTime() - new Date(b.scheduledAtUtc).getTime())
      .slice(0, 5);
  }

  const fixed30dTo = new Date().toISOString().slice(0, 10);
  const thirtyDaysAgo = new Date();
  thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
  const fixed30dFrom = thirtyDaysAgo.toISOString().slice(0, 10);

  const [ref30Total, ref30New, ref30Accepted] = showReferrerView
    ? await Promise.allSettled([
        careConnectServerApi.referrals.search({ createdFrom: fixed30dFrom, createdTo: fixed30dTo, pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'New', createdFrom: fixed30dFrom, createdTo: fixed30dTo, pageSize: 1 }),
        careConnectServerApi.referrals.search({ status: 'Accepted', createdFrom: fixed30dFrom, createdTo: fixed30dTo, pageSize: 1 }),
      ])
    : [null, null, null];

  const activity30Total = ref30Total && ref30Total.status === 'fulfilled' ? ref30Total.value.totalCount : 0;
  const activity30New = ref30New && ref30New.status === 'fulfilled' ? ref30New.value.totalCount : 0;
  const activity30Accepted = ref30Accepted && ref30Accepted.status === 'fulfilled' ? ref30Accepted.value.totalCount : 0;
  const activity30Rate = activity30Total > 0
    ? Math.round((activity30Accepted / activity30Total) * 100)
    : 0;

  const [
    totalRef,
    acceptedRef,
    declinedRef,
    scheduledRef,
    completedRef,
    totalAppt,
    completedAppt,
    cancelledAppt,
    noShowAppt,
    referralItems,
    appointmentItems,
  ] = await Promise.allSettled([
    careConnectServerApi.referrals.search({ createdFrom: analyticsRange.from, createdTo: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.referrals.search({ status: 'Accepted', createdFrom: analyticsRange.from, createdTo: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.referrals.search({ status: 'Declined', createdFrom: analyticsRange.from, createdTo: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.referrals.search({ status: 'Scheduled', createdFrom: analyticsRange.from, createdTo: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.referrals.search({ status: 'Completed', createdFrom: analyticsRange.from, createdTo: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.appointments.search({ from: analyticsRange.from, to: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.appointments.search({ status: 'Completed', from: analyticsRange.from, to: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.appointments.search({ status: 'Cancelled', from: analyticsRange.from, to: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.appointments.search({ status: 'NoShow', from: analyticsRange.from, to: analyticsRange.to, pageSize: 1 }),
    careConnectServerApi.referrals.search({ createdFrom: analyticsRange.from, createdTo: analyticsRange.to, pageSize: 200 }),
    careConnectServerApi.appointments.search({ from: analyticsRange.from, to: analyticsRange.to, pageSize: 200 }),
  ]);

  const getCount = (result: PromiseSettledResult<{ totalCount: number }>) =>
    (result.status === 'fulfilled' ? result.value.totalCount : 0);

  const funnelMetrics = computeReferralFunnel(
    getCount(totalRef),
    getCount(acceptedRef),
    getCount(declinedRef),
    getCount(scheduledRef),
    getCount(completedRef),
  );

  const apptMetrics = computeAppointmentMetrics(
    getCount(totalAppt),
    getCount(completedAppt),
    getCount(cancelledAppt),
    getCount(noShowAppt),
  );

  const referralItemsList = referralItems.status === 'fulfilled'
    ? referralItems.value.items
    : ([] as ReferralSummary[]);
  const appointmentItemsList = appointmentItems.status === 'fulfilled'
    ? appointmentItems.value.items
    : ([] as AppointmentSummary[]);
  const referralItemsCapped = referralItems.status === 'fulfilled' && referralItems.value.totalCount > 200;
  const appointmentItemsCapped = appointmentItems.status === 'fulfilled' && appointmentItems.value.totalCount > 200;
  const isCapped = referralItemsCapped || appointmentItemsCapped;

  const providerRows = computeProviderPerformance(
    referralItemsList.map((r) => ({ providerId: r.providerId, providerName: r.providerName, status: r.status })),
    appointmentItemsList.map((a) => ({ providerId: a.providerId, status: a.status })),
  );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {showReferrerView
              ? 'Overview of your referral activity and upcoming appointments.'
              : "Incoming referrals and today's appointment schedule."}
          </p>
        </div>
        {!showReferrerView && (
          <Link href="/careconnect/referrals?from=dashboard" className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity shrink-0">
            Referral Inbox
          </Link>
        )}
      </div>

      <div className={`grid grid-cols-2 gap-4 ${showReferrerView ? 'sm:grid-cols-5' : 'sm:grid-cols-4'}`}>
        {showReferrerView ? (
          <>
            <StatCard label="Active Referrals" value={referrals.length} href="/careconnect/referrals?from=dashboard" />
            <StatCard label="Upcoming (7 days)" value={upcomingApptCount} href="/careconnect/appointments" />
            <StatCard label="Completed" value={completedReferralCount} href="/careconnect/referrals?from=dashboard&status=Completed" />
            <StatCard label="Declined" value={declinedReferralCount} href="/careconnect/referrals?from=dashboard&status=Declined" />
            <StatCard label="Cancelled" value={cancelledReferralCount} href="/careconnect/referrals?from=dashboard&status=Cancelled" />
          </>
        ) : (
          <>
            <StatCard label="Pending Referrals" value={referrals.length} href="/careconnect/referrals?from=dashboard&status=New" />
            <StatCard label="Today's Appts" value={appointments.length} href="/careconnect/appointments" />
            <StatCard label="Accepted" value={acceptedReferralCount} href="/careconnect/referrals?from=dashboard&status=Accepted" />
            <StatCard label="Completed" value={completedReferralCount} href="/careconnect/referrals?from=dashboard&status=Completed" />
          </>
        )}
      </div>

      {showReferrerView && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-900">Referral Activity</h2>
            <span className="text-xs text-gray-400">Last 30 days</span>
          </div>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <StatCard label="Total Referrals" value={activity30Total} href={`/careconnect/referrals?from=dashboard&createdFrom=${fixed30dFrom}&createdTo=${fixed30dTo}`} />
            <StatCard label="Pending" value={activity30New} href={`/careconnect/referrals?from=dashboard&status=New&createdFrom=${fixed30dFrom}&createdTo=${fixed30dTo}`} />
            <StatCard label="Accepted" value={activity30Accepted} href={`/careconnect/referrals?from=dashboard&status=Accepted&createdFrom=${fixed30dFrom}&createdTo=${fixed30dTo}`} />
            <div className="bg-white border border-gray-200 rounded-lg px-4 py-4">
              <p className="text-2xl font-bold text-gray-900">{activity30Rate}%</p>
              <p className="text-xs text-gray-500 mt-1">Acceptance Rate</p>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {showReferrerView ? (
          <>
            <SectionCard title="Active Referrals" viewAllHref="/careconnect/referrals?from=dashboard" viewAllLabel="View all">
              <ReferralRows referrals={referrals} timezone={tenantTimezone} />
            </SectionCard>
            <SectionCard title="Upcoming Appointments" viewAllHref="/careconnect/appointments" viewAllLabel="View all">
              <AppointmentRows appointments={appointments} timezone={tenantTimezone} />
            </SectionCard>
          </>
        ) : (
          <>
            <SectionCard title="Pending Referrals" viewAllHref="/careconnect/referrals?from=dashboard&status=New" viewAllLabel="View all">
              <ReferralRows referrals={referrals} timezone={tenantTimezone} />
            </SectionCard>
            <SectionCard title="Today's Appointments" viewAllHref="/careconnect/appointments" viewAllLabel="View all">
              <AppointmentRows appointments={appointments} timezone={tenantTimezone} />
            </SectionCard>
          </>
        )}
      </div>

      <div className="space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-2 border-t border-gray-100">
          <div>
            <h2 className="text-base font-semibold text-gray-900">Performance Overview</h2>
            <p className="text-xs text-gray-400 mt-0.5">
              {formatDisplayDate(analyticsRange.from)} — {formatDisplayDate(analyticsRange.to)}
            </p>
          </div>
          <Suspense fallback={null}>
            <DateRangePicker
              activePreset={activePreset}
              currentFrom={analyticsRange.from}
              currentTo={analyticsRange.to}
            />
          </Suspense>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <AnalyticsPanelCard title="Referral Funnel">
            <ReferralFunnel
              metrics={funnelMetrics}
              from={analyticsRange.from}
              to={analyticsRange.to}
            />
          </AnalyticsPanelCard>

          <AnalyticsPanelCard title="Appointment Performance">
            <AppointmentMetricsPanel
              metrics={apptMetrics}
              from={analyticsRange.from}
              to={analyticsRange.to}
            />
          </AnalyticsPanelCard>
        </div>

        <AnalyticsPanelCard title="Provider Performance">
          <ProviderPerformanceTable
            rows={providerRows}
            from={analyticsRange.from}
            to={analyticsRange.to}
            isCapped={isCapped}
          />
        </AnalyticsPanelCard>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        {showReferrerView ? (
          <>
            <QuickAction href="/careconnect/referrals?from=dashboard" icon="ri-file-list-3-line" label="All Referrals" desc="Track the status of every referral" />
            <QuickAction href="/careconnect/appointments" icon="ri-calendar-2-line" label="Appointments" desc="View and manage your appointments" />
          </>
        ) : (
          <>
            <QuickAction href="/careconnect/referrals?from=dashboard" icon="ri-mail-line" label="Referral Inbox" desc="Review and accept incoming referrals" />
            <QuickAction href="/careconnect/appointments" icon="ri-calendar-check-line" label="Schedule" desc="View today's and upcoming appointments" />
          </>
        )}
      </div>
    </div>
  );
}
