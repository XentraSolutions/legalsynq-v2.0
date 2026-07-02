'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import Link from 'next/link';
import { KpiCard } from '@/components/lien/kpi-card';
import { StatusBadge, PriorityBadge } from '@/components/lien/status-badge';
import { useLienStore } from '@/stores/lien-store';
import { formatCurrency } from '@/lib/lien-utils';
import { CreateCaseForm } from '@/components/lien/forms/create-case-form';
import { MetricCard } from '@/components/ui/metric-card';
import {
  unifiedActivityService,
  getEntityHref,
  getNotificationHref,
  filterActivityByMode,
  type UnifiedActivityItem,
} from '@/lib/unified-activity';
import { useProviderMode } from '@/hooks/use-provider-mode';
import { useRoleAccess } from '@/hooks/use-role-access';
import { casesService } from '@/lib/cases';
import {
  DashboardStats,
  AllocationSegment,
  CaseReportItem,
  LienReportItem,
  CashMetricResponse,
} from '@/lib/cases/cases.types';
import { DateRangePicker, type DateRangeValue } from '@/components/ui/date-range-picker';

export const dynamic = 'force-dynamic';

function toDateString(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function last30DaysRange(): DateRangeValue {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - 30);
  return { from: toDateString(from), to: toDateString(to) };
}


const SOURCE_LABELS: Record<string, string> = {
  audit: 'Audit',
  notification: 'Notification',
};

function activityTimeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

function getItemHref(item: UnifiedActivityItem): string | null {
  if (item.source === 'audit') return getEntityHref(item.entity);
  if (item.source === 'notification') return getNotificationHref(item.id);
  return null;
}

