import {
  formatLegacyDateOnly,
  formatLegacyShortTimestamp,
} from "./format-date";

export function formatCurrency(amount?: number): string {
  if (amount == null) return "\u2014";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

export function formatDate(iso: string): string {
  return formatLegacyDateOnly(iso);
}

export function formatDateTime(iso: string): string {
  return formatLegacyShortTimestamp(iso, "UTC");
}

export function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  if (days < 30) return `${days}d ago`;
  return formatDate(iso);
}
