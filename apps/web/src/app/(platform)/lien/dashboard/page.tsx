"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import Link from "next/link";
import { formatDateOnly } from "@/lib/format-date";
import { KpiCard } from "@/components/lien/kpi-card";
import { StatusBadge, PriorityBadge } from "@/components/lien/status-badge";
import { useLienStore } from "@/stores/lien-store";
import { formatCurrency } from "@/lib/lien-utils";
import { CreateCaseForm } from "@/components/lien/forms/create-case-form";
import { MetricCard } from "@/components/ui/metric-card";
import {
  unifiedActivityService,
  getEntityHref,
  getNotificationHref,
  filterActivityByMode,
  type UnifiedActivityItem,
} from "@/lib/unified-activity";
import { useProviderMode } from "@/hooks/use-provider-mode";
import { useRoleAccess } from "@/hooks/use-role-access";
import {
  useDashboardStats,
  useDashboardReports,
} from "@/hooks/use-lien-dashboard";
import {
  DateRangePicker,
  type DateRangeValue,
} from "@/components/ui/date-range-picker";
import { StatCard } from "@/components/lien/dashboard/stat-card";
import { ReportDetailModal } from "@/components/lien/dashboard/report-detail-modal";
import { getAllocationColor } from "@/components/lien/dashboard/status-colors";
import { STATUS_LABELS } from "@/components/lien/status-badge";
import type {
  Segment,
  ReportModalConfig,
} from "@/components/lien/dashboard/types";
import type { CaseReportItem, LienReportItem } from "@/lib/cases/cases.types";

export const dynamic = "force-dynamic";

function formatPeriodLabel(range: DateRangeValue): string {
  const from = range.from ? formatDateOnly(`${range.from}T00:00:00`) : "—";
  const to = range.to ? formatDateOnly(`${range.to}T00:00:00`) : "—";
  return `${from} – ${to}`;
}

const SOURCE_LABELS: Record<string, string> = {
  audit: "Audit",
  notification: "Notification",
};

function activityTimeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

function getItemHref(item: UnifiedActivityItem): string | null {
  if (item.source === "audit") return getEntityHref(item.entity);
  if (item.source === "notification") return getNotificationHref(item.id);
  return null;
}

const LIEN_STATUS_ORDER = ["Draft", "Offered", "Sold", "Withdrawn"];
const CASE_STATUS_ORDER = [
  "PreDemand",
  "DemandSent",
  "InNegotiation",
  "CaseSettled",
  "Closed",
];

