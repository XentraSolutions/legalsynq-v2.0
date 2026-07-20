import Link from "next/link";
import {
  formatFundingCurrency,
  formatFundingDate,
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
  type ProviderPerformanceRow,
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

export default async function SynqLienFundingDashboardPage({
  searchParams,
}: DashboardPageProps) {
  const sp = await searchParams;
  const range = parseRange(sp.range);
  const dashboard = await getFundingDashboard({
    range,
    from: sp.from || undefined,
    to: sp.to || undefined,
  });

  return (
    <div className="mx-auto max-w-[1440px] space-y-5">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-orange-600">
            SynqLien Funding
          </p>
          <h1 className="mt-2 text-2xl font-semibold tracking-tight text-slate-950 sm:text-3xl">
            Dashboard
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Review offered liens, acquisition flow, and provider performance.
          </p>
        </div>
        <RangeControls range={range} from={sp.from} to={sp.to} />
      </div>

      <KpiGrid dashboard={dashboard} />

      <div className="grid gap-5 xl:grid-cols-[1.1fr_0.9fr]">
        <PendingOffersCard rows={dashboard.pendingOffers} />
        <PipelineCard range={range} stages={dashboard.pipelineStages} />
      </div>

      <div className="grid gap-5 xl:grid-cols-[1.35fr_0.65fr]">
        <ProviderPerformanceCard rows={dashboard.providerPerformance} />
        <OfferInboxCard dashboard={dashboard} />
      </div>
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
    sublabel: string;
    icon: string;
  }> = [
    {
      key: "totalLienPending",
      label: "Total Lien Pending",
      value: formatFundingCurrency(summary.totalLienPendingAmount),
      sublabel: `${formatFundingNumber(summary.totalLienPendingCount)} lien${summary.totalLienPendingCount === 1 ? "" : "s"}`,
      icon: "ri-time-line",
    },
    {
      key: "totalPendingOffered",
      label: "Total Pending Offered",
      value: formatFundingCurrency(summary.totalPendingOfferedAmount),
      sublabel: `${formatFundingNumber(summary.totalPendingOfferCount)} offer${summary.totalPendingOfferCount === 1 ? "" : "s"}`,
      icon: "ri-inbox-2-line",
    },
    {
      key: "purchasedLiens",
      label: "Purchased Liens",
      value: formatFundingNumber(summary.purchasedLienCount),
      sublabel: "Buyer-scoped acquisitions",
      icon: "ri-checkbox-circle-line",
    },
    {
      key: "capitalDeployed",
      label: "Capital Deployed",
      value: formatFundingCurrency(summary.capitalDeployedAmount),
      sublabel: "Accepted purchase value",
      icon: "ri-bank-card-line",
    },
  ];

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map(card => (
        <section
          key={card.key}
          className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm"
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-slate-500">{card.label}</p>
              <p className="mt-3 text-2xl font-semibold tracking-tight text-slate-950">
                {card.value}
              </p>
              <p className="mt-1 text-xs text-slate-400">{card.sublabel}</p>
            </div>
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-slate-50 text-slate-600">
              <i className={`${card.icon} text-[18px]`} />
            </span>
          </div>
          <TrendChip trend={trends[card.key] ?? null} />
        </section>
      ))}
    </div>
  );
}

