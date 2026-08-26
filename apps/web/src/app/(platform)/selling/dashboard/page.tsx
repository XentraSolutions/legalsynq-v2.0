"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSession } from "@/hooks/use-session";
import { formatDateOnly } from "@/lib/format-date";
import {
  DateRangePicker,
  type DateRangeValue,
} from "@/components/ui/date-range-picker";
import { StatCard } from "@/components/selling/dashboard/stat-card";
import { MetricCard } from "@/components/selling/dashboard/metric-card";
import {
  LiensOverTimeChart,
  type LiensOverTimePoint,
} from "@/components/selling/dashboard/liens-over-time-chart";
import { Card } from "@/components/ui/dashboard-card";
import { BaseTable } from "@/components/ui/base-table";
import type { ColumnDef, PaginationState } from "@tanstack/react-table";
import type { Segment } from "@/components/lien/dashboard/types";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";
import { liensService, type MonthlyAgingReportRow } from "@/lib/selling";

export const dynamic = "force-dynamic";

function formatUsd(value: number): string {
  return `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function formatCompactUsd(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    notation: "compact",
    maximumFractionDigits: 1,
  }).format(value);
}

function todayDateOnly(): string {
  const today = new Date();
  const year = today.getFullYear();
  const month = String(today.getMonth() + 1).padStart(2, "0");
  const day = String(today.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

// The dollar figures below are static mock data, but the picker should still visibly
// do *something* — swap the "vs <period>" comparison text to the selected range
// instead of leaving it a permanent no-op.
function formatPeriodLabel(range: DateRangeValue): string | null {
  if (!range.from || !range.to) return null;
  const from = formatDateOnly(`${range.from}T00:00:00`, {
    month: "short",
    day: "numeric",
  });
  const to = formatDateOnly(`${range.to}T00:00:00`, {
    month: "short",
    day: "numeric",
  });
  return `${from} – ${to}`;
}

// Shared blue/orange/green/yellow/red palette used across both summary donuts on this page.
const SUMMARY_COLORS = ["#3b82f6", "#f97316", "#22c55e", "#eab308", "#ef4444"];

const lienStatusSegments: Segment[] = [
  { label: "Active", value: 842, color: SUMMARY_COLORS[0] },
  { label: "Settled", value: 214, color: SUMMARY_COLORS[1] },
  { label: "In Reduction", value: 112, color: SUMMARY_COLORS[2] },
  { label: "Paid", value: 56, color: SUMMARY_COLORS[3] },
  { label: "Other / Closed", value: 24, color: SUMMARY_COLORS[4] },
];

const liensOverTimeData: LiensOverTimePoint[] = [
  { month: "Sep 24", value: 4200000 },
  { month: "Oct 24", value: 2600000 },
  { month: "Nov 24", value: 1400000 },
  { month: "Dec 24", value: 900000 },
  { month: "Jan 25", value: 1600000 },
  { month: "Feb 25", value: 3600000 },
  { month: "Mar 25", value: 5432123 },
  { month: "Apr 25", value: 3200000 },
  { month: "May 25", value: 1100000 },
];

interface TopBuyerRow {
  id: string;
  initials: string;
  name: string;
  activeLiens: number;
  lienBalance: number;
  percentOfTotal: number;
  avatarClassName: string;
}

const topBuyers: TopBuyerRow[] = [
  {
    id: "apex-mutual",
    initials: "AM",
    name: "Apex Mutual",
    activeLiens: 182,
    lienBalance: 1125842.5,
    percentOfTotal: 23.5,
    avatarClassName: "bg-blue-100 text-blue-700",
  },
  {
    id: "nova-care",
    initials: "NC",
    name: "Nova Care",
    activeLiens: 132,
    lienBalance: 687421.88,
    percentOfTotal: 14.4,
    avatarClassName: "bg-purple-100 text-purple-700",
  },
  {
    id: "summit-ins",
    initials: "SI",
    name: "Summit Ins.",
    activeLiens: 98,
    lienBalance: 456218.33,
    percentOfTotal: 9.5,
    avatarClassName: "bg-green-100 text-green-700",
  },
  {
    id: "beacon-life",
    initials: "BL",
    name: "Beacon Life",
    activeLiens: 76,
    lienBalance: 321775.19,
    percentOfTotal: 6.7,
    avatarClassName: "bg-orange-100 text-orange-700",
  },
  {
    id: "vanguard",
    initials: "VG",
    name: "Vanguard",
    activeLiens: 64,
    lienBalance: 289114.22,
    percentOfTotal: 6.0,
    avatarClassName: "bg-pink-100 text-pink-700",
  },
];

const topBuyersColumns: ColumnDef<TopBuyerRow, any>[] = [
  {
    id: "#",
    header: "#",
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>{row.index + 1}</span>
    ),
  },
  {
    id: "fundingCompany",
    header: "Funding Company",
    cell: ({ row }) => (
      <span className="flex items-center gap-2">
        <span
          className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[11px] font-semibold ${row.original.avatarClassName}`}
        >
          {row.original.initials}
        </span>
        <span className={TABLE_CELL_CLASSNAME}>{row.original.name}</span>
      </span>
    ),
  },
  {
    id: "activeLiens",
    header: "Active Liens",
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>{row.original.activeLiens}</span>
    ),
  },
  {
    id: "lienBalance",
    header: "Lien Balance",
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>
        {formatUsd(row.original.lienBalance)}
      </span>
    ),
  },
  {
    id: "percentOfTotal",
    header: "% of Total",
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>
        {row.original.percentOfTotal.toFixed(1)}%
      </span>
    ),
  },
];