export default function LienDashboardPage() {
  const servicing = useLienStore((s) => s.servicing);
  const [showCreateCase, setShowCreateCase] = useState(false);
  const { isSellMode } = useProviderMode();
  const ra = useRoleAccess();
  const [recentActivity, setRecentActivity] = useState<UnifiedActivityItem[]>(
    [],
  );
  const [activityLoading, setActivityLoading] = useState(true);
  const [activityError, setActivityError] = useState(false);
  const [dashboardRange, setDashboardRange] = useState<DateRangeValue>({});
  const [activeReport, setActiveReport] = useState<
    "liens" | "cases" | "lawFirm" | "facility" | null
  >(null);

  const { data: dashboardStats } = useDashboardStats();
  const { data: reports, isLoading: reportLoading } =
    useDashboardReports(dashboardRange);

  const lawFirmAllocation = reports?.lawFirms.segments ?? [];
  const lawFirmRows = reports?.lawFirms.rows ?? [];
  const facilityAllocation = reports?.facilities.segments ?? [];
  const facilityRows = reports?.facilities.rows ?? [];
  const lienRows = reports?.liens.items ?? [];
  const totalLienCount = reports?.liens.totalCount ?? 0;
  const caseRows = reports?.cases.items ?? [];
  const totalCaseCount = reports?.cases.totalCount ?? 0;
  const cashDeployed = reports?.deployed;
  const cashReceived = reports?.received;

  const periodLabel = useMemo(
    () => formatPeriodLabel(dashboardRange),
    [dashboardRange],
  );

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

  useEffect(() => {
    loadActivity();
  }, [loadActivity]);

  const pendingTasks = servicing.filter((s) => s.status !== "Completed");
  const overdueTasks = pendingTasks.filter(
    (s) => new Date(s.dueDate) < new Date(),
  );

  const lienAmountsByStatus = useMemo(() => {
    const result: Record<string, { purchase: number; billing: number }> = {};
    for (const lien of lienRows) {
      const key = lien.status ?? "Unknown";
      if (!result[key]) result[key] = { purchase: 0, billing: 0 };
      result[key].purchase += lien.totalPurchaseAmount ?? 0;
      result[key].billing += lien.totalBillingAmount ?? 0;
    }
    return result;
  }, [lienRows]);

  const lienStatusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const lien of lienRows) {
      const key = lien.status ?? "Unknown";
      counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
  }, [lienRows]);

  const caseStatusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const c of caseRows) {
      const key = c.status ?? "Unknown";
      counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
  }, [caseRows]);

  const totalLienPurchase = lienRows.reduce(
    (s, l) => s + (l.totalPurchaseAmount ?? 0),
    0,
  );
  const totalLienBilling = lienRows.reduce(
    (s, l) => s + (l.totalBillingAmount ?? 0),
    0,
  );

  // Built from the known status order plus whatever the API actually returns, so a
  // status this list doesn't anticipate still shows up instead of silently dropping
  // out of the total (previously hardcoded to 4 lien statuses / 5 case statuses).
  const lienSegments: Segment[] = useMemo(() => {
    const keys = Array.from(
      new Set([...LIEN_STATUS_ORDER, ...Object.keys(lienStatusCounts)]),
    );
    return keys.map((key, i) => ({
      label: STATUS_LABELS[key] ?? key,
      value: lienStatusCounts[key] ?? 0,
      color: getAllocationColor(i),
      subStats: [
        {
          label: "Purchase",
          value: formatCurrency(lienAmountsByStatus[key]?.purchase ?? 0),
        },
        {
          label: "Billing",
          value: formatCurrency(lienAmountsByStatus[key]?.billing ?? 0),
        },
      ],
    }));
  }, [lienStatusCounts, lienAmountsByStatus]);

  const caseSegments: Segment[] = useMemo(() => {
    const keys = Array.from(
      new Set([...CASE_STATUS_ORDER, ...Object.keys(caseStatusCounts)]),
    );
    return keys.map((key, i) => ({
      label: STATUS_LABELS[key] ?? key,
      value: caseStatusCounts[key] ?? 0,
      color: getAllocationColor(i),
    }));
  }, [caseStatusCounts]);

  const lawFirmSegments: Segment[] = useMemo(() => {
    return [...lawFirmAllocation]
      .sort((a, b) => b.value - a.value)
      .map((seg, i) => ({ ...seg, color: getAllocationColor(i) }));
  }, [lawFirmAllocation]);

  const facilitySegments: Segment[] = useMemo(() => {
    return [...facilityAllocation]
      .sort((a, b) => b.value - a.value)
      .map((seg, i) => ({ ...seg, color: getAllocationColor(i) }));
  }, [facilityAllocation]);

  const reportConfig: Record<
    "liens" | "cases" | "lawFirm" | "facility",
    ReportModalConfig
  > = {
    liens: {
      title: "Total Lien Report",
      totalLabel: "Total Liens",
      total: totalLienCount,
      segments: lienSegments,
      columns: [
        {
          label: "Lien ID",
          render: (r: LienReportItem) => r.lienNumber ?? "—",
        },
        {
          label: "Case ID",
          render: (r: LienReportItem) => r.caseNumber ?? "—",
        },
        {
          label: "Plaintiff Name",
          render: (r: LienReportItem) => r.clientName ?? "—",
        },
        {
          label: "Lien Status",
          render: (r: LienReportItem) =>
            STATUS_LABELS[r.status ?? ""] ?? r.status ?? "—",
        },
      ],
      rows: lienRows,
      rowKey: (r: LienReportItem) => r.id,
    },
    cases: {
      title: "Total Case Report",
      totalLabel: "Total Cases",
      total: totalCaseCount,
      segments: caseSegments,
      columns: [
        {
          label: "Case ID",
          render: (r: CaseReportItem) => r.caseNumber ?? "—",
        },
        {
          label: "Plaintiff Name",
          render: (r: CaseReportItem) => r.clientName ?? "—",
        },
        {
          label: "Date of Loss",
          render: (r: CaseReportItem) => r.dateOfIncident ?? "—",
        },
        {
          label: "Status",
          render: (r: CaseReportItem) =>
            STATUS_LABELS[r.status ?? ""] ?? r.status ?? "—",
        },
      ],
      rows: caseRows,
      rowKey: (r: CaseReportItem) => r.id,
    },
    lawFirm: {
      title: "Total Law Firm Case Allocation Report",
      totalLabel: "Total Cases Allocated",
      total: lawFirmSegments.reduce((s, seg) => s + seg.value, 0),
      segments: lawFirmSegments,
      columns: [
        {
          label: "Case ID",
          render: (r: CaseReportItem) => r.caseNumber ?? "—",
        },
        {
          label: "Plaintiff Name",
          render: (r: CaseReportItem) => r.clientName ?? "—",
        },
        {
          label: "Date of Loss",
          render: (r: CaseReportItem) => r.dateOfIncident ?? "—",
        },
        { label: "Law Firm", render: (r: CaseReportItem) => r.lawFirm ?? "—" },
      ],
      rows: lawFirmRows,
      rowKey: (r: CaseReportItem) => r.id,
    },
    facility: {
      title: "Total Medical Facility Case Allocation Report",
      totalLabel: "Total Cases Allocated",
      total: facilitySegments.reduce((s, seg) => s + seg.value, 0),
      segments: facilitySegments,
      columns: [
        {
          label: "Case ID",
          render: (r: LienReportItem) => r.caseNumber ?? "—",
        },
        {
          label: "Plaintiff Name",
          render: (r: LienReportItem) => r.clientName ?? "—",
        },
        {
          label: "Date of Loss",
          render: (r: LienReportItem) => r.incidentDate ?? "—",
        },
        {
          label: "Medical Facility",
          render: (r: LienReportItem) => r.facilityName ?? "—",
        },
      ],
      rows: facilityRows,
      rowKey: (r: LienReportItem) => r.id,
    },
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
          </div>
        </div>
        {/* not part of phase 1 migration */}
        {/* {ra.can('case:create') && (
          <button onClick={() => setShowCreateCase(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />
            New Case
          </button>
        )} */}
      </div>

      {/* KPI Cards not part of phase 1 mgiration */}
      {/* <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard title="Total Liens" value={dashboardStats?.totalLiens ?? 0} change={`${dashboardStats?.lienStatus?.find((ls) => ls.label === 'Draft')?.value ?? 0} draft`} changeType="neutral" icon="ri-stack-line" iconColor="text-indigo-600" href="/lien/liens" />
        <KpiCard title="Active Cases" value={dashboardStats?.totalActiveCases ?? 0} change={`${dashboardStats?.totalCases ?? 0} total`} changeType="neutral" icon="ri-folder-open-line" iconColor="text-blue-600" href="/lien/cases" />
        <KpiCard title="Pending Tasks" value={pendingTasks.length} change={overdueTasks.length > 0 ? `${overdueTasks.length} overdue` : 'All on track'} changeType={overdueTasks.length > 0 ? 'down' : 'up'} icon="ri-task-line" iconColor="text-amber-600" href="/lien/servicing" />
        <KpiCard title="Monthly Volume" value={formatCurrency(dashboardStats?.totalLienValue ?? 0)} change="All liens" changeType="neutral" icon="ri-money-dollar-circle-line" iconColor="text-emerald-600" />
      </div> */}

      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-gray-800">
          Reporting Period
        </h2>
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
            {
              label: "Total Purchase Amount",
              value: formatCurrency(totalLienPurchase),
            },
            {
              label: "Total Billing Amount",
              value: formatCurrency(totalLienBilling),
            },
          ]}
          segments={lienSegments}
          href="/lien/liens"
          onViewDetails={() => setActiveReport("liens")}
        />
        <StatCard
          title="Total Cases"
          total={reportLoading ? 0 : totalCaseCount}
          segments={caseSegments}
          href="/lien/cases"
          onViewDetails={() => setActiveReport("cases")}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="Law Firm Case Allocation"
          icon="ri-scales-3-line"
          total={
            reportLoading
              ? 0
              : lawFirmSegments.reduce((s, seg) => s + seg.value, 0)
          }
          segments={lawFirmSegments}
          href="/lien/cases"
          onViewDetails={() => setActiveReport("lawFirm")}
        />
        <StatCard
          title="Medical Facility Case Allocation"
          icon="ri-hospital-line"
          total={
            reportLoading
              ? 0
              : facilitySegments.reduce((s, seg) => s + seg.value, 0)
          }
          segments={facilitySegments}
          href="/lien/cases"
          onViewDetails={() => setActiveReport("facility")}
        />
      </div>

      {activeReport && (
        <ReportDetailModal
          open={!!activeReport}
          onClose={() => setActiveReport(null)}
          config={reportConfig[activeReport]}
          periodLabel={periodLabel}
        />
      )}
      {/* not part of phase 1 migration */}
      {/* <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
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
        */}
    </div>
  );
}
