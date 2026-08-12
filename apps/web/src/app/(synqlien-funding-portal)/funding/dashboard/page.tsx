import Link from "next/link";
import { ArrowRight, TrendingDown, TrendingUp } from "lucide-react";
import { CustomDateRangeForm } from "@/components/synqlien-funding-portal/custom-date-range-form";
import {
  formatFundingCurrency,
  formatFundingDateTime,
  formatFundingNumber,
  formatFundingPercent,
  getFundingDashboard,
  statusBadgeClass,
  type AcquisitionPipelineStage,
  type FundingDashboardRange,
  type FundingMetricKey,
  type FundingMetricTrend,
  type PendingFundingOfferRow,
  type SynqLienFundingDashboard,
} from "@/lib/synqlien-funding-portal";

export const dynamic = "force-dynamic";

interface DashboardPageProps {
  searchParams: Promise<{
    range?: string;
    from?: string;
    to?: string;
  }>;
}

const RANGE_LABELS: Record<FundingDashboardRange, string> = {
  last7Days: "Last 7 Days",
  last30Days: "Last 30 Days",
  custom: "Custom",
};

const RANGE_OPTIONS: FundingDashboardRange[] = ["last7Days", "last30Days", "custom"];
const DATE_PARAM_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export default async function SynqLienFundingDashboardPage({
  searchParams,
}: DashboardPageProps) {
  const sp = await searchParams;
  const range = parseRange(sp.range);
  const from = parseDateParam(sp.from);
  const to = parseDateParam(sp.to);
  const today = getTodayDateParam();
  const dashboard = await getFundingDashboard({
    range,
    from,
    to,
  });

  return (
    <div className="w-full space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-[28px] font-semibold leading-9 tracking-normal text-[#0a0a0a]">
            Dashboard
          </h1>
          <p className="mt-1 max-w-[760px] text-[14px] font-normal leading-[1.6] text-[#737373]">
            Manage and monitor lien offers submitted to your company. Review opportunities, track activity, and take action from one centralized dashboard.
          </p>
        </div>
        <Link
          href="/funding/offered-liens?status=Pending"
          className="hidden h-[38px] shrink-0 items-center justify-center rounded-[10px] bg-[#ee7132] px-4 text-[14px] font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-[#d86228] sm:inline-flex"
          aria-label="Open Offer Inbox"
        >
          Offer Inbox
        </Link>
      </div>

      <KpiGrid dashboard={dashboard} />

      <div className="grid gap-6 xl:grid-cols-2">
        <PendingOffersCard rows={dashboard.pendingOffers} />
        <PipelineCard
          range={range}
          from={from}
          to={to}
          defaultDate={today}
          stages={dashboard.pipelineStages}
        />
      </div>

      <OfferInboxCard />
    </div>
  );
}

function KpiGrid({ dashboard }: { dashboard: SynqLienFundingDashboard }) {
  const { summary } = dashboard;
  const trends = summary.trends ?? {};
  const cards: Array<{
    key: FundingMetricKey;
    label: string;
    value: string;
  }> = [
    {
      key: "totalLienPending",
      label: "Total Lien Pending",
      value: formatFundingNumber(summary.totalLienPendingCount),
    },
    {
      key: "totalPendingOffered",
      label: "Total Pending Offered",
      value: formatFundingCurrency(summary.totalPendingOfferedAmount),
    },
    {
      key: "purchasedLiens",
      label: "Purchased Liens",
      value: formatFundingNumber(summary.purchasedLienCount),
    },
    {
      key: "capitalDeployed",
      label: "Capital Deployed",
      value: formatFundingCurrency(summary.capitalDeployedAmount),
    },
  ];

  return (
    <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
      {cards.map(card => (
        <section
          key={card.key}
          className="min-h-[138px] rounded-[16px] border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.08)]"
        >
          <div className="flex items-start justify-between gap-3">
            <p className="text-[14px] font-normal leading-[1.6] text-[#737373]">
              {card.label}
            </p>
            <TrendPill trend={trends[card.key] ?? null} />
          </div>
          <p className="mt-1 text-[24px] font-semibold leading-8 tracking-normal text-[#0a0a0a]">
            {card.value}
          </p>
          <TrendSummary trend={trends[card.key] ?? null} />
        </section>
      ))}
    </div>
  );
}