// Tight padding — the report's currency columns need to fit without triggering
// horizontal scroll at the card's normal width, unlike the default px-4 cells.
const DENSE_CELL_META = { headerClassName: "px-2", cellClassName: "px-2" };

function agingCurrencyColumn(
  id: string,
  header: string,
  accessor: (row: MonthlyAgingReportRow) => number,
): ColumnDef<MonthlyAgingReportRow, any> {
  return {
    id,
    header,
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>
        {formatUsd(accessor(row.original))}
      </span>
    ),
  };
}

const agingColumns: ColumnDef<MonthlyAgingReportRow, any>[] = [
  {
    id: "lienCode",
    header: "Lien Code",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>{row.original.lienCode}</span>
    ),
  },
  {
    id: "fundingCompany",
    header: "Funding Company",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>
        {row.original.fundingCompany}
      </span>
    ),
  },
  agingCurrencyColumn("1-30", "1 - 30 Days", (r) => r.days1To30),
  agingCurrencyColumn("31-60", "31 - 60 Days", (r) => r.days31To60),
  agingCurrencyColumn("61-90", "61 - 90 Days", (r) => r.days61To90),
  agingCurrencyColumn("91-120", "91 - 120 Days", (r) => r.days91To120),
  agingCurrencyColumn("120+", "120+ Days", (r) => r.moreThan120),
  agingCurrencyColumn("total", "Total", (r) => r.totalAmount),
];

