import type {
  MonthlyAgingReportResponse,
  SellingAgingBucket,
  SellingDashboardMetric,
} from '@/shared/api/endpoints/Liens';
import { SLICE_COLORS, type DonutSlice, type StatCardData } from '../dashboardShared';

export function formatSellingCurrency(value: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatSellingCompactCurrency(value: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(value);
}

export function mapSellingMetric(
  label: string,
  metric: SellingDashboardMetric,
  currency: string
): StatCardData {
  const change = metric.changePercent ?? 0;
  return {
    label,
    value:
      metric.isAvailable && metric.value != null
        ? formatSellingCurrency(metric.value, currency)
        : '—',
    trend: `${Math.abs(change).toFixed(1)}%`,
    trendTone: change < 0 ? 'negative' : 'positive',
  };
}

export function formatSellingBucketLabel(value: string, grain: string): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: grain === 'month' ? undefined : 'numeric',
    year: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(`${value}T00:00:00Z`));
}

export function resolveSellingAgingAsOfDate(endDate: string, now = new Date()): string {
  if (endDate) {
    const [month, day, year] = endDate.split('/');
    if (month && day && year) return `${year}-${month}-${day}`;
    return endDate;
  }

  return now.toISOString().slice(0, 10);
}

export function buildMonthlyAgingSlices(
  report: MonthlyAgingReportResponse | undefined
): DonutSlice[] {
  const totals = report?.summaryTotals;
  if (!totals) return [];

  return [
    { label: 'Days 1–30', value: totals.days1To30 },
    { label: 'Days 31–60', value: totals.days31To60 },
    { label: 'Days 61–90', value: totals.days61To90 },
    { label: 'Days 91–120', value: totals.days91To120 },
    { label: 'Days 121+', value: totals.moreThan120 },
  ]
    .filter((bucket) => bucket.value > 0)
    .map((bucket, index) => ({
      ...bucket,
      amount: formatSellingCurrency(bucket.value, report.currency),
      color: SLICE_COLORS[index % SLICE_COLORS.length],
    }));
}

export function formatSellingAgingPeriod(bucket: string): string {
  const normalized = bucket.trim().toLowerCase().replace(/\s+/g, '');
  const labels: Record<string, string> = {
    '0-30': 'Days 1–30',
    '1-30': 'Days 1–30',
    '31-60': 'Days 31–60',
    '61-90': 'Days 61–90',
    '91-120': 'Days 91–120',
    '120+': 'Days 121+',
    '121+': 'Days 121+',
    morethan120: 'Days 121+',
  };
  return labels[normalized] ?? bucket;
}

export function visibleSellingAgingBuckets(buckets: SellingAgingBucket[]): SellingAgingBucket[] {
  return buckets.filter((bucket) => bucket.amount > 0 || bucket.lienCount > 0);
}