export default function LienDashboardPage() {
  const servicing = useLienStore((s) => s.servicing);
  const [showCreateCase, setShowCreateCase] = useState(false);
  const { mode, isSellMode } = useProviderMode();
  const ra = useRoleAccess();
  const [recentActivity, setRecentActivity] = useState<UnifiedActivityItem[]>([]);
  const [activityLoading, setActivityLoading] = useState(true);
  const [activityError, setActivityError] = useState(false);
  const [dashboardStats, setDashboardStats] = useState<DashboardStats>();
  const [dashboardRange, setDashboardRange] = useState<DateRangeValue>(last30DaysRange);
  const [lawFirmAllocation, setLawFirmAllocation] = useState<AllocationSegment[]>([]);
  const [facilityAllocation, setFacilityAllocation] = useState<AllocationSegment[]>([]);
  const [lienRows, setLienRows] = useState<LienReportItem[]>([]);
  const [totalLienCount, setTotalLienCount] = useState(0);
  const [caseRows, setCaseRows] = useState<CaseReportItem[]>([]);
  const [totalCaseCount, setTotalCaseCount] = useState(0);
  const [cashDeployed, setCashDeployed] = useState<CashMetricResponse>();
  const [cashReceived, setCashReceived] = useState<CashMetricResponse>();
  const [reportLoading, setReportLoading] = useState(false);

  const loadActivity = useCallback(async () => {
    setActivityLoading(true);
    setActivityError(false);
    try {
      const items = await unifiedActivityService.getRecentUnifiedActivity(10);
      setRecentActivity(filterActivityByMode(items, isSellMode).slice(0, 6));
    } catch {
      setActivityError(true);
    } finally {
      setActivityLoading(false);
    }
  }, [isSellMode]);

  useEffect(() => { loadActivity(); }, [loadActivity]);

  useEffect(() => {
    const getDashboardStats = async () => {
      try {
        const stats = await casesService.getDashboardStats();
        setDashboardStats(stats);
      } catch (error) {}
    }

    getDashboardStats()
  }, []);

  useEffect(() => {
    const loadReports = async () => {
      setReportLoading(true);
      try {
        const request = {
          page: 1,
          limit: 1000,
          startDate: dashboardRange.from,
          endDate: dashboardRange.to,
        };
        const [lawFirms, facilities, liens, cases, deployed, received] = await Promise.all([
          casesService.getLawFirmCaseAllocation(request),
          casesService.getMedicalFacilityCaseAllocation(request),
          casesService.getTotalLienReportRows(request),
          casesService.getTotalCaseReportRows(request),
          casesService.getCashDeployed(request),
          casesService.getCashReceived(request),
        ]);
        setLawFirmAllocation(lawFirms);
        setFacilityAllocation(facilities);
        setLienRows(liens.items);
        setTotalLienCount(liens.totalCount);
        setCaseRows(cases.items);
        setTotalCaseCount(cases.totalCount);
        setCashDeployed(deployed);
        setCashReceived(received);
      } catch (error) {
        setLawFirmAllocation([]);
        setFacilityAllocation([]);
        setLienRows([]);
        setTotalLienCount(0);
        setCaseRows([]);
        setTotalCaseCount(0);
        setCashDeployed(undefined);
        setCashReceived(undefined);
      } finally {
        setReportLoading(false);
      }
    };

    loadReports();
  }, [dashboardRange.from, dashboardRange.to]);

  const pendingTasks = servicing.filter((s) => s.status !== 'Completed');
  const overdueTasks = pendingTasks.filter((s) => new Date(s.dueDate) < new Date());

  const lienAmountsByStatus = useMemo(() => {
    const result: Record<string, { purchase: number; billing: number }> = {};
    for (const lien of lienRows) {
      const key = lien.status ?? 'Unknown';
      if (!result[key]) result[key] = { purchase: 0, billing: 0 };
      result[key].purchase += lien.purchasePrice ?? 0;
      result[key].billing += lien.originalAmount ?? 0;
    }
    return result;
  }, [lienRows]);

  const lienStatusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const lien of lienRows) {
      const key = lien.status ?? 'Unknown';
      counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
  }, [lienRows]);

  const caseStatusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const c of caseRows) {
      const key = c.status ?? 'Unknown';
      counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
  }, [caseRows]);

  const totalLienPurchase = lienRows.reduce((s, l) => s + (l.purchasePrice ?? 0), 0);
  const totalLienBilling = lienRows.reduce((s, l) => s + (l.originalAmount ?? 0), 0);

  const ALLOC_COLORS = ['#22d3ee', '#818cf8', '#f472b6', '#34d399', '#f59e0b', '#6366f1', '#fb923c', '#a78bfa'];

  const lawFirmSegments = useMemo(() => {
    return [...lawFirmAllocation]
      .sort((a, b) => b.value - a.value)
      .map((seg, i) => ({ ...seg, color: ALLOC_COLORS[i % ALLOC_COLORS.length] }));
  }, [lawFirmAllocation]);

  const facilitySegments = useMemo(() => {
    return [...facilityAllocation]
      .sort((a, b) => b.value - a.value)
      .map((seg, i) => ({ ...seg, color: ALLOC_COLORS[i % ALLOC_COLORS.length] }));
  }, [facilityAllocation]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
            <span className={[
              'text-[10px] font-semibold px-2 py-0.5 rounded-full leading-none',
              mode === 'sell' ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-slate-100 text-slate-600 border border-slate-200',
            ].join(' ')}>
              {mode === 'sell' ? 'Sell Mode' : 'Internal Mode'}
            </span>
          </div>
          <p className="text-sm text-gray-500 mt-0.5">SynqLien operational overview</p>
        </div>
        {ra.can('case:create') && (
          <button onClick={() => setShowCreateCase(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />
            New Case
          </button>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard title="Total Liens" value={dashboardStats?.totalLiens ?? 0} change={`${dashboardStats?.lienStatus?.find((ls) => ls.label === 'Draft')?.value ?? 0} draft`} changeType="neutral" icon="ri-stack-line" iconColor="text-indigo-600" href="/lien/liens" />
        <KpiCard title="Active Cases" value={dashboardStats?.totalActiveCases ?? 0} change={`${dashboardStats?.totalCases ?? 0} total`} changeType="neutral" icon="ri-folder-open-line" iconColor="text-blue-600" href="/lien/cases" />
        <KpiCard title="Pending Tasks" value={pendingTasks.length} change={overdueTasks.length > 0 ? `${overdueTasks.length} overdue` : 'All on track'} changeType={overdueTasks.length > 0 ? 'down' : 'up'} icon="ri-task-line" iconColor="text-amber-600" href="/lien/servicing" />
        <KpiCard title="Monthly Volume" value={formatCurrency(dashboardStats?.totalLienValue ?? 0)} change="All liens" changeType="neutral" icon="ri-money-dollar-circle-line" iconColor="text-emerald-600" />
      </div>

      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-gray-800">Reporting Period</h2>
        <div className="w-64">
          <DateRangePicker
            value={dashboardRange}
            onChange={setDashboardRange}
            placeholder="Filter by date range"
          />
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <MetricCard
          title="Cash Deployed"
          value={cashDeployed ? parseFloat(cashDeployed.totalAmount) : 0}
          subtitle="Based on Purchase Date"
          icon="ri-money-dollar-circle-line"
          iconBgColor="bg-blue-50"
          iconColor="text-blue-500"
          valueColor="text-blue-600"
          subtitleColor="text-gray-900"
        />
        <MetricCard
          title="Cash Received"
          value={cashReceived ? parseFloat(cashReceived.totalAmount) : 0}
          subtitle="Based on Payment Date"
          icon="ri-cash-line"
          iconBgColor="bg-green-50"
          iconColor="text-green-500"
          valueColor="text-green-600"
          subtitleColor="text-gray-900"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="Total Liens"
          total={reportLoading ? 0 : totalLienCount}
          additionalStats={[
            { label: 'Total Purchase Amount', value: formatCurrency(totalLienPurchase) },
            { label: 'Total Billing Amount', value: formatCurrency(totalLienBilling) },
          ]}
          segments={[
            {
              label: 'Draft', color: '#94a3b8',
              value: lienStatusCounts['Draft'] ?? 0,
              subStats: [
                { label: 'Purchase', value: formatCurrency(lienAmountsByStatus['Draft']?.purchase ?? 0) },
                { label: 'Billing', value: formatCurrency(lienAmountsByStatus['Draft']?.billing ?? 0) },
              ],
            },
            {
              label: 'Offered', color: '#4f46e5',
              value: lienStatusCounts['Offered'] ?? 0,
              subStats: [
                { label: 'Purchase', value: formatCurrency(lienAmountsByStatus['Offered']?.purchase ?? 0) },
                { label: 'Billing', value: formatCurrency(lienAmountsByStatus['Offered']?.billing ?? 0) },
              ],
            },
            {
              label: 'Sold', color: '#10b981',
              value: lienStatusCounts['Sold'] ?? 0,
              subStats: [
                { label: 'Purchase', value: formatCurrency(lienAmountsByStatus['Sold']?.purchase ?? 0) },
                { label: 'Billing', value: formatCurrency(lienAmountsByStatus['Sold']?.billing ?? 0) },
              ],
            },
            {
              label: 'Withdrawn', color: '#f59e0b',
              value: lienStatusCounts['Withdrawn'] ?? 0,
              subStats: [
                { label: 'Purchase', value: formatCurrency(lienAmountsByStatus['Withdrawn']?.purchase ?? 0) },
                { label: 'Billing', value: formatCurrency(lienAmountsByStatus['Withdrawn']?.billing ?? 0) },
              ],
            },
          ]}
          href="/lien/liens"
        />
        <StatCard
          title="Total Cases"
          total={reportLoading ? 0 : totalCaseCount}
          segments={[
            { label: 'Pre-Demand', value: caseStatusCounts['PreDemand'] ?? 0, color: '#f472b6' },
            { label: 'Demand Sent', value: caseStatusCounts['DemandSent'] ?? 0, color: '#6366f1' },
            { label: 'In Negotiation', value: caseStatusCounts['InNegotiation'] ?? 0, color: '#3b82f6' },
            { label: 'Settled', value: caseStatusCounts['CaseSettled'] ?? 0, color: '#10b981' },
            { label: 'Closed', value: caseStatusCounts['Closed'] ?? 0, color: '#94a3b8' },
          ]}
          href="/lien/cases"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="Law Firm Case Allocation"
          icon="ri-scales-3-line"
          total={reportLoading ? 0 : lawFirmSegments.reduce((s, seg) => s + seg.value, 0)}
          segments={lawFirmSegments}
          href="/lien/cases"
        />
        <StatCard
          title="Medical Facility Case Allocation"
          icon="ri-hospital-line"
          total={reportLoading ? 0 : facilitySegments.reduce((s, seg) => s + seg.value, 0)}
          segments={facilitySegments}
          href="/lien/cases"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h2 className="text-sm font-semibold text-gray-800">Task Queue</h2>
            <Link href="/lien/servicing" className="text-xs text-primary font-medium hover:underline">View All</Link>
          </div>
          <div className="divide-y divide-gray-100">
            {pendingTasks.slice(0, 5).map((task) => (
              <Link key={task.id} href={`/lien/servicing/${task.id}`} className="px-5 py-3 flex items-center justify-between hover:bg-gray-50 transition-colors block">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-gray-700 font-medium truncate">{task.description}</p>
                  <p className="text-xs text-gray-400 mt-0.5">{task.taskNumber} &middot; Due {task.dueDate} &middot; {task.assignedTo}</p>
                </div>
                <div className="flex items-center gap-2 ml-4">
                  <PriorityBadge priority={task.priority} />
                  <StatusBadge status={task.status} />
                </div>
              </Link>
            ))}
            {pendingTasks.length === 0 && <div className="px-5 py-8 text-center text-sm text-gray-400">No pending tasks. All caught up!</div>}
          </div>
        </div>

        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h2 className="text-sm font-semibold text-gray-800">Recent Activity</h2>
            <Link href="/lien/activity" className="text-xs text-primary font-medium hover:underline">View All</Link>
          </div>
          <div className="divide-y divide-gray-100">
            {activityLoading && (
              <div className="px-5 py-8 flex items-center justify-center gap-2 text-sm text-gray-400">
                <span className="inline-block w-4 h-4 border-2 border-gray-300 border-t-indigo-500 rounded-full animate-spin" />
                Loading...
              </div>
            )}
            {!activityLoading && activityError && (
              <div className="px-5 py-8 text-center">
                <p className="text-xs text-gray-400">Unable to load recent activity</p>
                <button onClick={loadActivity} className="text-xs text-indigo-600 mt-1 hover:underline">Retry</button>
              </div>
            )}
            {!activityLoading && !activityError && recentActivity.length === 0 && (
              <div className="px-5 py-8 text-center text-sm text-gray-400">No recent activity</div>
            )}
            {!activityLoading && !activityError && recentActivity.map((item) => {
              const href = getItemHref(item);
              const Wrapper = href ? Link : 'div';
              const wrapperProps = href
                ? { href, className: 'px-5 py-3 flex gap-3 hover:bg-gray-50 transition-colors block' }
                : { className: 'px-5 py-3 flex gap-3' };
              return (
                <Wrapper key={item.id} {...(wrapperProps as any)}>
                  <div className={`w-8 h-8 rounded-lg bg-gray-50 flex items-center justify-center shrink-0 ${item.iconColor}`}>
                    <i className={`${item.icon} text-base`} />
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-1.5">
                      <p className="text-xs text-gray-700 font-medium truncate">{item.title}</p>
                      <span className={[
                        'text-[9px] font-medium px-1 py-0.5 rounded-full shrink-0 leading-none',
                        item.source === 'audit' ? 'bg-blue-50 text-blue-600' : 'bg-purple-50 text-purple-600',
                      ].join(' ')}>
                        {SOURCE_LABELS[item.source]}
                      </span>
                    </div>
                    <p className="text-xs text-gray-500 truncate mt-0.5">{item.description}</p>
                    {item.sourceDetail.kind === 'notification' && item.sourceDetail.errorMessage && (
                      <p className="text-[10px] text-red-500 truncate mt-0.5">{item.sourceDetail.errorMessage}</p>
                    )}
                    <p className="text-xs text-gray-400 mt-0.5">
                      {item.actor ? `${item.actor.name} · ` : ''}{activityTimeAgo(item.timestampRaw)}
                    </p>
                  </div>
                </Wrapper>
              );
            })}
          </div>
        </div>
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h2 className="text-sm font-semibold text-gray-800 mb-4">Quick Actions</h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          {[
            { href: '/lien/cases', icon: 'ri-folder-add-line', label: 'New Case', color: 'text-blue-600', sellOnly: false, show: ra.can('case:create') },
            { href: '/lien/liens', icon: 'ri-stack-line', label: 'New Lien', color: 'text-indigo-600', sellOnly: false, show: ra.can('lien:create') },
            { href: '/lien/bill-of-sales', icon: 'ri-receipt-line', label: 'Bill of Sale', color: 'text-green-600', sellOnly: true, show: ra.can('bos:view') },
            { href: '/lien/batch-entry', icon: 'ri-upload-2-line', label: 'Batch Import', color: 'text-purple-600', sellOnly: false, show: ra.isSeller || ra.isAdmin },
            { href: '/lien/document-handling', icon: 'ri-file-copy-2-line', label: 'Documents', color: 'text-amber-600', sellOnly: false, show: ra.can('document:view') },
            { href: '/lien/contacts', icon: 'ri-contacts-book-line', label: 'Contacts', color: 'text-teal-600', sellOnly: false, show: ra.can('contact:view') },
          ].filter((a) => a.show && (!a.sellOnly || isSellMode)).map((action) => (
            <Link key={action.href} href={action.href} className="flex flex-col items-center gap-2 p-4 rounded-lg border border-gray-100 hover:border-gray-200 hover:bg-gray-50 transition-colors">
              <div className={`w-10 h-10 rounded-lg bg-gray-50 flex items-center justify-center ${action.color}`}>
                <i className={`${action.icon} text-xl`} />
              </div>
              <span className="text-xs font-medium text-gray-600">{action.label}</span>
            </Link>
          ))}
        </div>
      </div>

      <CreateCaseForm open={showCreateCase} onClose={() => setShowCreateCase(false)} />
    </div>
  );
}

interface SubStat { label: string; value: string | number; }
interface Segment { label: string; value: number; color: string; subStats?: SubStat[]; }
interface AdditionalStat { label: string; value: string | number; }

function StatCard({ title, total, segments, href, additionalStats, icon = 'ri-todo-line' }: {
  title: string;
  total: number;
  segments: Segment[];
  href: string;
  additionalStats?: AdditionalStat[];
  icon?: string;
}) {
  const filteredSegments = segments.filter((s) => s.value > 0);
  const grandTotal = filteredSegments.reduce((s, seg) => s + seg.value, 0);
  const dominant = filteredSegments.length > 0 ? filteredSegments.reduce((a, b) => a.value > b.value ? a : b) : { value: 0 };
  const pct = grandTotal > 0 ? ((dominant.value / grandTotal) * 100).toFixed(1) : '0';

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
        <Link href={href} className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50 transition-colors">
          <i className="ri-file-list-line text-sm leading-none" />
          View Details
        </Link>
      </div>
      <div className="flex items-start gap-6">
        <div className="flex flex-col flex-1 min-w-0">
          <div className="space-y-3 mb-4">
            <div className="flex items-start gap-2">
              <i className={`${icon} text-gray-400 text-sm mt-0.5 shrink-0`} />
              <div>
                <p className="text-xs text-gray-500">{title}</p>
                <p className="text-2xl font-bold text-blue-600 leading-tight">{total.toLocaleString()}</p>
              </div>
            </div>
            {additionalStats?.map((stat, i) => (
              <div key={i} className="flex items-start gap-2">
                <i className={`${icon} text-gray-400 text-sm mt-0.5 shrink-0`} />
                <div>
                  <p className="text-xs text-gray-500">{stat.label}</p>
                  <p className="text-sm font-bold text-blue-600">{stat.value}</p>
                </div>
              </div>
            ))}
          </div>
          {filteredSegments.length > 0 && <hr className="border-gray-100 mb-3" />}
          <ul className="space-y-2">
            {filteredSegments.map((seg, i) => (
              <li key={i}>
                <div className="flex items-center justify-between text-xs">
                  <span className="flex items-center gap-1.5">
                    <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: seg.color }} />
                    <span className="text-gray-700 font-medium">{seg.label}</span>
                  </span>
                  <span className="font-medium text-gray-700 tabular-nums">{seg.value.toLocaleString()}</span>
                </div>
                {seg.subStats && seg.subStats.length > 0 && (
                  <ul className="mt-1 space-y-0.5 pl-3.5">
                    {seg.subStats.map((sub, j) => (
                      <li key={j} className="flex items-center justify-between text-xs text-gray-400">
                        <span>{sub.label}</span>
                        <span className="tabular-nums">{sub.value}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ul>
        </div>
        <div className="shrink-0">
          <DonutChart segments={filteredSegments.length > 0 ? filteredSegments : [{ label: 'None', value: 1, color: '#e5e7eb' }]} pctLabel={`${pct}%`} />
        </div>
      </div>
    </div>
  );
}

function DonutChart({ segments, pctLabel }: { segments: Segment[]; pctLabel: string }) {
  const [tooltip, setTooltip] = useState<{ label: string; value: number } | null>(null);
  const SIZE = 120; const CX = SIZE / 2; const CY = SIZE / 2; const R = 44; const SW = 18;
  const CIRC = 2 * Math.PI * R;
  const total = segments.reduce((s, seg) => s + seg.value, 0);
  const arcs: { offset: number; dash: string; color: string; label: string; value: number }[] = [];
  let cumulative = 0;
  for (const seg of segments) {
    const fraction = total > 0 ? seg.value / total : 0;
    const arcLen = fraction * CIRC;
    arcs.push({ color: seg.color, dash: `${arcLen} ${CIRC - arcLen}`, offset: CIRC / 4 - cumulative, label: seg.label, value: seg.value });
    cumulative += arcLen;
  }
  return (
    <div className="relative">
      {tooltip && (
        <div className="absolute -top-7 left-1/2 -translate-x-1/2 bg-gray-800 text-white text-[10px] px-2 py-1 rounded whitespace-nowrap z-10 pointer-events-none">
          {tooltip.label}: {tooltip.value}
        </div>
      )}
      <svg width={SIZE} height={SIZE} viewBox={`0 0 ${SIZE} ${SIZE}`}>
        <circle cx={CX} cy={CY} r={R} fill="none" stroke="#f3f4f6" strokeWidth={SW} />
        {arcs.map((arc, i) => (
          <circle
            key={i} cx={CX} cy={CY} r={R} fill="none"
            stroke={arc.color} strokeWidth={SW}
            strokeDasharray={arc.dash} strokeDashoffset={arc.offset}
            strokeLinecap="butt" className="cursor-pointer"
            onMouseEnter={() => setTooltip({ label: arc.label, value: arc.value })}
            onMouseLeave={() => setTooltip(null)}
          />
        ))}
        <text x={CX} y={CY + 4} textAnchor="middle" fontSize="12" fontWeight="600" fill="#374151">{pctLabel}</text>
      </svg>
    </div>
  );
}