export default function SellingDashboardPage() {
  const { session } = useSession();
  const displayName = session?.orgName || session?.email?.split("@")[0] || "";
  const [dashboardRange, setDashboardRange] = useState<DateRangeValue>({});
  const [agingPagination, setAgingPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });
  const periodLabel = formatPeriodLabel(dashboardRange) ?? "Apr 1 – Apr 30";
  const agingAsOfDate = dashboardRange.to ?? todayDateOnly();
  const {
    data: monthlyAging,
    isPending: isAgingPending,
    error: agingError,
  } = useQuery({
    queryKey: ["selling-monthly-aging", agingAsOfDate, agingPagination],
    queryFn: () =>
      liensService.getMonthlyAgingReport({
        asOfDate: agingAsOfDate,
        page: agingPagination.pageIndex + 1,
        pageSize: agingPagination.pageSize,
      }),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
  const arAgingSegments = useMemo<Segment[]>(() => {
    const totals = monthlyAging?.summaryTotals;
    return [
      {
        label: "1-30 Days",
        value: totals?.days1To30 ?? 0,
        color: SUMMARY_COLORS[0],
      },
      {
        label: "31-60 Days",
        value: totals?.days31To60 ?? 0,
        color: SUMMARY_COLORS[1],
      },
      {
        label: "61-90 Days",
        value: totals?.days61To90 ?? 0,
        color: SUMMARY_COLORS[2],
      },
      {
        label: "91-120 Days",
        value: totals?.days91To120 ?? 0,
        color: SUMMARY_COLORS[3],
      },
      {
        label: "120+ Days",
        value: totals?.moreThan120 ?? 0,
        color: SUMMARY_COLORS[4],
      },
    ];
  }, [monthlyAging]);
  const totalAr = monthlyAging?.summaryTotals.totalAmount ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">
            Welcome{displayName ? `, ${displayName}` : ""}
          </h1>
          <p className="mt-1 text-sm text-gray-500">
            Monitor your lien activity and stay informed with an overview of
            your operations.
          </p>
        </div>
        <div className="w-72 shrink-0">
          <DateRangePicker
            value={dashboardRange}
            onChange={(range) => {
              setDashboardRange(range);
              setAgingPagination((current) => ({
                ...current,
                pageIndex: 0,
              }));
            }}
            placeholder="Select date range"
            presets
          />
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <MetricCard
          label="Total Lien Revenue"
          value={4782350.72}
          trend="up"
          trendDescription="Trending up this month"
          statsPercentage={8.9}
          description={`Up 8.9% vs ${periodLabel}`}
          formatAsCurrency={true}
        />
        <MetricCard
          label="Total Outstanding"
          value={3842196.18}
          trend="up"
          trendDescription="Trending up this month"
          statsPercentage={6.4}
          description={`Up 6.4% vs ${periodLabel}`}
          formatAsCurrency={true}
        />
        <MetricCard
          label="Past Amount Due"
          value={1287542.63}
          trend="up"
          trendDescription="Trending up this month"
          statsPercentage={14.2}
          description={`Up 14.2% vs ${periodLabel}`}
          formatAsCurrency={true}
        />
        <MetricCard
          label="Payments"
          value={635251.44}
          trend="down"
          trendDescription="Trending down this month"
          statsPercentage={5.0}
          description={`Down 5.0% vs ${periodLabel}`}
          formatAsCurrency={true}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="A/R Aging Summary"
          total={totalAr}
          segments={arAgingSegments}
          statsType="A/R:"
          totalStats={totalAr}
          centerValue={
            isAgingPending
              ? "..."
              : agingError
                ? "—"
                : formatCompactUsd(totalAr)
          }
          centerLabel="Total A/R"
          detailsHref="#aging-details"
        />

        <StatCard
          title="Liens by Status"
          total={1248}
          segments={lienStatusSegments}
          statsType=""
          totalStats={0}
          showHeaderStat={false}
          valueFormat="number"
          centerValue="1,248"
          centerLabel="Total Liens"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-5">
        <Card
          title="Liens Over Time"
          subtitle="Total for the last 9 months"
          className="px-3 lg:col-span-3"
        >
          <LiensOverTimeChart data={liensOverTimeData} />
        </Card>

        <Card title="Top 5 Buyers By Balance" className="px-3 lg:col-span-2">
          <BaseTable
            data={topBuyers}
            columns={topBuyersColumns}
            getRowId={(r) => r.id}
            enableSorting={false}
            enablePagination={false}
            emptyMessage="No buyers to show."
            className="bg-white border-none w-full p-0"
            headerClassName={TABLE_HEADER_CLASSNAME}
            headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
          />
        </Card>
      </div>

      <div id="aging-details" className="scroll-mt-6">
        <Card title="A/R Aging Details" icon="ri-draggable" className="px-3">
          <BaseTable
            data={monthlyAging?.data ?? []}
            columns={agingColumns}
            getRowId={(r) => r.lienCode}
            enableSorting={false}
            manualPagination
            pageCount={monthlyAging?.totalPages ?? 0}
            pagination={agingPagination}
            onPaginationChange={setAgingPagination}
            totalCount={monthlyAging?.totalCount ?? 0}
            pageSizeOptions={[10, 25, 50]}
            isLoading={isAgingPending}
            emptyMessage={
              agingError
                ? "Unable to load aging details."
                : "No aging data to show."
            }
            className="bg-white border-none w-full p-0"
            headerClassName={TABLE_HEADER_CLASSNAME}
            headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
          />
        </Card>
      </div>
    </div>
  );
}
