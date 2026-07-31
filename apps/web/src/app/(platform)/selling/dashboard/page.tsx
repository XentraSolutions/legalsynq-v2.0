"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import Link from "next/link";
import { formatDateOnly } from "@/lib/format-date";
import { KpiCard } from "@/components/lien/kpi-card";
import { StatusBadge, PriorityBadge } from "@/components/lien/status-badge";
import { useLienStore } from "@/stores/lien-store";
import { formatCurrency } from "@/lib/lien-utils";
import { CreateCaseForm } from "@/components/lien/forms/create-case-form";
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
import { StatCard } from "@/components/selling/dashboard/stat-card";
import { ReportDetailModal } from "@/components/lien/dashboard/report-detail-modal";
import { getAllocationColor } from "@/components/lien/dashboard/status-colors";
import { STATUS_LABELS } from "@/components/lien/status-badge";
import type {
  Segment,
  ReportModalConfig,
} from "@/components/lien/dashboard/types";
import { casesService } from "@/lib/cases";
import type { CaseReportItem, LienReportItem } from "@/lib/cases/cases.types";
import { MetricCard } from "@/components/selling/dashboard/metric-card";
import { Card } from "@/components/ui/dashboard-card";
import { BaseTable } from "@/components/ui/base-table";
import { ColumnDef, SortingState } from "@tanstack/react-table";
import { liensService } from "@/lib/selling";
import { PaginationMeta } from "@/lib/contacts";
import { AgingListItem } from "@/lib/selling/liens.types";
import { Tabs } from "@/components/ui/tabs";
import { BaseSelect } from "@/components/ui/base-select";

export const dynamic = "force-dynamic";

function formatPeriodLabel(range: DateRangeValue): string {
  const from = range.from ? formatDateOnly(`${range.from}T00:00:00`) : "—";
  const to = range.to ? formatDateOnly(`${range.to}T00:00:00`) : "—";
  return `${from} – ${to}`;
}

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

const LIEN_STATUS_ORDER = ["Draft", "Offered", "Sold", "Withdrawn"];
const DATE_FILTERS = [
  { key: "Today", label: "Today" },
  { key: "Week", label: "Week" },
  { key: "Month", label: "Month" },
  { key: "Year  ", label: "Year " },
];

interface LienListItem {
  lienId: string;
  lienSeller?: number;
  activeLiens?: number;
  lienBalance?: number;
  total?: number;
  lienNumber: string;
  caseId?: null | string;
  caseNumber?: null | string;
  fundingCompanyId?: null | string;
  fundingCompany?: null | string;
  lawFirmId?: null | string;
  lawFirm?: null | string;
  caseManagerId?: null | string;
  caseManager?: null | string;
  facilityId?: null | string;
  facility?: null | string;
  initialServiceDate: string;
  billingAmount: number;
  askAmount: null | number;
  highestBidAmount?: number;
  purchasePrice: null | number;
  status: string;
}