function TrendPill({ trend }: { trend?: FundingMetricTrend | null }) {
  if (!trend) return null;

  const tone =
    trend.direction === "down"
      ? "bg-[#fee2e2] text-[#b91c1c]"
      : trend.direction === "flat"
        ? "bg-[#f5f5f5] text-[#525252]"
        : "bg-[#dcfce7] text-[#15803d]";

  return (
    <span className={`inline-flex h-6 items-center gap-0.5 rounded-full px-2 text-[12px] font-medium leading-[1.6] ${tone}`}>
      <TrendIcon direction={trend.direction} className="h-3 w-3 shrink-0" />
      {formatTrendPercent(trend)}
    </span>
  );
}

function TrendSummary({ trend }: { trend?: FundingMetricTrend | null }) {
  if (!trend) return <div className="mt-4 h-10" aria-hidden />;

  const verb =
    trend.direction === "down"
      ? "Trending down this month"
      : trend.direction === "flat"
        ? "Stable this month"
        : "Trending up this month";
  const directionLabel =
    trend.direction === "down"
      ? "Down"
      : trend.direction === "flat"
        ? "No change"
        : "Up";

  return (
    <div className="mt-4 space-y-1">
      <p className="text-[12px] font-semibold leading-[1.6] text-[#0a0a0a]">
        {verb} <TrendIcon direction={trend.direction} className="ml-1 inline-block h-3.5 w-3.5 align-[-2px]" />
      </p>
      <p className="text-[12px] font-normal leading-[1.6] text-[#737373]">
        {directionLabel} {formatFundingPercent(Math.abs(trend.value))}
        {trend.label ? ` ${trend.label}` : ""}
      </p>
    </div>
  );
}

function TrendIcon({
  direction,
  className,
}: {
  direction: FundingMetricTrend["direction"];
  className?: string;
}) {
  const Icon = direction === "down" ? TrendingDown : direction === "flat" ? ArrowRight : TrendingUp;

  return <Icon aria-hidden className={className} strokeWidth={2.25} />;
}

