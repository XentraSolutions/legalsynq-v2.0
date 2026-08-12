export function formatFundingCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number.isFinite(value) ? value : 0);
}

export function formatFundingNumber(value: number): string {
  return new Intl.NumberFormat('en-US', {
    maximumFractionDigits: 0,
  }).format(Number.isFinite(value) ? value : 0);
}

export function formatFundingDate(value?: string | null): string {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return new Intl.DateTimeFormat('en-US', {
    month: '2-digit',
    day: '2-digit',
    year: 'numeric',
  }).format(date);
}

export function formatFundingDateTime(value?: string | null): string {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  const datePart = new Intl.DateTimeFormat('en-US', {
    month: '2-digit',
    day: '2-digit',
    year: 'numeric',
  }).format(date);
  const timePart = new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit',
  }).format(date);
  return `${datePart}  -  ${timePart}`;
}

export function formatFundingPercent(value?: number | null): string {
  if (value === undefined || value === null || !Number.isFinite(value)) return '-';
  return `${new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 }).format(value)}%`;
}

export function statusBadgeClass(status: string): string {
  const normalized = status.trim().toLowerCase();
  if (normalized === 'pending') return 'bg-amber-50 text-amber-700 ring-amber-200';
  if (normalized === 'accepted' || normalized === 'purchased') return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
  if (normalized === 'declined' || normalized === 'expired') return 'bg-rose-50 text-rose-700 ring-rose-200';
  return 'bg-slate-50 text-slate-600 ring-slate-200';
}