export default function LienDashboardPage() {
  const servicing = useLienStore((s) => s.servicing);
  const { isSellMode } = useProviderMode();
  const ra = useRoleAccess();
  const [dashboardRange, setDashboardRange] = useState<DateRangeValue>({});
  const [activeReport, setActiveReport] = useState<
    "liens" | "cases" | "lawFirm" | "facility" | null
  >(null);

  const { data: dashboardStats } = useDashboardStats();
  const { data: reports, isLoading: reportLoading } =
    useDashboardReports(dashboardRange);
  const [liens, setLiens] = useState<LienListItem[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });
  const [search, setSearch] = useState("");
  // const [filters, setFilters] = useState<LiensFilterValues>(EMPTY_LIENS_FILTERS);
  const [sorting, setSorting] = useState<SortingState>([]);
  const lienRows = reports?.liens.items ?? [];
  const totalLienCount = reports?.liens.totalCount ?? 0;

  const periodLabel = useMemo(
    () => formatPeriodLabel(dashboardRange),
    [dashboardRange],
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

  const dateRanges = ["0-30", "31-60", "61-90", "91-120", "120"];

  const DaysStatusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    const rows = [
      {
        label: "0-30",
        value: 51,
      },
      {
        label: "31-60",
        value: 51,
      },
      {
        label: "61-90",
        value: 51,
      },
      {
        label: "91-120",
        value: 51,
      },
      { label: "120", value: 120 },
    ];
    for (const lien of rows) {
      const key = lien.label ?? "Unknown";
      counts[key] = lien.value;
    }
    return counts;
  }, [lienRows]);

  // Built from the known status order plus whatever the API actually returns, so a
  // status this list doesn't anticipate still shows up instead of silently dropping
  // out of the total (previously hardcoded to 4 lien statuses / 5 case statuses).
  const arAgingSegments: Segment[] = useMemo(() => {
    const keys = Array.from(
      new Set([...dateRanges, ...Object.keys(DaysStatusCounts)]),
    );
    return keys.map((key, i) => ({
      label: STATUS_LABELS[key] ?? key,
      value: DaysStatusCounts[key] ?? 0,
      color: getAllocationColor(i),
    }));
  }, [DaysStatusCounts, lienAmountsByStatus]);

  const lienStatusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const lien of lienRows) {
      const key = lien.status ?? "Unknown";
      counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
  }, [lienRows]);
  const lienSegments: Segment[] = useMemo(() => {
    const keys = Array.from(
      new Set([...LIEN_STATUS_ORDER, ...Object.keys(lienStatusCounts)]),
    );
    return keys.map((key, i) => ({
      label: STATUS_LABELS[key] ?? key,
      value: lienStatusCounts[key] ?? 0,
      color: getAllocationColor(i),
    }));
  }, [lienStatusCounts, lienAmountsByStatus]);

  function frozenColumn(left: string, width: string, last = false) {
    const edge = last
      ? "border-r border-gray-200 shadow-[4px_0_6px_-4px_rgba(0,0,0,0.10)]"
      : "";
    return {
      headerClassName: `sticky ${left} z-10 bg-gray-50 ${width} ${edge}`,
      cellClassName: `sticky ${left} z-10 bg-white group-hover:bg-gray-50 transition-colors ${edge}`,
    };
  }

  async function getApi() {
    console.log("wewww");
    let liens;
    try {
      liens = await liensService.getSellingDashboard();
      console.log(liens);
    } catch (err) {}
  }
  getApi();
  const topLiensColumns = useMemo<ColumnDef<LienListItem, any>[]>(
    () => [
      {
        id: "Lien Seller",
        accessorKey: "lienSeller",
        header: "Lien Seller",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.lienSeller}
          </span>
        ),
      },
      {
        id: "Active Liens",
        accessorKey: "activeLiens",
        header: "Active Liens",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },
      {
        id: "Lien Balance",
        accessorKey: "lienBalance",
        header: "Lien Balance",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.lienBalance}
          </span>
        ),
      },
      {
        id: "% of Total",
        accessorKey: "total",
        header: "% of Total",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.total}
          </span>
        ),
      },
    ],
    [],
  );

  const agingColumns = useMemo<ColumnDef<AgingListItem, any>[]>(
    () => [
      {
        id: "Lien Seller",
        accessorKey: "lienSeller",
        header: "Lien Seller",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.lienSeller}
          </span>
        ),
      },
      {
        id: "0-30",
        accessorKey: "activeLiens",
        header: "0-30 Days",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },
      {
        id: "31-60",
        accessorKey: "activeLiens",
        header: "31-60 Days",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },
      {
        id: "60-90",
        accessorKey: "activeLiens",
        header: "60-90 Days",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },
      {
        id: "91-120",
        accessorKey: "activeLiens",
        header: "91-120 Days",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },

      {
        id: "120",
        accessorKey: "activeLiens",
        header: "120+ Days",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },

      {
        id: "Total",
        accessorKey: "total",
        header: "Total",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.total}
          </span>
        ),
      },
      {
        id: "pastDue",
        accessorKey: "activeLiens",
        header: "Past Due %",
        cell: ({ row }) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original.activeLiens}
          </span>
        ),
      },

      {
        id: "status",
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) => {
          const status = row.original.status?.toLowerCase();

          const config: any = {
            high: {
              icon: "ri-menu-line",
              color: "text-red-600",
              bg: "bg-red-100",
            },
            medium: {
              icon: "ri-menu-2-line",
              color: "text-yellow-600",
              bg: "bg-yellow-100",
            },
            low: {
              icon: "ri-subtract-fill",
              color: "text-green-600",
              bg: "bg-green-100",
            },
          };

          const { icon, color, bg } = config[status] ?? {
            icon: "ri-question-line",
            color: "text-gray-600",
            bg: "bg-gray-100",
          };

          return (
            <span
              className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-medium ${bg} ${color}`}
            >
              <i className={`${icon} text-sm`} />
              <span className="capitalize">{status}</span>
            </span>
          );
        },
      },
    ],
    [],
  );
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
          </div>
        </div>
      </div>

      <div>
        <h2 className="text-sm font-semibold text-gray-800">Welcome</h2>
      </div>
      <div className="flex items-center justify-between gap-3">
        <div className="basis-2/4">
          <Tabs bordered={false} defaultTab="Today" tabs={DATE_FILTERS}></Tabs>
        </div>

        <div className="w-120 flex gap-3">
          <DateRangePicker
            value={dashboardRange}
            onChange={setDashboardRange}
            placeholder="Filter by date range"
          />
          <BaseSelect
            multiple={false}
            value={""}
            options={[]}
            onChange={() => {}}
          />
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <MetricCard
          label="Total Lien Revenue"
          value={4782350.72}
          trend="up"
          trendDescription={`Trending up this month`}
          statsPercentage={8.9}
          description="Up 8.9% vs Apr 1 – Apr 30"
          formatAsCurrency={true}
        />
        <MetricCard
          label="Total Outstanding"
          value={4782350.72}
          trend="up"
          trendDescription={`Trending up this month`}
          statsPercentage={8.9}
          description="Up 8.9% vs Apr 1 – Apr 30"
          formatAsCurrency={true}
        />
        <MetricCard
          label="Past Amount Due"
          value={4782350.72}
          trend="up"
          trendDescription={`Trending up this month`}
          statsPercentage={8.9}
          description="Up 8.9% vs Apr 1 – Apr 30"
          formatAsCurrency={true}
        />
        <MetricCard
          label="Payments"
          value={4782350.72}
          trend="up"
          trendDescription={`Trending up this month`}
          statsPercentage={8.9}
          description="Up 8.9% vs Apr 1 – Apr 30"
          formatAsCurrency={true}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="Total Liens"
          total={reportLoading ? 0 : totalLienCount}
          segments={arAgingSegments}
          statsType="A/R:"
          totalStats={3842196.18}
        />

        <StatCard
          title="Liens by Status"
          total={reportLoading ? 0 : totalLienCount}
          segments={lienSegments}
          statsType="A/R:"
          totalStats={3842196.18}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <Card title="Top 5 Liens by Balance" className="px-3">
          <BaseTable
            data={liens}
            columns={topLiensColumns}
            getRowId={(l) => l.lienId}
            enableSorting={false}
            emptyMessage="No liens match your filters."
            className="bg-white border-none w-full p-0"
          />
        </Card>

        <Card title="Top 5 Liens by Balance" className="px-3">
          <BaseTable
            data={liens}
            columns={topLiensColumns}
            getRowId={(l) => l.lienId}
            enableSorting={false}
            emptyMessage="No liens match your filters."
            className="bg-white border-none w-full p-0"
          />
        </Card>
      </div>

      <div>
        <Card title="Aging by Lien Seller" className="px-3">
          <BaseTable
            data={[]}
            columns={agingColumns}
            getRowId={(l) => l.lienId}
            enableSorting={false}
            emptyMessage="No liens match your filters."
            className="bg-white border-none w-full p-0"
          />
        </Card>
      </div>
    </div>
  );
}
