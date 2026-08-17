import type { SellingDashboardMetric } from '@/shared/api/endpoints/Liens';
import type { StatCardData } from '../dashboardShared';

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