function TrendChip({ trend }: { trend?: FundingMetricTrend | null }) {
  if (!trend) return null;

  const directionIcon =
    trend.direction === "up"
      ? "ri-arrow-up-line"
      : trend.direction === "down"
        ? "ri-arrow-down-line"
        : "ri-subtract-line";
  const tone =
    trend.direction === "up"
      ? "bg-emerald-50 text-emerald-700 ring-emerald-200"
      : trend.direction === "down"
        ? "bg-rose-50 text-rose-700 ring-rose-200"
        : "bg-slate-50 text-slate-600 ring-slate-200";

  return (
    <div className="mt-4">
      <span className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-medium ring-1 ${tone}`}>
        <i className={`${directionIcon} text-[13px]`} />
        {formatFundingPercent(trend.value)}
        {trend.label ? <span className="font-normal opacity-75">{trend.label}</span> : null}
      </span>
    </div>
  );
}

function PendingOffersCard({ rows }: { rows: PendingFundingOfferRow[] }) {
  return (
    <Card
      title="Pending Offers"
      description="Offers awaiting funding-company review."
      actionHref="/funding/offered-liens?status=Pending"
      actionLabel="View all"
    >
      {rows.length === 0 ? (
        <EmptyState icon="ri-inbox-line" title="No pending offers" />
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-100">
            <thead>
              <tr className="text-left text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                <th className="py-3 pr-4">Lien</th>
                <th className="px-4 py-3">Provider</th>
                <th className="px-4 py-3">Offer</th>
                <th className="px-4 py-3">Due</th>
                <th className="py-3 pl-4">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map(row => (
                <tr key={row.id}>
                  <td className="py-3 pr-4">
                    <div className="min-w-[150px]">
                      <p className="text-sm font-semibold text-slate-950">{row.lienNumber}</p>
                      <p className="mt-0.5 text-xs text-slate-400">{row.sellerName}</p>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-600">{row.providerName}</td>
                  <td className="px-4 py-3 text-sm font-medium text-slate-900">
                    {formatFundingCurrency(row.offeredAmount)}
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-500">
                    {formatFundingDate(row.responseDueAtUtc)}
                  </td>
                  <td className="py-3 pl-4">
                    <StatusBadge status={row.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function PipelineCard({
  range,
  stages,
}: {
  range: FundingDashboardRange;
  stages: AcquisitionPipelineStage[];
}) {
  const maxAmount = Math.max(0, ...stages.map(stage => stage.totalAmount));
  const totalAmount = stages.reduce((sum, stage) => sum + stage.totalAmount, 0);
  const hasStages = stages.length > 0;

  return (
    <Card
      title="Acquisition Pipeline"
      description={`Range: ${RANGE_LABELS[range]}`}
    >
      <div className="mb-4 flex items-center justify-between rounded-md bg-slate-50 px-3 py-2">
        <span className="text-xs font-medium text-slate-500">Total pipeline value</span>
        <span className="text-sm font-semibold text-slate-950">
          {formatFundingCurrency(totalAmount)}
        </span>
      </div>

      {hasStages ? (
        <div className="space-y-4">
          {stages.map(stage => (
            <PipelineStageRow key={stage.key} stage={stage} maxAmount={maxAmount} />
          ))}
        </div>
      ) : (
        <div className="space-y-4">
          <div>
            <div className="mb-2 flex items-center justify-between gap-3">
              <span className="text-sm font-medium text-slate-600">Total</span>
              <span className="text-sm font-semibold text-slate-950">$0</span>
            </div>
            <div className="h-2 rounded-full bg-slate-100">
              <div className="h-2 rounded-full bg-orange-500" style={{ width: "0%" }} />
            </div>
          </div>
          <EmptyState icon="ri-bar-chart-horizontal-line" title="No pipeline activity for this range" compact />
        </div>
      )}
    </Card>
  );
}

function PipelineStageRow({
  stage,
  maxAmount,
}: {
  stage: AcquisitionPipelineStage;
  maxAmount: number;
}) {
  const width = maxAmount > 0 ? Math.round((stage.totalAmount / maxAmount) * 100) : 0;
  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-slate-700">{stage.label}</p>
          <p className="mt-0.5 text-xs text-slate-400">
            {formatFundingNumber(stage.count)} lien{stage.count === 1 ? "" : "s"}
            {stage.conversionRatePercent != null ? ` - ${formatFundingPercent(stage.conversionRatePercent)} conversion` : ""}
          </p>
        </div>
        <p className="shrink-0 text-sm font-semibold text-slate-950">
          {formatFundingCurrency(stage.totalAmount)}
        </p>
      </div>
      <div className="h-2 rounded-full bg-slate-100">
        <div
          className="h-2 rounded-full bg-orange-500"
          style={{ width: `${width}%` }}
        />
      </div>
    </div>
  );
}

function ProviderPerformanceCard({ rows }: { rows: ProviderPerformanceRow[] }) {
  return (
    <Card title="Provider Performance" description="Buyer-facing provider metrics.">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-100">
          <thead>
            <tr className="text-left text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              <th className="py-3 pr-4">Provider</th>
              <th className="px-4 py-3">Liens</th>
              <th className="px-4 py-3">Offered</th>
              <th className="px-4 py-3">Accepted</th>
              <th className="py-3 pl-4">Avg Response</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rows.length === 0 ? (
              <tr>
                <td colSpan={5} className="py-10">
                  <EmptyState icon="ri-hospital-line" title="No provider performance data" compact />
                </td>
              </tr>
            ) : rows.map(row => (
              <tr key={row.providerId}>
                <td className="py-3 pr-4 text-sm font-medium text-slate-950">{row.providerName}</td>
                <td className="px-4 py-3 text-sm text-slate-600">{formatFundingNumber(row.lienCount)}</td>
                <td className="px-4 py-3 text-sm text-slate-600">{formatFundingCurrency(row.offeredAmount)}</td>
                <td className="px-4 py-3 text-sm text-slate-600">{formatFundingCurrency(row.acceptedAmount)}</td>
                <td className="py-3 pl-4 text-sm text-slate-600">
                  {row.averageResponseHours == null ? "-" : `${formatFundingNumber(row.averageResponseHours)}h`}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function OfferInboxCard({ dashboard }: { dashboard: SynqLienFundingDashboard }) {
  const pendingCount = dashboard.offerInbox?.pendingCount ?? dashboard.summary.totalPendingOfferCount;
  const unreadCount = dashboard.offerInbox?.unreadCount ?? null;

  return (
    <Card title="Offer Inbox" description="Funding opportunities requiring review.">
      <div className="rounded-lg border border-orange-100 bg-orange-50 p-4">
        <div className="flex items-start gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-white text-orange-600">
            <i className="ri-inbox-2-line text-[19px]" />
          </span>
          <div className="min-w-0">
            <p className="text-2xl font-semibold tracking-tight text-slate-950">
              {formatFundingNumber(pendingCount)}
            </p>
            <p className="mt-1 text-sm text-slate-600">
              Pending offer{pendingCount === 1 ? "" : "s"}
            </p>
            {unreadCount != null ? (
              <p className="mt-1 text-xs text-slate-500">
                {formatFundingNumber(unreadCount)} unread
              </p>
            ) : null}
          </div>
        </div>
      </div>

      <div className="mt-4 space-y-3 text-sm">
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-3">
          <span className="text-slate-500">Latest received</span>
          <span className="font-medium text-slate-900">
            {formatFundingDateTime(dashboard.offerInbox?.latestReceivedAtUtc)}
          </span>
        </div>
        <Link
          href="/funding/offered-liens?status=Pending"
          className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-md bg-slate-950 px-4 text-sm font-medium text-white transition-colors hover:bg-slate-800"
        >
          Open Offer Inbox
          <i className="ri-arrow-right-line text-[15px]" />
        </Link>
      </div>
    </Card>
  );
}

function RangeControls({
  range,
  from,
  to,
}: {
  range: FundingDashboardRange;
  from?: string;
  to?: string;
}) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-2 shadow-sm">
      <div className="flex flex-col gap-2 lg:flex-row lg:items-center">
        <div className="grid grid-cols-2 gap-1 sm:flex">
          {(["last7Days", "last30Days"] as const).map(item => (
            <Link
              key={item}
              href={`/funding/dashboard?range=${item}`}
              className={`inline-flex h-9 items-center justify-center rounded-md px-3 text-sm font-medium transition-colors ${
                range === item
                  ? "bg-slate-950 text-white"
                  : "text-slate-600 hover:bg-slate-50 hover:text-slate-950"
              }`}
            >
              {RANGE_LABELS[item]}
            </Link>
          ))}
        </div>
        <form action="/funding/dashboard" className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <input type="hidden" name="range" value="custom" />
          <input
            type="date"
            name="from"
            defaultValue={from}
            className="h-9 rounded-md border border-slate-200 bg-white px-2 text-sm text-slate-700 outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
            aria-label="Custom range start"
          />
          <input
            type="date"
            name="to"
            defaultValue={to}
            className="h-9 rounded-md border border-slate-200 bg-white px-2 text-sm text-slate-700 outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
            aria-label="Custom range end"
          />
          <button
            type="submit"
            className={`inline-flex h-9 items-center justify-center rounded-md px-3 text-sm font-medium transition-colors ${
              range === "custom"
                ? "bg-slate-950 text-white"
                : "bg-slate-100 text-slate-700 hover:bg-slate-200"
            }`}
          >
            Custom
          </button>
        </form>
      </div>
    </div>
  );
}

function Card({
  title,
  description,
  actionHref,
  actionLabel,
  children,
}: {
  title: string;
  description?: string;
  actionHref?: string;
  actionLabel?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-slate-950">{title}</h2>
          {description ? <p className="mt-1 text-sm text-slate-500">{description}</p> : null}
        </div>
        {actionHref && actionLabel ? (
          <Link
            href={actionHref}
            className="shrink-0 text-sm font-medium text-orange-700 transition-colors hover:text-orange-800"
          >
            {actionLabel}
          </Link>
        ) : null}
      </div>
      {children}
    </section>
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
    <div className={`flex flex-col items-center justify-center text-center ${compact ? "py-4" : "py-12"}`}>
      <span className="flex h-10 w-10 items-center justify-center rounded-md bg-slate-50 text-slate-400">
        <i className={`${icon} text-[19px]`} />
      </span>
      <p className="mt-3 text-sm font-medium text-slate-600">{title}</p>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ring-1 ${statusBadgeClass(status)}`}>
      {status}
    </span>
  );
}

function parseRange(value?: string): FundingDashboardRange {
  if (value === "last7Days" || value === "custom") return value;
  return "last30Days";
}