function PendingOffersCard({ rows }: { rows: PendingFundingOfferRow[] }) {
  const visibleRows = rows.slice(0, 5);

  return (
    <section className="rounded-[16px] border border-[#e5e5e5] bg-white shadow-[0_1px_1.5px_rgba(0,0,0,0.08)]">
      <div className="flex min-h-[53px] items-center justify-between gap-3 px-6 py-4">
        <h2 className="text-[16px] font-semibold leading-5 text-[#0a0a0a]">
          Pending Offers
        </h2>
        <Link
          href="/funding/offered-liens?status=Pending"
          className="inline-flex h-[38px] items-center gap-3 rounded-[10px] border border-[#e5e5e5] bg-white px-4 text-[14px] font-medium leading-[1.6] text-[#0a0a0a] shadow-[0_1px_2px_rgba(0,0,0,0.08)] transition-colors hover:border-[#f4a076] hover:text-[#ee7132]"
        >
          View All
          <i className="ri-arrow-right-line text-[16px]" />
        </Link>
      </div>

      {visibleRows.length === 0 ? (
        <EmptyState icon="ri-inbox-line" title="No pending offers" />
      ) : (
        <ul className="divide-y divide-[#e5e5e5] px-6">
          {visibleRows.map(row => {
            const sellerCompany = row.sellerCompany?.trim() || row.sellerName || "Seller company unavailable";
            const sellerName = row.sellerName?.trim() || "Seller unavailable";

            return (
              <li key={row.id} className="flex min-h-[86px] items-center justify-between gap-5 py-3">
                <div className="min-w-0">
                  <StatusBadge status={row.status} size="sm" />
                  <p className="mt-2 truncate text-[14px] font-medium leading-[1.6] text-[#0a0a0a]">
                    {sellerCompany}
                  </p>
                  <p className="truncate text-[12px] font-normal leading-[1.6] text-[#737373]">
                    {sellerName}
                  </p>
                </div>
                <div className="shrink-0 text-right">
                  <p className="text-[14px] font-medium leading-[1.6] text-[#0a0a0a]">
                    {formatFundingCurrency(row.offeredAmount)}
                  </p>
                  <p className="mt-1 text-[12px] font-normal leading-[1.6] text-[#737373]">
                    {formatFundingDateTime(row.receivedAtUtc)}
                  </p>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function PipelineCard({
  range,
  from,
  to,
  defaultDate,
  stages,
}: {
  range: FundingDashboardRange;
  from?: string;
  to?: string;
  defaultDate: string;
  stages: AcquisitionPipelineStage[];
}) {
  const waitingForCustomRange = range === "custom" && (!from || !to);
  const visibleStages = waitingForCustomRange ? [] : stages;
  const totalCount = visibleStages.reduce((sum, stage) => sum + stage.count, 0);

  return (
    <section className="rounded-[16px] border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.08)]">
      <h2 className="text-[16px] font-semibold leading-5 text-[#0a0a0a]">
        Acquisition Pipeline
      </h2>
      <RangeTabs range={range} from={from} to={to} defaultDate={defaultDate} />

      {waitingForCustomRange ? (
        <div className="mt-8 rounded-[10px] border border-dashed border-[#e5e5e5] bg-[#fafafa] px-4 py-8 text-center">
          <p className="text-[14px] font-medium leading-[1.6] text-[#525252]">
            Select a start and end date to view pipeline data.
          </p>
        </div>
      ) : (
        <>
          <div className="mt-8 flex items-center justify-between gap-3">
            <p className="text-[24px] font-semibold leading-8 text-[#0a0a0a]">Total:</p>
            <p className="text-[24px] font-semibold leading-8 text-[#0a0a0a]">
              {formatFundingNumber(totalCount)}
            </p>
          </div>

          {visibleStages.length === 0 ? (
            <EmptyState icon="ri-bar-chart-horizontal-line" title="No pipeline activity for this range" compact />
          ) : (
            <div className="mt-7 space-y-8">
              {visibleStages.map(stage => (
                <PipelineStageRow key={stage.key} stage={stage} totalCount={totalCount} />
              ))}
            </div>
          )}
        </>
      )}
    </section>
  );
}

function RangeTabs({
  range,
  from,
  to,
  defaultDate,
}: {
  range: FundingDashboardRange;
  from?: string;
  to?: string;
  defaultDate: string;
}) {
  return (
    <>
      <div className="mt-4 grid h-9 grid-cols-3 overflow-hidden rounded-[8px] bg-[#f5f5f5] p-px">
        {RANGE_OPTIONS.map(item => {
          const active = range === item;
          return (
            <Link
              key={item}
              href={buildDashboardHref(item, from, to)}
              className={`flex items-center justify-center rounded-[7px] text-[12px] font-medium leading-[1.6] transition-colors ${
                active
                  ? "border border-[#e5e5e5] bg-white text-[#0a0a0a] shadow-[0_1px_1px_rgba(0,0,0,0.08)]"
                  : "text-[#0a0a0a] hover:bg-white/70"
              }`}
            >
              {RANGE_LABELS[item]}
            </Link>
          );
        })}
      </div>

      {range === "custom" ? <CustomDateRangeForm from={from} to={to} defaultDate={defaultDate} /> : null}
    </>
  );
}

function PipelineStageRow({
  stage,
  totalCount,
}: {
  stage: AcquisitionPipelineStage;
  totalCount: number;
}) {
  const percent = totalCount > 0 ? (stage.count / totalCount) * 100 : 0;
  const tone = getPipelineTone(stage.label);

  return (
    <div>
      <div className="mb-3 flex items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] ${tone.iconBg}`}>
            <i className={`${tone.icon} text-[22px] leading-none ${tone.iconText}`} />
          </span>
          <p className="truncate text-[16px] font-medium leading-5 text-[#0a0a0a]">
            {stage.label}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2 text-[16px] font-medium leading-5">
          <p className="text-[#0a0a0a]">{formatFundingNumber(stage.count)}</p>
          <p className="text-[#737373]">({formatFundingPercent(percent)})</p>
        </div>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-[#f5f5f5]">
        <div
          className={`h-2 rounded-full ${tone.bar}`}
          style={{ width: `${Math.min(100, Math.max(0, percent))}%` }}
        />
      </div>
    </div>
  );
}

function OfferInboxCard() {
  return (
    <Link
      href="/funding/offered-liens?status=Pending"
      className="flex rounded-[16px] border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.08)] transition-colors hover:border-[#f4a076]"
    >
      <div className="flex w-full items-start gap-6">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[10px] bg-[#f5f5f5] text-[#0a0a0a]">
          <i className="ri-mail-line text-[24px]" />
        </span>
        <div className="min-w-0">
          <h2 className="text-[16px] font-semibold leading-5 text-[#0a0a0a]">
            Offer Inbox
          </h2>
          <p className="mt-2 text-[14px] font-normal leading-[1.6] text-[#737373]">
            Review and accept incoming lien offers.
          </p>
        </div>
      </div>
    </Link>
  );
}

function EmptyState({
  icon,
  title,
  compact = false,
}: {
  icon: string;
  title: string;
  compact?: boolean;
}) {
  return (
    <div className={`flex flex-col items-center justify-center text-center ${compact ? "py-6" : "py-12"}`}>
      <span className="flex h-10 w-10 items-center justify-center rounded-[8px] bg-[#f5f5f5] text-[#737373]">
        <i className={`${icon} text-[19px]`} />
      </span>
      <p className="mt-3 text-[14px] font-medium leading-[1.6] text-[#525252]">{title}</p>
    </div>
  );
}

function StatusBadge({
  status,
  size = "md",
}: {
  status: string;
  size?: "sm" | "md";
}) {
  return (
    <span className={`inline-flex rounded-full font-medium ring-1 ${size === "sm" ? "px-2 py-0.5 text-[12px]" : "px-3 py-1 text-[14px]"} ${statusBadgeClass(status)}`}>
      {status}
    </span>
  );
}

function formatTrendPercent(trend: FundingMetricTrend): string {
  return formatFundingPercent(Math.abs(trend.value));
}

function getPipelineTone(label: string): {
  icon: string;
  iconBg: string;
  iconText: string;
  bar: string;
} {
  const normalized = label.trim().toLowerCase();
  if (normalized.includes("accepted") || normalized.includes("purchased")) {
    return {
      icon: "ri-checkbox-circle-line",
      iconBg: "bg-[#f5f5f5]",
      iconText: "text-[#15803d]",
      bar: "bg-[#22c55e]",
    };
  }
  if (normalized.includes("declined") || normalized.includes("expired")) {
    return {
      icon: "ri-close-circle-line",
      iconBg: "bg-[#f5f5f5]",
      iconText: "text-[#ef4444]",
      bar: "bg-[#ef4444]",
    };
  }
  return {
    icon: "ri-time-line",
    iconBg: "bg-[#f5f5f5]",
    iconText: "text-[#a16207]",
    bar: "bg-[#eab308]",
  };
}

function buildDashboardHref(range: FundingDashboardRange, from?: string, to?: string): string {
  const params = new URLSearchParams({ range });
  if (range === "custom") {
    if (from) params.set("from", from);
    if (to) params.set("to", to);
  }
  return `/funding/dashboard?${params.toString()}`;
}

function parseRange(value?: string): FundingDashboardRange {
  if (value === "last7Days" || value === "custom") return value;
  return "last30Days";
}

function parseDateParam(value?: string): string | undefined {
  if (!value || !DATE_PARAM_PATTERN.test(value)) return undefined;

  const date = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return undefined;

  return date.toISOString().slice(0, 10) === value ? value : undefined;
}

function getTodayDateParam(): string {
  const now = new Date();
  const localDate = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 10);
}
