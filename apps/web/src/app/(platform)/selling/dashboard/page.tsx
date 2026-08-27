"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSession } from "@/hooks/use-session";
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
import {
  TABLE_CELL_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";
import {
  liensService,
  type SellingOperationsAgingBucket,
  type SellingOperationsBuyerAgingItem,
  type SellingOperationsMetric,
  type SellingOperationsStatusItem,
  type SellingOperationsTopBuyerItem,
} from "@/lib/selling";

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

// Shared blue/orange/green/yellow/red palette used across both summary donuts on this page.
const SUMMARY_COLORS = ["#3b82f6", "#f97316", "#22c55e", "#eab308", "#ef4444"];

// Generic copy for any analytics section the backend reports as unavailable
// (isAvailable: false). The API's unavailableReason is internal engineering
// detail (e.g. "no due date persisted") — never render it verbatim to users.
const UNAVAILABLE_FEATURE_MESSAGE = "Coming soon.";

// Rotating avatar background classes for the top-buyers table.
const AVATAR_CLASSNAMES = [
  "bg-blue-100 text-blue-700",
  "bg-orange-100 text-orange-700",
  "bg-green-100 text-green-700",
  "bg-yellow-100 text-yellow-700",
  "bg-red-100 text-red-700",
];

const MONTH_LABEL_FORMATTER = new Intl.DateTimeFormat("en-US", {
  month: "short",
  year: "2-digit",
});

const SHORT_DATE_FORMATTER = new Intl.DateTimeFormat("en-US", {
  month: "short",
  day: "numeric",
});

function formatShortDate(dateOnly: string): string {
  return SHORT_DATE_FORMATTER.format(new Date(`${dateOnly}T00:00:00`));
}

function buyerInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

interface TopBuyerRow {
  id: string;
  initials: string;
  name: string;
  activeLiens: number;
  lienBalance: number;
  percentOfTotal: number;
  avatarClassName: string;
}

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

interface BuyerAgingRow {
  id: string;
  buyerName: string;
  days0To30: number;
  days31To60: number;
  days61To90: number;
  days91To120: number;
  moreThan120: number;
  total: number;
  pastDuePercent: number | null;
}

// Bucket labels aren't a fixed enum on the wire, so buckets are matched by the
// day-range embedded in their label (e.g. "1-30", "0-30 Days", "120+") rather
// than an exact string, since the backend's bucket-naming convention isn't
// pinned down while ArAging/BuyerAging are still stub responses.
const BUCKET_RANGES: { key: keyof BuyerAgingRow; low: number; high: number }[] = [
  { key: "days0To30", low: 0, high: 30 },
  { key: "days31To60", low: 31, high: 60 },
  { key: "days61To90", low: 61, high: 90 },
  { key: "days91To120", low: 91, high: 120 },
  { key: "moreThan120", low: 120, high: Infinity },
];

function parseBucketRange(bucket: string): [number, number] | null {
  const plusMatch = bucket.match(/(\d+)\s*\+/);
  if (plusMatch) return [Number(plusMatch[1]), Infinity];
  const rangeMatch = bucket.match(/(\d+)\D+(\d+)/);
  if (rangeMatch) return [Number(rangeMatch[1]), Number(rangeMatch[2])];
  return null;
}

function bucketAmounts(
  buckets: SellingOperationsAgingBucket[],
): Partial<Record<keyof BuyerAgingRow, number>> {
  const result: Partial<Record<keyof BuyerAgingRow, number>> = {};
  for (const b of buckets) {
    const range = parseBucketRange(b.bucket);
    if (!range) continue;
    const [lo, hi] = range;
    const match = BUCKET_RANGES.find(
      (r) => Math.abs(r.low - lo) <= 1 && (r.high === Infinity ? hi === Infinity : Math.abs(r.high - hi) <= 1),
    );
    if (match) result[match.key] = b.amount;
  }
  return result;
}

// The API doesn't send a severity — derived client-side from pastDuePercent.
function pastDueStatus(pastDuePercent: number | null): { label: string; className: string } | null {
  if (pastDuePercent == null) return null;
  if (pastDuePercent >= 25) return { label: "High", className: "bg-red-100 text-red-700" };
  if (pastDuePercent >= 10) return { label: "Medium", className: "bg-yellow-100 text-yellow-700" };
  return { label: "Low", className: "bg-green-100 text-green-700" };
}

function agingCurrencyColumn(
  id: string,
  header: string,
  accessor: (row: BuyerAgingRow) => number,
): ColumnDef<BuyerAgingRow, any> {
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

const buyerAgingColumns: ColumnDef<BuyerAgingRow, any>[] = [
  {
    id: "fundingCompany",
    header: "Funding Company",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>{row.original.buyerName}</span>
    ),
  },
  agingCurrencyColumn("0-30", "0 - 30 Days", (r) => r.days0To30),
  agingCurrencyColumn("31-60", "31 - 60 Days", (r) => r.days31To60),
  agingCurrencyColumn("61-90", "61 - 90 Days", (r) => r.days61To90),
  agingCurrencyColumn("91-120", "91 - 120 Days", (r) => r.days91To120),
  agingCurrencyColumn("120+", "120+ Days", (r) => r.moreThan120),
  agingCurrencyColumn("total", "Total", (r) => r.total),
  {
    id: "pastDuePercent",
    header: "Past Due %",
    meta: DENSE_CELL_META,
    cell: ({ row }) => (
      <span className={TABLE_CELL_CLASSNAME}>
        {row.original.pastDuePercent != null
          ? `${row.original.pastDuePercent.toFixed(1)}%`
          : "—"}
      </span>
    ),
  },
  {
    id: "status",
    header: "Status",
    meta: DENSE_CELL_META,
    cell: ({ row }) => {
      const status = pastDueStatus(row.original.pastDuePercent);
      if (!status) return <span className={TABLE_CELL_CLASSNAME}>—</span>;
      return (
        <span
          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${status.className}`}
        >
          {status.label}
        </span>
      );
    },
  },
];

export default function SellingDashboardPage() {
  const { session } = useSession();
  const displayName = session?.orgName || session?.email?.split("@")[0] || "";
  const [dashboardRange, setDashboardRange] = useState<DateRangeValue>({});

  const hasCustomRange = Boolean(dashboardRange.from && dashboardRange.to);
  const { data: analyticsDashboard, isPending: isAnalyticsPending } = useQuery({
    queryKey: [
      "selling-analytics-dashboard",
      hasCustomRange ? dashboardRange.from : null,
      hasCustomRange ? dashboardRange.to : null,
    ],
    queryFn: () =>
      liensService.getAnalyticsDashboard({
        startDate: hasCustomRange ? dashboardRange.from : undefined,
        endDate: hasCustomRange ? dashboardRange.to : undefined,
        compare: "previousPeriod",
      }),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });

  const arAgingSegments = useMemo<Segment[]>(() => {
    const amounts = bucketAmounts(analyticsDashboard?.arAging.buckets ?? []);
    return [
      { label: "1-30 Days", value: amounts.days0To30 ?? 0, color: SUMMARY_COLORS[0] },
      { label: "31-60 Days", value: amounts.days31To60 ?? 0, color: SUMMARY_COLORS[1] },
      { label: "61-90 Days", value: amounts.days61To90 ?? 0, color: SUMMARY_COLORS[2] },
      { label: "91-120 Days", value: amounts.days91To120 ?? 0, color: SUMMARY_COLORS[3] },
      { label: "120+ Days", value: amounts.moreThan120 ?? 0, color: SUMMARY_COLORS[4] },
    ];
  }, [analyticsDashboard]);
  const totalAr = analyticsDashboard?.arAging.total ?? 0;

  const lienStatusSegments = useMemo<Segment[]>(() => {
    const statuses = analyticsDashboard?.lienStatuses ?? [];
    return statuses.map((s: SellingOperationsStatusItem, i) => ({
      label: s.status,
      value: s.lienCount,
      color: SUMMARY_COLORS[i % SUMMARY_COLORS.length],
    }));
  }, [analyticsDashboard]);
  const totalLienCount = lienStatusSegments.reduce((sum, s) => sum + s.value, 0);

  const liensOverTimeData = useMemo<LiensOverTimePoint[]>(() => {
    const points = analyticsDashboard?.timeSeries ?? [];
    const mapped = points.map((p) => ({
      month: MONTH_LABEL_FORMATTER.format(new Date(`${p.bucketStart}T00:00:00`)),
      value: p.lienRevenue,
    }));
    // A single bucket renders as an isolated dot with no line, so we prepend a
    // zero-value point for the prior month purely to give the chart a line to draw.
    if (mapped.length === 1) {
      const soleBucketDate = new Date(`${points[0].bucketStart}T00:00:00`);
      const priorMonthDate = new Date(soleBucketDate);
      priorMonthDate.setMonth(priorMonthDate.getMonth() - 1);
      return [
        { month: MONTH_LABEL_FORMATTER.format(priorMonthDate), value: 0 },
        ...mapped,
      ];
    }
    return mapped;
  }, [analyticsDashboard]);

  const topBuyers = useMemo<TopBuyerRow[]>(() => {
    const buyers = analyticsDashboard?.topBuyers ?? [];
    return buyers.map((b: SellingOperationsTopBuyerItem, i) => ({
      id: b.buyerOrgId,
      initials: buyerInitials(b.buyerName),
      name: b.buyerName,
      activeLiens: b.activeLienCount,
      lienBalance: b.totalBalance,
      percentOfTotal: b.percentOfTotalBalance,
      avatarClassName: AVATAR_CLASSNAMES[i % AVATAR_CLASSNAMES.length],
    }));
  }, [analyticsDashboard]);

  const buyerAgingRows = useMemo<BuyerAgingRow[]>(() => {
    const items = analyticsDashboard?.buyerAging?.items ?? [];
    return items.map((item: SellingOperationsBuyerAgingItem) => {
      const amounts = bucketAmounts(item.buckets);
      return {
        id: item.buyerOrgId,
        buyerName: item.buyerName,
        days0To30: amounts.days0To30 ?? 0,
        days31To60: amounts.days31To60 ?? 0,
        days61To90: amounts.days61To90 ?? 0,
        days91To120: amounts.days91To120 ?? 0,
        moreThan120: amounts.moreThan120 ?? 0,
        total: item.total,
        pastDuePercent: item.pastDuePercent,
      };
    });
  }, [analyticsDashboard]);

  const periodLabel = useMemo(() => {
    const period = analyticsDashboard?.period;
    if (!period) return "";
    return `${formatShortDate(period.startDate)} – ${formatShortDate(period.endDate)}`;
  }, [analyticsDashboard]);

  const metrics = analyticsDashboard?.metrics;
  const metricCardProps = (metric: SellingOperationsMetric | undefined) => {
    if (!metric) {
      return { value: 0 };
    }
    if (!metric.isAvailable || metric.value == null) {
      return { unavailable: true };
    }
    if (metric.changePercent == null) {
      return { value: metric.value };
    }
    const trend: "up" | "down" = metric.changePercent >= 0 ? "up" : "down";
    const percent = Math.abs(Math.round(metric.changePercent * 10) / 10);
    return {
      value: metric.value,
      trend,
      statsPercentage: percent,
      trendDescription: trend === "up" ? "Trending up this month " : "Trending down this month ",
      description: `${trend === "up" ? "Up" : "Down"} ${percent}%${periodLabel ? ` vs ${periodLabel}` : ""}`,
    };
  };

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

      {/* Past Amount Due shows "Coming soon" via metricCardProps' unavailable
          branch — the analytics endpoint (SellingOperationsDashboardService.
          GetAsync) always returns Metrics.PastAmountDue as IsAvailable=false
          today because Lien has no persisted due date to compute past-due
          against (internal reason from the API, not shown to users: "Lien
          receivables do not currently persist a due date..."). It'll switch
          to real data automatically once the backend starts sending
          IsAvailable=true — see "Aging by Lien Buyer" below for the same
          blocker. */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <MetricCard
          label="Total Lien Revenue"
          formatAsCurrency={true}
          {...metricCardProps(metrics?.totalLienRevenue)}
        />
        <MetricCard
          label="Total Outstanding"
          formatAsCurrency={true}
          {...metricCardProps(metrics?.totalOutstanding)}
        />
        <MetricCard
          label="Past Amount Due"
          formatAsCurrency={true}
          {...metricCardProps(metrics?.pastAmountDue)}
        />
        <MetricCard
          label="Payments"
          formatAsCurrency={true}
          {...metricCardProps(metrics?.payments)}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <StatCard
          title="A/R Aging Summary"
          total={totalAr}
          segments={arAgingSegments}
          statsType="A/R:"
          totalStats={totalAr}
          centerValue={isAnalyticsPending ? "..." : formatCompactUsd(totalAr)}
          centerLabel="Total A/R"
          unavailableMessage={
            analyticsDashboard && !analyticsDashboard.arAging.isAvailable
              ? UNAVAILABLE_FEATURE_MESSAGE
              : undefined
          }
        />

        <StatCard
          title="Liens by Status"
          total={totalLienCount}
          segments={lienStatusSegments}
          statsType=""
          totalStats={totalLienCount}
          showHeaderStat={false}
          valueFormat="number"
          centerValue={totalLienCount.toLocaleString()}
          centerLabel="Total Liens"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-5">
        <Card
          title="Liens Over Time"
          subtitle="Revenue by month for the selected period"
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

      {/* TODO: buyerAgingRows will always be empty until the backend adds a
          persisted due date to Lien — SellingOperationsDashboardService
          returns BuyerAging.IsAvailable=false unconditionally today
          (internal reason from the API: "Lien receivables do not currently
          persist a due date, so past-due and A/R aging values cannot be
          calculated reliably."). That's implementation detail, not
          something to surface to end users, so the table shows a generic
          empty state instead — see UNAVAILABLE_FEATURE_MESSAGE. The
          column/bucket-matching logic below is ready for real data as soon
          as the API starts sending it — no frontend change needed. */}
      <Card title="Aging by Lien Buyer" icon="ri-draggable" className="px-3">
        <BaseTable
          data={buyerAgingRows}
          columns={buyerAgingColumns}
          getRowId={(r) => r.id}
          enableSorting={false}
          enablePagination={false}
          isLoading={isAnalyticsPending}
          emptyMessage={
            analyticsDashboard && !analyticsDashboard.buyerAging.isAvailable
              ? UNAVAILABLE_FEATURE_MESSAGE
              : "No aging data to show."
          }
          className="bg-white border-none w-full p-0"
          headerClassName={TABLE_HEADER_CLASSNAME}
          headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
        />
      </Card>
    </div>
  );
}
