"use client";

import { useState } from "react";
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
import type { ColumnDef } from "@tanstack/react-table";
import type { Segment } from "@/components/lien/dashboard/types";

export const dynamic = "force-dynamic";

function formatUsd(value: number): string {
  return `$${value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

// The dollar figures below are static mock data, but the picker should still visibly
// do *something* — swap the "vs <period>" comparison text to the selected range
// instead of leaving it a permanent no-op.
function formatPeriodLabel(range: DateRangeValue): string | null {
  if (!range.from || !range.to) return null;
  const from = formatDateOnly(`${range.from}T00:00:00`, { month: "short", day: "numeric" });
  const to = formatDateOnly(`${range.to}T00:00:00`, { month: "short", day: "numeric" });
  return `${from} – ${to}`;
}

// Shared blue/orange/green/yellow/red palette used across both summary donuts on this page.
const SUMMARY_COLORS = ["#3b82f6", "#f97316", "#22c55e", "#eab308", "#ef4444"];

// Dollar amounts are derived from the displayed percentages of the $3,842,196.18
// total (rather than typed in independently) so the pie wedges, legend
// percentages, and "Total A/R" header can't drift out of sync with each other.
const arAgingSegments: Segment[] = [
  { label: "0-30 Days", value: 1256398.15, color: SUMMARY_COLORS[0] },
  { label: "31-60 Days", value: 987444.42, color: SUMMARY_COLORS[1] },
  { label: "61-90 Days", value: 753070.45, color: SUMMARY_COLORS[2] },
  { label: "91-120 Days", value: 430325.97, color: SUMMARY_COLORS[3] },
  { label: "120+ Days", value: 414957.19, color: SUMMARY_COLORS[4] },
];

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
  { id: "apex-mutual", initials: "AM", name: "Apex Mutual", activeLiens: 182, lienBalance: 1125842.5, percentOfTotal: 23.5, avatarClassName: "bg-blue-100 text-blue-700" },
  { id: "nova-care", initials: "NC", name: "Nova Care", activeLiens: 132, lienBalance: 687421.88, percentOfTotal: 14.4, avatarClassName: "bg-purple-100 text-purple-700" },
  { id: "summit-ins", initials: "SI", name: "Summit Ins.", activeLiens: 98, lienBalance: 456218.33, percentOfTotal: 9.5, avatarClassName: "bg-green-100 text-green-700" },
  { id: "beacon-life", initials: "BL", name: "Beacon Life", activeLiens: 76, lienBalance: 321775.19, percentOfTotal: 6.7, avatarClassName: "bg-orange-100 text-orange-700" },
  { id: "vanguard", initials: "VG", name: "Vanguard", activeLiens: 64, lienBalance: 289114.22, percentOfTotal: 6.0, avatarClassName: "bg-pink-100 text-pink-700" },
];

const topBuyersColumns: ColumnDef<TopBuyerRow, any>[] = [
  {
    id: "#",
    header: "#",
    cell: ({ row }) => (
      <span className="text-xs text-gray-500">{row.index + 1}</span>
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
        <span className="text-sm font-medium text-gray-800">
          {row.original.name}
        </span>
      </span>
    ),
  },
  {
    id: "activeLiens",
    header: "Active Liens",
    cell: ({ row }) => (
      <span className="text-xs text-gray-700">{row.original.activeLiens}</span>
    ),
  },
  {
    id: "lienBalance",
    header: "Lien Balance",
    cell: ({ row }) => (
      <span className="text-xs text-gray-700">
        {formatUsd(row.original.lienBalance)}
      </span>
    ),
  },
  {
    id: "percentOfTotal",
    header: "% of Total",
    cell: ({ row }) => (
      <span className="text-xs text-gray-700">
        {row.original.percentOfTotal.toFixed(1)}%
      </span>
    ),
  },
];

type AgingStatus = "High" | "Medium" | "Low";

interface AgingByBuyerRow {
  id: string;
  fundingCompany: string;
  d0_30: number;
  d31_60: number;
  d61_90: number;
  d91_120: number;
  d120Plus: number;
  total: number;
  pastDuePercent: number;
  status: AgingStatus;
}

// d0_30 is set so each row's five buckets sum exactly to `total` (which is kept in
// sync with the matching company's `lienBalance` in topBuyers above) — the source
// figures were off by anywhere from $0.31 to ~$1,000 depending on the row.
const agingByBuyer: AgingByBuyerRow[] = [
  { id: "apex-mutual", fundingCompany: "Apex Mutual", d0_30: 412512.31, d31_60: 298451.23, d61_90: 221114.55, d91_120: 112662.11, d120Plus: 81102.3, total: 1125842.5, pastDuePercent: 17.1, status: "High" },
  { id: "nova-care", fundingCompany: "Nova Care", d0_30: 211541.23, d31_60: 156882.21, d61_90: 118442.33, d91_120: 74551.12, d120Plus: 126004.99, total: 687421.88, pastDuePercent: 29.1, status: "High" },
  { id: "summit-ins", fundingCompany: "Summit Ins.", d0_30: 166331.14, d31_60: 108992.77, d61_90: 76551.88, d91_120: 53221.99, d120Plus: 51120.55, total: 456218.33, pastDuePercent: 22.8, status: "Medium" },
  { id: "beacon-life", fundingCompany: "Beacon Life", d0_30: 99772.33, d31_60: 72441.11, d61_90: 54002.19, d91_120: 38441.77, d120Plus: 57117.79, total: 321775.19, pastDuePercent: 29.7, status: "High" },
  { id: "vanguard", fundingCompany: "Vanguard", d0_30: 76221.31, d31_60: 54883.12, d61_90: 41667.22, d91_120: 29561.88, d120Plus: 86780.69, total: 289114.22, pastDuePercent: 40.3, status: "High" },
];

const AGING_STATUS_STYLES: Record<AgingStatus, string> = {
  High: "bg-red-100 text-red-600",
  Medium: "bg-amber-100 text-amber-700",
  Low: "bg-green-100 text-green-600",
};

// Tight padding — 9 columns of currency data need to fit without triggering
// horizontal scroll at the card's normal width, unlike the default px-4 cells.
const DENSE_CELL_META = { headerClassName: "px-2", cellClassName: "px-2" };

function agingCurrencyColumn(
  id: string,
  header: string,
  accessor: (row: AgingByBuyerRow) => number,
): ColumnDef<AgingByBuyerRow, any> {
  return {
    id,
    header,
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className="text-xs text-gray-700">
        {formatUsd(accessor(row.original))}
      </span>
    ),
  };
}

const agingColumns: ColumnDef<AgingByBuyerRow, any>[] = [
  {
    id: "fundingCompany",
    header: "Funding Company",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className="text-xs font-medium text-gray-800">
        {row.original.fundingCompany}
      </span>
    ),
  },
  agingCurrencyColumn("0-30", "0 - 30 Days", (r) => r.d0_30),
  agingCurrencyColumn("31-60", "31 - 60 Days", (r) => r.d31_60),
  agingCurrencyColumn("61-90", "61 - 90 Days", (r) => r.d61_90),
  agingCurrencyColumn("91-120", "91 - 120 Days", (r) => r.d91_120),
  agingCurrencyColumn("120+", "120+ Days", (r) => r.d120Plus),
  {
    id: "total",
    header: "Total",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className="text-xs font-medium text-gray-900">
        {formatUsd(row.original.total)}
      </span>
    ),
  },
  {
    id: "pastDue",
    header: "Past Due %",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className="text-xs text-gray-700">
        {row.original.pastDuePercent.toFixed(1)}%
      </span>
    ),
  },
  {
    id: "status",
    header: "Status",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span
        className={`inline-flex items-center rounded-full px-2 py-1 text-xs font-medium ${AGING_STATUS_STYLES[row.original.status]}`}
      >
        {row.original.status}
      </span>
    ),
  },
];

export default function SellingDashboardPage() {
  const { session } = useSession();
  const displayName = session?.orgName || session?.email?.split("@")[0] || "";
  const [dashboardRange, setDashboardRange] = useState<DateRangeValue>({});
  const periodLabel = formatPeriodLabel(dashboardRange) ?? "Apr 1 – Apr 30";

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
            onChange={setDashboardRange}
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
          total={arAgingSegments.reduce((sum, s) => sum + s.value, 0)}
          segments={arAgingSegments}
          statsType="A/R:"
          totalStats={3842196.18}
          centerValue="$3.8M"
          centerLabel="Total A/R"
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
          />
        </Card>
      </div>

      <div>
        <Card title="Aging by Lien Buyer" icon="ri-draggable" className="px-3">
          <BaseTable
            data={agingByBuyer}
            columns={agingColumns}
            getRowId={(r) => r.id}
            enableSorting={false}
            enablePagination={false}
            emptyMessage="No aging data to show."
            className="bg-white border-none w-full p-0"
          />
        </Card>
      </div>
    </div>
  );
}
